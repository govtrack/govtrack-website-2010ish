#!/usr/bin/perl

use CGI;
use DBSQL;
use Data::Serializer;
use XML::LibXML;

use WmsConfig;

$CGI::DISABLE_UPLOADS = 1;

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

Go();
#eval { Go(); };

DBSQL::Close();

sub Go {
	my $kml;
	my $folder;
	if (CGI::param('format') eq 'kml') {
		$kml = XML::LibXML::Document->new();
		my $docnode = $kml->createElementNS('http://www.opengis.net/kml/2.2', 'kml');
		$kml->setDocumentElement($docnode);
		
		$folder = $kml->createElement('Folder');
		$docnode->appendChild($folder);
	} else {
		print <<EOF;
Content-type: text/plain

EOF
	}
	
	my $serializer = Data::Serializer->new();

	my $maxpoints = 1000;
	if (CGI::param('maxpoints')) {
		$maxpoints = int(CGI::param('maxpoints'));
		if ($maxpoints < 3 || $maxpoints > 1000000) { 
			$maxpoints = 1000;
		}
	}

	my $dataset = CGI::param('dataset');
	if ($dataset !~ /\S/) { print "No dataset name specified.\n"; return; }

	my $uri = CGI::param('region');

    my @regions;
    
    if ($uri) {
      @regions = DBSQL::Select("wmsgeometry INNER JOIN wmsidentifiers AS a ON wmsgeometry.dataset=a.id INNER JOIN wmsidentifiers AS b ON wmsgeometry.region=b.id",
                ["0, polygon, innerpt_long, innerpt_lat"],
                [DBSQL::SpecEQ('b.value', $uri),
                 DBSQL::SpecEQ('a.value', $dataset)]);
    } else {
	      @regions = DBSQL::Select("wmsgeometry INNER JOIN wmsidentifiers AS a ON wmsgeometry.dataset=a.id",
                ["0, polygon, innerpt_long, innerpt_lat"],
                [DBSQL::SpecEQ('a.value', $dataset)]);
    }

	# Deserialize all of the data.
	for my $region (@regions) {
		$points = $serializer->deserialize($$region[1]);
		$$region[1] = $points;
	}

	# Reduce the resolution of all of the polygons in the output
	# simultaneously.
	my $totalpoints;
	while (($totalpoints = countpoints(\@regions)) > $maxpoints) {
		# We can't remove points that occur in different line segments,
		# since if we remove one segment the point in another segment
		# will be left attached to nothing. TODO
	
		# Look at each nonoverlapping pair of points and get
		# a list of distances.
		my @dists;
		for my $region (@regions) {
			my $points = $$region[1];
			for (my $i = 0; $i < scalar(@$points); $i += 2) {
				my ($x1, $y1, $x2, $y2) = ($$points[$i][0], $$points[$i][1], $$points[$i+1][0], $$points[$i+1][1]);
				my $d = sqrt(($x1-$x2)**2 + ($y1-$y2)**2);
				push @dists, $d;
			}
		}
		@dists = sort(@dists);
			
		# How many points to collapse? We can cut the size by
		# at most half by collapsing all of the pairs. $collapse
		# is the proportion of pairs to collapes. It might be
		# greater than one: then we collapse them all and loop
		# around.
		my $collapse = ($totalpoints - $maxpoints) / scalar($totalpoints);
		$collapse *= scalar(@$dists);
		my $cdist = ($collapse < scalar(@$dists) ? $dists[$collapse] : 0);
		
		my $madechange = 0;
		
		for my $region (@regions) {
			my $points = $$region[1];
			my @newpoints;
			for (my $i = 0; $i < scalar(@$points); $i += 2) {
				my ($x1, $y1, $x2, $y2) = ($$points[$i][0], $$points[$i][1], $$points[$i+1][0], $$points[$i+1][1]);
				if (!defined($x2)) {
					push @newpoints, [$x1, $y1];
					next;
				}
				my $d = sqrt(($x1-$x2)**2 + ($y1-$y2)**2);
				if ($d <= $cdist || $cdist == 0) {
					push @newpoints, [($x1+$x2)/2, ($y1+$y2)/2];
					$madechange = 1;
				} else {
					push @newpoints, [$x1, $y1];
					push @newpoints, [$x2, $y2];
				}
			}
			$$region[1] = [@newpoints];
		}
		if (!$madechange) { last; }
	}
	
	for my $region (@regions) {
		my $uri = $$region[0];
		my $points = $$region[1];
		if (CGI::param('format') eq 'kml') {
			my $placemark = $kml->createElement('Placemark');
			$folder->appendChild($placemark);
	
			my $name = $kml->createElement('name');
			$placemark->appendChild($name);
			$name->appendText($uri);
	
			my $polygon = $kml->createElement('Polygon');
			$placemark->appendChild($polygon);

			my $am = $kml->createElement('altitudeMode');
			$polygon->appendChild($am);
			$am->appendText('clampToGround');
	
			my $ob = $kml->createElement('outerBoundaryIs');
			$polygon->appendChild($ob);

			my $lr = $kml->createElement('LinearRing');
			$ob->appendChild($lr);
		
			my $co = $kml->createElement('coordinates');
			$lr->appendChild($co);

			for my $xy (@$points) {
				$co->appendText("$$xy[0],$$xy[1],0\n");
			}
		
		} else {
			for my $xy (@$points) {
				print "$$xy[0] $$xy[1]\n";
			}
		}
	}

	if (CGI::param('format') eq 'kml') {
		print <<EOF;
Content-type: text/xml
Content-Disposition: inline; filename=region.kml

EOF

		print $kml->toString(1);
	}
}

sub countpoints {
	my $regions = $_[0];
	my $count = 0;
	for my $region (@$regions) {
		my $points = $$region[1];
		$count += scalar(@$points);
	}
	return $count;
}
