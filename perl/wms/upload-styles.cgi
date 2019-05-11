#!/usr/bin/perl

use CGI;
use DBSQL;
use WmsConfig;
use WmsUploadUtils;

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

	my $global_dataset = CGI::param('dataset');
	if ($global_dataset !~ /\S/) { undef $global_dataset; }
	
	if (!WmsUploadUtils::CheckAuth($styleset)) {
		print "You are not authorized to modify this layer. Check your 'key' parameter.\n";
		return;
	}

	DBSQL::Delete(wmsstyles, [DBSQL::SpecEQ(styleset, $styleset)]);
	
	my %datasetids;

	my $data = CGI::param('styles');
	my %seen;
	for my $entry (split(/[\n\r]+/, $data)) {
		my $regionuri;
		my %hash;
		
		($regionuri, %hash) = WmsUploadUtils::ParseLine($entry);
		if (!defined($regionuri)) { next; }
		
		my $dataset = $global_dataset;
		if ($regionuri =~ /^(.*)\@(.*)$/) {
			$dataset = $1;
			$regionuri = $2;
		}
		if (!defined($dataset)) {
			print "No dataset parameter set and \@-notation not used to specify the dataset for region $regionuri.\n";
			next;
		}
		
		my $datasetid;
		if (defined($datasetids{$dataset})) {
			$datasetid = $datasetids{$dataset};
		} else {
			($datasetid) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
			if (!defined($datasetid)) { print "No geometry is available for the data set $dataset."; next; }
			$datasetids{$dataset} = $datasetid;
		}
	
		my ($region) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $regionuri)]);
		if (!defined($region)) { print "No region named $regionuri is in the dataset $dataset.\n"; next; }
		
		$seen{$region} = 1;
		
		if (!defined($hash{radius})) { $hash{radius} = 0; }
	
		DBSQL::Insert(wmsstyles,
			styleset => $styleset,
			dataset => $datasetid,
			region => $region,
			bordercolor => $hash{bordercolor},
			borderweight => $hash{borderweight},
			fillcolor => $hash{fillcolor},
			radius => $hash{radius},
			textcolor => $hash{textcolor},
			label => $hash{label},
			font => $hash{font},
			markerdata => $hash{markerdata}
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


