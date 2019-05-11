#!/usr/bin/perl

use Data::Dumper;
use Unicode::MapUTF8 qw(to_utf8 from_utf8 utf8_supported_charset);
use Geo::ShapeFile;
use Geo::Proj4;
use JSON;
use DBSQL;
use WmsConfig;
use WmsGeometry;

if (scalar(@ARGV) < 2) {
	print <<EOF;
Usage: perl load-shapefile.pl datasetname shapefile.zip [dbkey] [labelkey]
	Omit dbkey on your first attempt to get a printout of
	keys in the shapefile database. Choose one that will
	be the ID of each shape. Then use this as the dbkey
	in your second run of this program.
EOF
}

my @Colors = (
	'255,51,102', '255,255,0', '153,51,255', '102,255,0',
	'0,51,102', '255,0,204', '255,102,0', '0,102,102',
	'153,0,153', '204,102,102', '0,255,255', '153,153,102');

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

# Remember which regions we've seen (since we may see them more
# than once) and which colors we assigned (so we don't give the
# same color to neighboring regions).
my %color;

my $srcproj = Geo::Proj4->new("+proj=latlong +datum=WGS84");
my $dstproj = Geo::Proj4->new("+proj=latlong +datum=WGS84");
if ($ENV{SOURCE_PROJECTION}) { $srcproj = Geo::Proj4->new($ENV{SOURCE_PROJECTION}); if (!$srcproj) { die "Bad projection."; } }

AddShapeFile($ARGV[0], $ARGV[1], 0, 1);

DBSQL::Delete(wmscache, [DBSQL::SpecEQ(styleset, $ARGV[0])]);
        
DBSQL::Close();

#############################################################3

sub SetStart {
	my $dataset = shift;
	my $tmpid = 0; # always load into dataset 0
	DBSQL::Delete(wmsgeometry, [DBSQL::SpecEQ(dataset, $tmpid)]);
	DBSQL::Delete(wmsstyles, [DBSQL::SpecEQ(styleset, $ARGV[0])]);
	return $tmpid;
}

sub SetEnd {
	my $dataset = shift;
	my $tmpid = shift;
	
	my ($id) = DBSQL::SelectFirst(wmsidentifiers, ['id'], [DBSQL::SpecEQ('value', $dataset)]);
	if (defined($id)) {
		# Delete the old data set.
		DBSQL::Delete(wmsgeometry, [DBSQL::SpecEQ(dataset, $id)]);
	} else {
		($id) = DBSQL::SelectFirst(wmsidentifiers, ['max(id)']);
		$id++;
		DBSQL::Insert(wmsidentifiers, 'id' => $id, 'value' => $dataset);
	}
	
	# Change the dataset id of the new data set.
	DBSQL::Update(wmsgeometry, [DBSQL::SpecEQ(dataset, 0)], dataset => $id);
	DBSQL::Update(wmsstyles, [DBSQL::SpecEQ(dataset, 0)], dataset => $id);
}

sub AddShapeFile {
	my $partial = 0; if ($_[0] eq 'PARTIAL') { $partial = shift; }
	my $dataset = shift;
	my $filename = shift;
	my $filenum = shift;
	my $nfiles = shift;
	
	my $tmpid;
	my $lastid;
	
	# drop any unfinished loading into a temporary data set
	if ($partial ne 'PARTIAL') {
		$tmpid = SetStart($dataset);
	} else {
		$tmpid = $dataset;
	}
	
	my $tmpdir = "/tmp/init-wms-geometry";
	if ($filename =~ /\.zip$/) {
		system("rm -rf $tmpdir; mkdir -p $tmpdir; unzip -d $tmpdir $filename");
		$filename =~ s/^.*\///;
		$filename =~ s/\.zip$//;
		$filename = "$tmpdir/$filename";
		
	} elsif ($filename =~ s/\.shp$//) {
		# just chop off .shp before passing to Geo::ShapeFile
	}
	
	my $shapefile = new Geo::ShapeFile($filename);

	my $lastprog;
	
	foreach my $shidx (1 .. $shapefile->records()) {
		my $prog = int($shidx/$shapefile->records() * 25) * 4;
		if ($prog != $lastprog) {
			my $overallprog = int(($filenum + $prog/100) / $nfiles * 100);
			print "$filename $prog\%... ($overallprog\%...)\n";
			$lastprog = $prog;
		}
	
		my $shape = $shapefile->get_shp_record($shidx);
 		if ($shape->shape_type() != 5) { next; } # polygon shape type

		my %db = $shapefile->get_dbf_record($shidx);
		if ($db{_deleted}) { next; }

		my $region;
		if (!$ARGV[2]) {
			$Data::Dumper::Terse = 1;
			$Data::Dumper::Sortkeys = 1;
			print Dumper([\%db]);
			next;
		} else {
			$region = $db{$ARGV[2]};
		}
		if (!defined($region)) { next; }

		my ($id) = DBSQL::SelectFirst(wmsidentifiers, ['id'], [DBSQL::SpecEQ('value', $region)]);
		if (!defined($id)) {
			if (!defined($lastid)) {
				($lastid) = DBSQL::SelectFirst(wmsidentifiers, ['max(id)']);
			}
			$id = ++$lastid;
			$lastid = $id;
			DBSQL::Insert(wmsidentifiers, 'id' => $id, 'value' => $region);
		}
		
		my ($minx, $maxx, $miny, $maxy);
		foreach my $pidx (1 .. $shape->num_parts()) {
			my @shpoints = $shape->get_part($pidx);
		
			my @points;
			for my $pt (@shpoints) {
				my ($x, $y) = @{ $srcproj->transform($dstproj, [$pt->X(), $pt->Y()]) };
				
				# Hack for U.S.: Alaska wraps around, so for sanity
				# we allow for longitudes less than -180.
				if ($x > 0) { $x -= 360; }
		
				push @points, [$x, $y];
				
				if (!defined($minx)) { $minx = $x; $miny = $y; $maxx = $x; $maxy = $y; }
				if ($x < $minx) { $minx = $x; }
				if ($x > $maxx) { $axnx = $x; }
				if ($y < $miny) { $miny = $y; }
				if ($y > $maxy) { $maxy = $y; }
			}
			
			WmsGeometry::AddRegionPolygon($id, $tmpid, \@points, $region);
		}

		# Choose a color index not in use by a neighboring region

		if ($color{$id}) {
			warn "Region $id appears more than once in the shapefile.";
			next;
		}
		
		# Look at which colors are in use in the bounding box.
		my @neighbors = DBSQL::SelectVector("wmsgeometry", ['region'],
			[DBSQL::SpecGE(long_max, $minx - ($maxx - $minx)/8),
			 DBSQL::SpecLE(long_min, $maxx + ($maxx - $minx)/8),
			 DBSQL::SpecGE(lat_max, $miny - ($maxy - $miny)/8),
			 DBSQL::SpecLE(lat_min, $maxy + ($maxy - $miny)/8),
			 DBSQL::SpecEQ(dataset, $tmpid)]);
		my %inuse;
		for my $n (@neighbors) {
			if (defined($color{$n})) { $inuse{$color{$n}} = 1; }
		}
		
		# choose an unused color index for this region
		my $color;
		for (my $i = 0; $i < scalar(@Colors); $i++) {
			if (rand() < .33) { next; } # randomly skip colors for variety
			if (!$inuse{$i}) { $color = $i; last; }
		}
		if (!defined($color)) {
			warn "Not enough colors available.";
			$color = int(rand() * scalar(@Colors));
		}
		
		# mark that we've done this region with this color index
		$color{$id} = $color;

		my $fillcolor = $Colors[$color];
		
		my $label;
		if ($ARGV[3]) { $label = $db{$ARGV[3]}; }
		
		my $markerdata = JSON::to_json(\%db);
		
		my $mode = '';
		
		DBSQL::Insert(wmsstyles,
			styleset => $ARGV[0],
			dataset => $tmpid,
			region => $id,
			bordercolor => "128,128,128",
			borderweight => "2",
			fillcolor => $fillcolor,
			);

		DBSQL::Insert(wmsstyles,
			styleset => $ARGV[0] . "_bl",
			dataset => $tmpid,
			region => $id,
			bordercolor => "128,128,128",
			borderweight => "3",
			textcolor => "0,0,0",
			label => $label,
			markerdata => $markerdata,
			);
	}

	if ($partial ne 'PARTIAL') {
		SetEnd($dataset, $tmpid);
	}
}

	
