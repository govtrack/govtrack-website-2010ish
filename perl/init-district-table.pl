#!/usr/bin/perl

use Geo::ShapeFile;
use Data::Serializer;

require "sql.pl";

DBOpen('govtrack', 'govtrack', undef);

DBExecute("DROP TABLE cdgeometry;");
DBExecute("CREATE TABLE cdgeometry (session TINYINT, state CHAR(2), district TINYINT, long_min DOUBLE, long_max DOUBLE, lat_min DOUBLE, lat_max DOUBLE, polygon TEXT, smallpolygon TEXT);");
DBExecute("CREATE INDEX state_dist ON cdgeometry (state, district);");
DBExecute("CREATE INDEX long_min ON cdgeometry (long_min);");
DBExecute("CREATE INDEX long_max ON cdgeometry (long_max);");
DBExecute("CREATE INDEX lat_min ON cdgeometry (lat_min);");
DBExecute("CREATE INDEX lat_max ON cdgeometry (lat_max);");

$session = 110;

my $dir = "/home/govtrack/extdata/gis";
require "/home/govtrack/scripts/gis/states_109.pl";

$shapefile = new Geo::ShapeFile("$dir/cd99_" . $session);

$serializer = Data::Serializer->new(
	serializer => 'Storable',
	compress => 1);
	
foreach my $shidx (1 .. $shapefile->records()) {
	my $shape = $shapefile->get_shp_record($shidx);
 	if ($shape->shape_type() != 5) { next; } # polygon shape type

	my %db = $shapefile->get_dbf_record($shidx);
	my $state = $CDIST109STATES{int($db{STATE})};
	my $dist = int($db{CD});	
	if ($state eq "" || $db{CD} eq "") { next; }
	
	if ($dist > 90) { $dist = 0; }
	
	print "$state$dist\n";

	foreach my $pidx (1 .. $shape->num_parts()) {
		my @shpoints = $shape->get_part($pidx);
		
		my ($x1, $x2, $y1, $y2) = (undef, undef, undef, undef);
		
		my @points;
		for my $pt (@shpoints) {
			my ($x, $y) = ($pt->X(), $pt->Y());
			
			# Hack for U.S.: Alaska wraps around, so for sanity
			# we allow for longitudes less than -180.
			if ($x > 0) { $x -= 360; }
		
			push @points, [$x, $y];
			
			if (!defined($x1) || $x < $x1) { $x1 = $x; }
			if (!defined($y1) || $y < $y1) { $y1 = $y; }
			if (!defined($x2) || $x > $x2) { $x2 = $x; }
			if (!defined($y2) || $y > $y2) { $y2 = $y; }
		}
		
		# maximum of 3,000 points in any polygon because we can't
		# seem to reliably serialize/deserialize larger arrays
		# through MySQL.
		simplify(\@points, 3000);
		my $polygon = $serializer->serialize(\@points);

		my @smallpoints = @points;
		simplify(\@smallpoints, 300);
		my $smallpolygon = $serializer->serialize(\@smallpoints);
	
		DBInsert(cdgeometry,
			session => $session,
			state => $state,
			district => $dist,
			long_min => $x1,
			long_max => $x2,
			lat_min => $y1,
			lat_max => $y2,
			polygon => $polygon,
			smallpolygon => $smallpolygon);
	}
}

DBClose();

sub simplify {
	my $points = shift;
	my $maxpoints = shift;
	
	while (scalar(@$points) > $maxpoints) {
		# Find the smallest distance between any two points.
		my $mindist;
	
		for my $i (0..scalar(@$points)-1) {
			my ($x1, $y1) = @{@$points[$i]};
			my ($x2, $y2) = @{@$points[($i+1) % scalar(@$points)]};
			
			my $d = ($x2-$x1)**2 + ($y2-$y1)**2;
			if (!defined($mindist) || $d < $mindist) { $mindist = $d; }
		}
		
		# Cut out points at a distance of 1.1*mindist.
		my @newpoints;
		for my $i (0..scalar(@$points)-1) {
			my ($x1, $y1) = @{@$points[$i]};
			my ($x2, $y2) = @{@$points[($i+1) % scalar(@$points)]};
			
			my $d = ($x2-$x1)**2 + ($y2-$y1)**2;
			if ($d > $mindist * 1.1) {
				push @newpoints, @$points[$i];
			}
		}
		
		@$points = @newpoints;
	}
}
