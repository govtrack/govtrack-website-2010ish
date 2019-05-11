#!/usr/bin/perl

use CGI;
use DBSQL;
use Data::Serializer;
use Math::Geometry::Planar;

use WmsConfig;
use WmsRestOutput;

$CGI::DISABLE_UPLOADS = 1;

print <<EOF;
Content-type: text/plain

EOF

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

eval { Search(); };

DBSQL::Close();

sub Search {
	my $styleset = CGI::param('layer');
	if ($styleset !~ /\S/) { print "No layer name specified.\n"; return; }

	my $lat = CGI::param('lat');
	my $long = CGI::param('long');
	if ($lat !~ /\S/ || $long !~ /\S/) { print "No lat and long parameters specified."; return; }
	
	my @datasets = DBSQL::SelectVectorDistinct("wmsstyles", ["dataset"], [DBSQL::SpecEQ(styleset, $styleset)]);
	if (scalar(@datasets) == 0) { print "No data for this layer.\n"; return; }

    my @regions1 = DBSQL::Select("wmsgeometry INNER JOIN wmsstyles ON wmsgeometry.region=wmsstyles.region AND wmsgeometry.dataset=wmsstyles.dataset
    	INNER JOIN wmsidentifiers ON wmsgeometry.region=wmsidentifiers.id",
                ["polygon, wmsidentifiers.value, innerpt_long, innerpt_lat, markerdata, area"],
                [DBSQL::SpecEQ('wmsstyles.styleset', $styleset),
                 DBSQL::SpecIn('wmsgeometry.dataset', @datasets),
                 DBSQL::SpecIn('wmsstyles.dataset', @datasets),
                 DBSQL::SpecGE(long_max, $long), DBSQL::SpecLE(long_min, $long),
                 DBSQL::SpecGE(lat_max, $lat), DBSQL::SpecLE(lat_min, $lat)]);

	my @regions;

	my $serializer = Data::Serializer->new();
	for my $r (@regions1) {
		my $poly = shift(@$r);
		$poly = $serializer->deserialize($poly);
		if (scalar(@$poly) < 3) { next; }
		my $p = Math::Geometry::Planar->new;
		$p->points($poly);
		if ($p->isinside([$long, $lat])) {
			push @regions, $r;
		}
	}
	
	WmsRestOutput::PrintResults(\@regions, ['string', 'num', 'num', 'string', 'float'], ['URI', 'innerpt_long', 'innerpt_lat', 'markerdata', 'area']);
}


