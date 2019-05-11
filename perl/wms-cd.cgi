#!/usr/bin/perl

# This API is used for the embeddable Google Maps congressional district widget,
# but as of January 2009, I am issuing a redirect to my new WMS map server
# to get the equivalent tile from there, retiring the custom tile drawing code
# here that preceded that system.

use CGI;

$CGI::DISABLE_UPLOADS = 1;

$layers = CGI::param('LAYERS');
$styles = CGI::param('STYLES');
$srs = CGI::param('SRS');
$bbox = CGI::param('BBOX');
$width = CGI::param('WIDTH');
$height = CGI::param('HEIGHT');
$format = CGI::param('FORMAT');

if ($layers =~ /outline/) {
	$layers2 = "cd-110-outlines";
} else {
	$layers2 = "cd-110";
}

if ($layers =~ /state=(\w\w)/) {
	$layers2 .= ":http://www.rdfabout.com/rdf/usgov/geo/us/$1/cd/110/\%";
} elsif ($layers =~ /district=(\w\w)(\d+)/) {
	if ($2 == 0) {
		$layers2 .= ":http://www.rdfabout.com/rdf/usgov/geo/us/$1";
	} else {
		$layers2 .= ":http://www.rdfabout.com/rdf/usgov/geo/us/$1/cd/110/" . int($2);
	}
}

		
print <<EOF;
Status: 301
Location: http://www.govtrack.us/perl/wms/wms.cgi?LAYERS=$layers2\&SRS=$srs\&BBOX=$bbox\&WIDTH=$width\&HEIGHT=$height\&FORMAT=$format\&STYLES=$styles

EOF

