package WmsGeometry;

use Data::Serializer;
use Math::Geometry::Planar;

use SphericalPolygonArea;

my $triangle = "triangle/triangle -p";

my $serializer = Data::Serializer->new(
	serializer => 'Storable',
	compress => 1);

1;

sub AddRegionPolygon {
	my ($regionid, $datasetid, $points, $regionuri) = @_;

	# get bounds of polygon
	my ($x1, $x2, $y1, $y2) = (undef, undef, undef, undef);
	for my $pt (@$points) {
		my ($x, $y) = @$pt;
		if (!defined($x1) || $x < $x1) { $x1 = $x; }
		if (!defined($y1) || $y < $y1) { $y1 = $y; }
		if (!defined($x2) || $x > $x2) { $x2 = $x; }
		if (!defined($y2) || $y > $y2) { $y2 = $y; }
	}

	# maximum of 5000 points in any polygon to make sure we
	# dont go over the storage limit of a LONGBLOB
	simplify($points, 5000);
	my $polygon = $serializer->serialize($points);

	my @smallpoints = @$points;
	simplify(\@smallpoints, 300);
	my $smallpolygon = $serializer->serialize(\@smallpoints);
			
	my @ultrasmallpoints = @smallpoints;
	simplify(\@ultrasmallpoints, 50);
	my ($ix, $iy);
	if (scalar(@ultrasmallpoints) >= 3) {
		($ix, $iy) = FindInteriorPoint(@ultrasmallpoints);
	} else {
		$ix = ($x1+$x2)/2;
		$iy = ($y1+$y2)/2;
	}

	my $area = 0;
	if (scalar(@$points) >= 3) {
		$area = SphericalPolygonArea::GetArea(@$points)
			* $SphericalPolygonArea::EarthSqMiPerSphericalDegree;
	}

	# load into the temporary data set
	DBSQL::Insert(wmsgeometry,
		dataset => $datasetid,
		region => $regionid,
		long_min => $x1,
		long_max => $x2,
		lat_min => $y1,
		lat_max => $y2,
		innerpt_long => $ix,
		innerpt_lat => $iy,
		polygon => $polygon,
		smallpolygon => $smallpolygon,
		area => $area);
}

sub FindInteriorPoint {
	my @points = @_; # array of [x,y] arrayrefs
	
	my $polyfile = "/tmp/triangle.poly";
	my $nodefile = "/tmp/triangle.1.node";
	my $elefile = "/tmp/triangle.1.ele";

	my @edges;

	open POLY, ">$polyfile" or die $@;
	print POLY scalar(@points) . " 2 0 0\n";
	for ($i = 0; $i < scalar(@points); $i++) {
		print POLY "$i $points[$i][0] $points[$i][1]\n";
	}
	print POLY scalar(@points) . " 0\n";
	for ($i = 0; $i < scalar(@points); $i++) {
		$i2 = ($i + 1) % scalar(@points);
		print POLY "$i $i $i2\n";
		push @edges, [$points[$i], $points[$i2]];
	}
	print POLY "0\n";
	close POLY;

	system("$triangle $polyfile > /dev/null");

	undef @points;

	open (POINTS, "<$nodefile") || die $@;
	my $line = <POINTS>;
	while (!eof(POINTS)) {
		$line = <POINTS>; chop $line;
		if ($line =~ /\A\#/) { next; }
		$line =~ s/\A\s+//g;
		my ($num, $x, $y) = split(/\s+/, $line);
		$points[$num][0] = $x;
		$points[$num][1] = $y;
	}
	close POINTS;

	open ELEMS, "<$elefile" or die $@;
	$line = <ELEMS>;
	my @cpoints;
	while (!eof(ELEMS)) {
		$line = <ELEMS>; chop $line;
		if ($line =~ /\A\#/) { next; }
		$line =~ s/\A\s+//g;
		my $num;
		my @e;
		($num, $e[0], $e[1], $e[2]) = split(/\s+/, $line);
		my $x = ($points[$e[0]][0]+$points[$e[1]][0]+$points[$e[2]][0])/3;
		my $y = ($points[$e[0]][1]+$points[$e[1]][1]+$points[$e[2]][1])/3;
		push @cpoints, [$x, $y];
		#for (my $i = 0; $i <= 2; $i++) {
		#	my $a = $e[$i];
		#	my $b = $e[($i+1) % 3];
		#	my $x = ($points[$a][0]+$points[$b][0])/2;
		#	my $y = ($points[$a][1]+$points[$b][1])/2;
		#	push @cpoints, [$x, $y];
		#}
	}
	close ELEMS;
	
	my $minptdist;
	my $minpt;
	
	foreach my $pt (@cpoints) {
		my $mindist;
		foreach my $edge (@edges) {
			eval {
				my $d = abs(DistanceToSegment([$$edge[0], $$edge[1], $pt]));
				if (!defined($mindist) || $mindist > $d) { $mindist = $d; }
			};
			#if (defined($mindist) && defined($minptdist) && $mindist < $minptdist) { last; }
		}
		if (defined($mindist) && (!defined($minptdist) || $minptdist < $mindist)) {
			$minptdist = $mindist; $minpt = $pt;
		}
	}
	
	return $$minpt[0], $$minpt[1];
}


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
		
		if (scalar(@newpoints) < 10 || scalar(@newpoints) == scalar(@$points)) {
			print "Failed to cull below " . scalar(@newpoints) . " points.\n";
			last;
		}
		
		@$points = @newpoints;
	}
}

