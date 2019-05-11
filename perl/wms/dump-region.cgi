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

eval { Go(); };

DBSQL::Close();

sub Go {
	my $dataset = CGI::param('dataset');
	if ($dataset !~ /\S/) { print "No dataset name specified.\n"; return; }

	my $uri = CGI::param('region');
	if ($uri !~ /\S/) { print "No region name specified.\n"; return; }

    my @region = DBSQL::Select("wmsgeometry INNER JOIN wmsidentifiers AS a ON wmsgeometry.dataset=a.id INNER JOIN wmsidentifiers AS b ON wmsgeometry.region=b.id",
                ["polygon, innerpt_long, innerpt_lat"],
                [DBSQL::SpecEQ('b.value', $uri),
                 DBSQL::SpecEQ('a.value', $dataset)]);
                 
    @region = @{$region[0]};
	my $poly = $region[0];

	my $serializer = Data::Serializer->new();
	$poly = $serializer->deserialize($poly);
	
	for my $xy (@$poly) {
		print "$$xy[0] $$xy[1]\n";
	}
}


