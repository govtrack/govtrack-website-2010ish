#!/usr/bin/perl

use CGI;
use DBSQL;
use WmsConfig;
use WmsUploadUtils;

require "sparql.pl";

print <<EOF;
Content-type: text/plain

EOF

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

eval { LoadStyles(); };
$error = $@;

DBSQL::Close();

if ($error) { die "$error\n"; }

sub LoadStyles {
	my $styleset = CGI::param('layer');
	if ($styleset !~ /\S/) { print "No layer name specified.\n"; return; }

	my $dataset = CGI::param('dataset');
	if ($dataset !~ /\S/) { print "No dataset name specified.\n"; return; }

	my ($datasetid) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
	if (!defined($datasetid)) { print "No geometry is available for the data set $dataset."; return; }
	
	if (!WmsUploadUtils::CheckAuth($styleset)) {
		print "You are not authorized to modify this layer. Check your 'key' parameter.\n";
		return;
	}
	
	# run query to get data
	my @data = SparqlQuery(CGI::param('endpoint'), CGI::param('query'));
	
	# compute range of values
	my $minvalue;
	my $maxvalue;
	for my $row (@data) {
		my $value = $$row{value};
		if (!defined($minvalue) || $value < $minvalue) { $minvalue = $value; }
		if (!defined($maxvalue) || $value > $maxvalue) { $maxvalue = $value; }
	}

	# clear the styleset
	DBSQL::Delete(wmsstyles, [DBSQL::SpecEQ(styleset, $styleset)]);
	
	my $ctr = 0;
	
	# add style data to database
	for my $row (@data) {
		my $regionuri = $$row{uri};
		my $value = $$row{value};
		
		my ($region) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $regionuri)]);
		if (!defined($region)) { print "No region named $regionuri is in the dataset $dataset.\n"; next; }
		
		$seen{$region} = 1;
		
		my $c = int(($value-$minvalue)/($maxvalue-$minvalue) * 255);
		my $fillcolor = "$c,$c,$c";
		
		DBSQL::Insert(wmsstyles,
			styleset => $styleset,
			dataset => $datasetid,
			region => $region,
			#bordercolor => $hash{bordercolor},
			#borderweight => $hash{borderweight},
			fillcolor => $fillcolor,
			#radius => $hash{radius},
			#textcolor => $hash{textcolor},
			#label => $hash{label},
			#font => $hash{font},
			#markerdata => $hash{markerdata}
			);
		$ctr++;
	}
	
	print "Loaded $ctr region style entries.\n";

	my @regions;
	for my $r (DBSQL::SelectVector(wmsgeometry, ['region'], [DBSQL::SpecEQ(dataset, $dataset)])) {
		if (!$seen{$r}) { push @regions, $r; }
	}
	if (scalar(@regions) > 0) {
		my @regionnames = DBSQL::SelectVector('wmsidentifiers', ['value'], [DBSQL::SpecIn('id', @regions)]);
		if (scalar(@regionnames) > 0) {
			print "No style information was set for the following regions:\n";
		}
		for my $r (@regionnames) {
			print "$r\n";
		}
	}

	DBSQL::Delete(wmscache, [DBSQL::SpecEQ(styleset, $styleset)]);
}


