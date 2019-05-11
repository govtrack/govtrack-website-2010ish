#!/usr/bin/perl

# This API lists the regions in a data set or style set. It can also
# be used to prepare an upload template for upload-styles.cgi.

use CGI;
use DBSQL;
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
	my @regions;
	
	my @fields;
	my @fieldnames;
	my @fieldtypes;
	
	for my $f (split(/,/, CGI::param('fields'))) {
		if ($f eq 'coord') {
			push @fields, 'innerpt_long', 'innerpt_lat';
			push @fieldnames, 'long', 'lat';
			push @fieldtypes, 'numeric', 'numeric';
		}
		if ($f eq 'area') {
			push @fields, 'area';
			push @fieldnames, 'area';
			push @fieldtypes, 'numeric';
		}
		if ($f eq 'markerdata') {
			push @fields, 'markerdata';
			push @fieldnames, 'markerdata';
			push @fieldtypes, 'string';
		}
	}
	
	my $urispec = '1';
	if (CGI::param('uri') && CGI::param('match') ne 'prefix') {
		$urispec = DBSQL::SpecEQ('wmsidentifiers.value', CGI::param('uri'));
	}
	if (CGI::param('uri') && CGI::param('match') eq 'prefix') {
		$urispec = DBSQL::SpecStartsWith('wmsidentifiers.value', CGI::param('uri'));
	}
	
	if (CGI::param('layer')) {
		my $styleset = CGI::param('layer');
		if ($styleset !~ /\S/) { print "No layer name specified.\n"; return; }
		
		@regions = DBSQL::Select(
			"wmsstyles LEFT JOIN wmsidentifiers ON wmsstyles.region=wmsidentifiers.id",
			["wmsidentifiers.value"],
           	[DBSQL::SpecEQ('wmsstyles.styleset', $styleset),
           	 $urispec],
           	 "ORDER BY wmsidentifiers.value, area DESC");

	} elsif (CGI::param('dataset')) {
		my $dataset = CGI::param('dataset');
		if ($dataset !~ /\S/) { print "No data set specified.\n"; return; }

		($dataset) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
		if (!defined($dataset)) { print "No geometry is available for this data set.\n"; return; }
    
		@regions = DBSQL::Select(DISTINCT,
			"wmsgeometry LEFT JOIN wmsidentifiers ON wmsgeometry.region=wmsidentifiers.id",
			["wmsidentifiers.value", @fields],
            [DBSQL::SpecEQ('wmsgeometry.dataset', $dataset),
           	 $urispec],
           	 "ORDER BY wmsidentifiers.value, area DESC");
             #"NOT EXISTS (SELECT * FROM wmsgeometry as b WHERE wmsgeometry.region=b.region and wmsgeometry.area < b.area)"]);
	}
	
	@regions = sort({ $$a[0] cmp $$b[0]} @regions);
	
	if (CGI::param('header') eq '1') {
		unshift @regions, ["region URI", @fieldnames];
	}
	
	WmsRestOutput::PrintResults(\@regions, ['string', @fieldtypes], ['URI', @fieldnames]);
}


