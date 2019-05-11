#!/usr/bin/perl

use CGI;
use DBSQL;
use WmsConfig;
use WmsRestOutput;

$CGI::DISABLE_UPLOADS = 1;

print <<EOF;
Content-type: text/plain

EOF

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

eval { PrintMarkers(); };

DBSQL::Close();

sub PrintMarkers {
	my $styleset = CGI::param('layer');
	if ($styleset !~ /\S/) { print "No layer name specified.\n"; return; }

	my ($long1,$lat1,$long2,$lat2) = split(/\s*,\s*/, CGI::param('BBOX'));
	if (!defined($long1) || !defined($long2) || !defined($lat1) || !defined($lat2)) {
		print "Specify BBOX as long1,lat1,long2,lat2.\n";
		return;
	}
	
	my $limit = 1000;
	if (CGI::param('limit') && CGI::param('limit') < $limit) { $limit = CGI::param('limit')+1; }
	
	my @datasets = DBSQL::SelectVectorDistinct("wmsstyles", ["dataset"], [DBSQL::SpecEQ(styleset, $styleset)]);

    my @regions = DBSQL::Select("wmsgeometry INNER JOIN wmsstyles ON wmsgeometry.region=wmsstyles.region AND wmsgeometry.dataset=wmsstyles.dataset
    	INNER JOIN wmsidentifiers ON wmsgeometry.region=wmsidentifiers.id",
                ["wmsidentifiers.value, innerpt_long, innerpt_lat, markerdata"],
                [DBSQL::SpecEQ('wmsstyles.styleset', $styleset),
                 DBSQL::SpecIn('wmsgeometry.dataset', @datasets),
                 DBSQL::SpecIn('wmsstyles.dataset', @datasets),
                 DBSQL::SpecGE(innerpt_long, $long1), DBSQL::SpecLE(innerpt_long, $long2),
                 DBSQL::SpecGE(innerpt_lat, $lat1), DBSQL::SpecLE(innerpt_lat, $lat2),
                 DBSQL::SpecGE('long_max-long_min', int(CGI::param('min_long_size'))),
                 DBSQL::SpecGE('lat_max-lat_min', int(CGI::param('min_lat_size'))),
                 ],
                 "LIMIT $limit");
	
	if (scalar(@regions) >= $limit) { @regions = (); } # return nothing to singal too many matches

    WmsRestOutput::PrintResults(\@regions, ['string', 'num', 'num', 'string']);

}


