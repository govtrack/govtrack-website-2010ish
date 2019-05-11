#!/usr/bin/perl

use Encode;
use Geo::ShapeFile;
use DBSQL;
use WmsGeometry;
use Data::Dumper;

for my $arg (@ARGV) { $ACTIONS{$arg} = 1; }

my $pw = `cat ~/priv/mysqlpw`;
chop $pw;
DBSQL::Open('govtrack', 'govtrack', $pw);

if ($ACTIONS{TABLE}) {
	DBSQL::Execute("DROP TABLE wmsidentifiers;");
	DBSQL::Execute("CREATE TABLE wmsidentifiers (id INT, value TEXT NOT NULL);");
	DBSQL::Execute("CREATE UNIQUE INDEX id ON wmsidentifiers (id);");
	DBSQL::Execute("CREATE UNIQUE INDEX value ON wmsidentifiers (value(255));");

	DBSQL::Execute("DROP TABLE wmsgeometry;");
	DBSQL::Execute("CREATE TABLE wmsgeometry (dataset INT NULL, region INT NOT NULL, long_min DOUBLE NOT NULL, long_max DOUBLE NOT NULL, lat_min DOUBLE NOT NULL, lat_max DOUBLE NOT NULL, innerpt_long DOUBLE NOT NULL, innerpt_lat DOUBLE NOT NULL, polygon LONGBLOB NOT NULL, smallpolygon BLOB NOT NULL, area FLOAT NOT NULL);");
	DBSQL::Execute("CREATE INDEX reg ON wmsgeometry (dataset, region);");
	DBSQL::Execute("CREATE INDEX long_min ON wmsgeometry (dataset, long_min);");
	DBSQL::Execute("CREATE INDEX long_max ON wmsgeometry (dataset, long_max);");
	DBSQL::Execute("CREATE INDEX lat_min ON wmsgeometry (dataset, lat_min);");
	DBSQL::Execute("CREATE INDEX lat_max ON wmsgeometry (dataset, lat_max);");
}

if ($ACTIONS{STATES}) {
	AddShapeFile(
		"http://www.rdfabout.com/rdf/usgov/us/states",
		"/home/govtrack/extdata/gis/national/tl_2008_us_state",
		0, 1,
		\&States);
}

if ($ACTIONS{COUNTIES}) {
	LoadCountyNames();
	AddShapeFile(
		"http://www.rdfabout.com/rdf/usgov/us/counties",
		"/home/govtrack/extdata/gis/national/tl_2008_us_county",
		0, 1,
		\&Counties);
}

if ($ACTIONS{DISTRICTS}) {
	require "/home/govtrack/scripts/gis/states_109.pl";
	AddShapeFiles(
		"http://www.rdfabout.com/rdf/usgov/congress/house/110",
		"/home/govtrack/extdata/gis/by-state/", "cd111",
		\&CD);
}

if ($ACTIONS{ZCTAS}) {
	AddShapeFiles(
		"http://www.rdfabout.com/rdf/usgov/us/zctas",
		"/home/govtrack/extdata/gis/zcta", ".*",
		0, 1,
		\&ZCTA);
}

if ($ACTIONS{COUNTYSUBS}) {
	LoadCountyNames();
	AddShapeFiles(
		"http://www.rdfabout.com/rdf/usgov/us/countysubs",
		"/home/govtrack/extdata/gis/by-state/", "cousub",
		\&CountySubs);
}

if ($ACTIONS{CDPS} && 0) { # broken
	LoadCountyNames();
	AddShapeFiles(
		"http://www.rdfabout.com/rdf/usgov/us/cdps",
		"/home/govtrack/extdata/gis/by-state/", "place",
		\&CensusDataPlaces);
}

if ($ACTIONS{SLDL}) {
	# state legislative district - lower
	AddShapeFiles(
		"http://www.rdfabout.com/rdf/usgov/us/sld/lower",
		"/home/govtrack/extdata/gis/by-state", "sldl",
		\&SLD,
		_district_type => 'lower');
}
if ($ACTIONS{SLDU}) {
	# state legislative district - upper
	AddShapeFiles(
		"http://www.rdfabout.com/rdf/usgov/us/sld/upper",
		"/home/govtrack/extdata/gis/by-state", "sldu",
		\&SLD,
		_district_type => 'upper');
}

DBSQL::Close();

sub States {
	my %db = @_;
	return "http://www.rdfabout.com/rdf/usgov/geo/us/" . lc($db{STUSPS});
}

sub LoadCountyNames {
	require "/home/govtrack/scripts/gis/states_109.pl";
	my $shapefile = new Geo::ShapeFile("/home/govtrack/extdata/gis/national/tl_2008_us_county");
	foreach my $shidx (1 .. $shapefile->records()) {
		my %db = $shapefile->get_dbf_record($shidx);
		$FIPS_County{$db{STATEFP} . "|" . $db{COUNTYFP}} =
			"http://www.rdfabout.com/rdf/usgov/geo/us/" . lc($CDIST109STATES{$db{STATEFP}}) . '/counties/' . PlaceNameUri($db{NAMELSAD});
	}
}

sub PlaceNameUri {
	# This matches exactly what I have in census.pl so that I can reconstruct
	# the right URIs without having to look up FIPS codes, and in case any
	# names of places changed or were added, we want the latest.
	my $name = shift;
	$name = decode('WinLatin1', $name); # ?
	$name =~ s/ CCD$//g;
	$name =~ s/ CDP$//g;
	$name = lc($name);
	$name =~ s/\.//g;
	$name =~ s/\W/_/g;
	return $name;
}

sub Counties {
	my %db = @_;
	return $FIPS_County{$db{STATEFP} . "|" . $db{COUNTYFP}};
}

sub CD {
	my %db = @_;

	my $session = 110;
	my $state = lc($CDIST109STATES{int($db{STATEFP10})});
	my $dist = int($db{CD111FP});	
	if ($state eq "" || $dist eq "") { warn Dumper(\%db); return undef; }
	
	if ($dist > 90) { $dist = 0; }
	
	#print "$state$dist $db{NAMELSAD} \n";
	if ($dist > 0) {
		return "http://www.rdfabout.com/rdf/usgov/geo/us/$state/cd/$session/$dist";
	} else {
		return "http://www.rdfabout.com/rdf/usgov/geo/us/$state";
	}
}

sub ZCTA {
	my %db = @_;
	return "http://www.rdfabout.com/rdf/usgov/geo/census/zcta/$db{NAME}";
}

sub CountySubs {
	my %db = @_;
	return $FIPS_County{$db{STATEFP} . "|" . $db{COUNTYFP}} . "/" . PlaceNameUri($db{NAMELSAD});
}

sub CensusDataPlaces {
	my %db = @_;

	die Dumper([\%db]);
	
	my $uri = $FipsCDP{"$db{STATE}:$db{PLACEFP}"};
	if ($uri eq 'MULTIPLE') {
		print "Multiple matches for $db{NAME} $db{STATE} $db{PLACEFP}\n";
		return undef;
	}
	
	if (!$uri) {
		print "No matches for $db{NAME} $db{STATE} $db{PLACEFP}\n";
		return undef;
	}

	return $uri;
}

sub SLD {
	my %db = @_;
	
	my %states = (
	1 => AL, 2 => AK, 4 => AZ, 5 => AR, 6 => CA, 8 => CO, 9 => CT, 10 =>
	DE, 11 => DC, 12 => FL, 13 => GA, 15 => HI, 16 => ID, 17 => IL, 18 =>
	IN, 19 => IA, 20 => KS, 21 => KY, 22 => LA, 23 => ME, 24 => MD, 25 =>
	MA, 26 => MI, 27 => MN, 28 => MS, 29 => MO, 30 => MT, 31 => NE, 32 =>
	NV, 33 => NH, 34 => NJ, 35 => NM, 36 => NY, 37 => NC, 38 => ND, 39 =>
	OH, 40 => OK, 41 => OR, 42 => PA, 44 => RI, 45 => SC, 46 => SD, 47 =>
	TN, 48 => TX, 49 => UT, 50 => VT, 51 => VA, 53 => WA, 54 => WV, 55 =>
	WI, 56 => WY, 60 => AS, 66 => GU, 69 => MP, 72 => PR, 78 => VI
	);
	
	my $name;
	if ($db{'_district_type'} eq 'lower') {
		$name = $db{'SLDLST'};
	} else {
		$name = $db{'SLDUST'};
	}
	
	if ($name eq 'ZZZ') {
		# area of a county in which no legislative district is
		# identified
		return undef;
	}
	
	$name =~ s/^0+//;

	return "http://www.rdfabout.com/rdf/usgov/geo/us/"
		. lc($states{int($db{STATEFP})})
		. "/legislature/" . $db{'_district_type'}
		. "/" . $name;
	
}

#############################################################3

sub AddShapeFiles {
	my $dataset = shift;
	my $dir = shift;
	my $pattern = shift;
	my $proc = shift;
	my @additional_args = @_;
	
	# drop any unfinished loading into a temporary data set
	my $tmpid = SetStart($dataset);

	opendir D, "$dir";
	my $ctr = 0;
	my @files;
	foreach my $f (readdir(D)) {
		if ($f =~ /$pattern/ && $f =~ /^(.*\.(shp|zip))$/) {
			push @files, $1;
		}
	}
	foreach my $f (@files) {
		AddShapeFile(PARTIAL, $tmpid, "$dir/$f", $ctr++, scalar(@files), $proc, _source_file => $f, @additional_args);
	}
	closedir D;

	SetEnd($dataset, $tmpid);
}

sub SetStart {
	my $dataset = shift;
	my $tmpid = 0; # always load into dataset 0
	DBSQL::Delete(wmsgeometry, [DBSQL::SpecEQ(dataset, $tmpid)]);
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
}

sub AddShapeFile {
	my $partial = 0; if ($_[0] eq 'PARTIAL') { $partial = shift; }
	my $dataset = shift;
	my $filename = shift;
	my $filenum = shift;
	my $nfiles = shift;
	my $proc = shift;
	my %procargs = @_;
	
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
		
		my $region = &$proc(%db, %procargs);
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
		
		foreach my $pidx (1 .. $shape->num_parts()) {
			my @shpoints = $shape->get_part($pidx);
		
			my @points;
			for my $pt (@shpoints) {
				my ($x, $y) = ($pt->X(), $pt->Y());
			
				# Hack for U.S.: Alaska wraps around, so for sanity
				# we allow for longitudes less than -180.
				if ($x > 0) { $x -= 360; }
		
				push @points, [$x, $y];
			}
			
			WmsGeometry::AddRegionPolygon($id, $tmpid, \@points, $region);
		}
	}

	if ($partial ne 'PARTIAL') {
		SetEnd($dataset, $tmpid);
	}
}

