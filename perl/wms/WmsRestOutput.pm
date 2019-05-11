#!/usr/bin/perl

package WmsRestOutput;

1;

sub PrintResults {
	my @results = @{ $_[0] };
	my @fieldtypes = @{ $_[1] };
	my @fieldnames = @{ $_[2] };

	if (CGI::param('format') eq '' || CGI::param('format') eq 'text') {
		for my $r (@results) {
			for (my $i = 0; $i < scalar(@$r); $i++) {
				if (!defined($$r[$i]) && $fieldtypes[$i] eq 'numeric') {
					$$r[$i] = 'null';
				}
			}
			print join("\t", @$r) . "\n";
		}
	} elsif (CGI::param('format') eq 'json') {
		if (CGI::param('json_callback')) {
			print CGI::param('json_callback') . "(";
		} else {
			print "{ \"regions\": ";
		}
		print "[\n";
		for (my $i = 0; $i < scalar(@results); $i++) {
			print "{ ";
			my $r = $results[$i];
			for (my $j = 0; $j < scalar(@$r); $j++) {
				print "\"$fieldnames[$j]\": ";
				if ($fieldtypes[$j] eq 'string') {
					$$r[$j] =~ s#\\#\\\\#g;
					$$r[$j] =~ s#"#\\"#g;
					$$r[$j] = "\"$$r[$j]\"";
				}
				print "$$r[$j]";
				print ", " if ($j < scalar(@$r)-1);
			}
			print "}";
			print "," if ($i < scalar(@results)-1);
			print "\n";
		}
		if (CGI::param('json_callback')) {
			print "]);\n";
		} else {
			print "]}\n";
		}
	} elsif (CGI::param('format') eq 'osm') {
		print '<osm version="0.6" generator="GovTrack.us WMS Server">' . "\n";
		for (my $i = 0; $i < scalar(@results); $i++) {
			my $r = $results[$i];
			my ($lng, $lat);
			for (my $j = 0; $j < scalar(@$r); $j++) {
				if ($fieldnames[$j] eq 'long') { $lng = $$r[$j]; }
				if ($fieldnames[$j] eq 'lat') { $lat = $$r[$j]; }
			}
			print "\t<node id='$i' lon='$lng' lat='$lat'>\n";
			for (my $j = 0; $j < scalar(@$r); $j++) {
				print "\t\t<tag k='$fieldnames[$j]' v='$$r[$j]'/>\n";
			}
			print "\t</node>\n";
		}
		print "</osm>\n";
	}
}
