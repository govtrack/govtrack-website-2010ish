#!/usr/bin/perl

use CGI;
use DBSQL;
use WmsConfig;
use WmsUploadUtils;
use WmsGeometry;

print <<EOF;
Content-type: text/plain

EOF

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

eval { LoadGeometry(); };
my $error = $@;

DBSQL::Close();

if ($error) { die "$error\n"; }

sub LoadGeometry {
	my $dataset = CGI::param('dataset');
	
	if ($dataset !~ /\S/) { print "No dataset specified.\n"; return; }
	
	if (!WmsUploadUtils::CheckAuth($dataset)) {
		print "You are not authorized to modify this dataset. Check your 'key' parameter.\n";
		return;
	}
	
	my ($id) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
	if (!defined($id)) {
		($id) = DBSQL::SelectFirst(wmsidentifiers, ['max(id)']);
		$id++;
		DBSQL::Insert(wmsidentifiers, 'id' => $id, 'value' => $dataset);
	}
	$dataset = $id;
	
	DBSQL::Delete(wmsgeometry, [DBSQL::SpecEQ(dataset, $dataset)]);

	my $data = CGI::param('geometry');
	my $ctr;
	for my $entry (split(/[\n\r]+/, $data)) {
		my $regionuri;
		my %hash;
		
		($regionuri, %hash) = WmsUploadUtils::ParseLine($entry);
		if (!defined($regionuri)) { next; }
	
		my ($region) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $regionuri)]);
		if (!defined($region)) {
			my ($id) = DBSQL::SelectFirst(wmsidentifiers, ['max(id)']);
			$id++;
			DBSQL::Insert(wmsidentifiers, 'id' => $id, 'value' => $regionuri);
			$region = $id;
		}
		
		my @points;
		my @pdata = split(/,/, $hash{points});
		while (scalar(@pdata) > 0) {
			my $long = shift(@pdata) * 1.0;
			my $lat = shift(@pdata) * 1.0;
			push @points, [$long, $lat];
		}
		
		WmsGeometry::AddRegionPolygon($region, $dataset, \@points);
		
		$ctr++;
	}
	
	# clear the cache for any stylesets that use this dataset
	my @stylesets = DBSQL::SelectVectorDistinct('wmsstyles', ['styleset'], [DBSQL::SpecEQ('dataset', $dataset)]);
	for my $styleset (@stylesets) {
		DBSQL::Delete(wmscache, [DBSQL::SpecEQ(styleset, $styleset)]);
	}
	
	print "Loaded $ctr region entries.\n";
}

