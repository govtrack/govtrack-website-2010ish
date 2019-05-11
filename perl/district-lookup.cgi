#!/usr/bin/perl

use CGI;
use Math::Geometry::Planar;
use XML::LibXML;
use LWP::UserAgent;
use Data::Serializer;
use DBSQL;

# next line required when running in mod_perl so
# it knows where we are
push @INC, "/home/govtrack/website/perl";

my $session = 110;

my $UA = LWP::UserAgent->new(keep_alive => 2, timeout => 15, agent => "GovTrack.us Congressional District Lookup REST API", from => "www.govtrack.us");

my $lat = CGI::param('lat');
my $long = CGI::param('long');
my $zip = CGI::param('zipcode');

my $ipaddr = CGI::param('ip');
if (0 && $ipaddr =~ /^\d+\.\d+\.\d+\.\d+$/) {
	my $response = $UA->get("http://api.hostip.info/get_html.php?ip=$ipaddr\&position=true");
    if ($response->is_success) {
    	$response->content =~ /Latitude: (.*)/; $lat = $1;
    	$response->content =~ /Longitude: (.*)/; $long = $1;
	} else {
		$error = $response->content;
	}
}
if ($ipaddr =~ /^\d+\.\d+\.\d+\.\d+$/) {
	my $response = $UA->get("http://ipinfodb.com/ip_query.php?ip=" . $ipaddr);
    if ($response->is_success) {
    	$response->content =~ m|<Latitude>(.*)</Latitude>|; $lat = $1;
    	$response->content =~ m|<Longitude>(.*)</Longitude>|; $long = $1;
	} else {
		$error = $response->content;
	}
}
	
my $address = CGI::param('address');
if ($address ne "") {
	my $response = $UA->get("http://rpc.geocoder.us/service/rest?address=$address");
    if ($response->is_success && $response->content =~ /^</) {
		my $parser = XML::LibXML->new();
		my $doc = $parser->parse_string($response->content);
		$lat = $doc->findvalue('rdf:RDF/geo:Point/geo:lat');
		$long = $doc->findvalue('rdf:RDF/geo:Point/geo:long');
	} else {
		$error = $response->content;
	}
}

my @districts;

do "db_open.pl";

if ($zip =~ /^(\d\d\d\d\d)(-(\d\d\d\d))?$/) {
	$zip = "$1$3";
	
	# try first searching for records that have this zipcode as a prefix
	# (which includes an exact match for this zipcode)
	@districts = DBSQL::Select(DISTINCT, zipcodes, ["state", "district"],
		[DBSQL::SpecStartsWith('zip9', $zip)]);
		
	# if that fails, look for a prefix of this zipcode, which will be
	# the first entry preceding this zip code lexicographically
	if (scalar(@districts) == 0) {
		@districts = DBSQL::Select(zipcodes, ["state", "district", "zip9"],
			[DBSQL::SpecLT('zip9', $zip)], "ORDER BY zip9 DESC LIMIT 1");
		# check that the preceding entry was actually a prefix
		my $z = @{$districts[0]}[2];
		if (substr($zip, 0, length($z)) ne $z) {
			@districts = ();
		}
	}

} elsif ($lat && $long) {
	my $response = $UA->get("http://www.govtrack.us/perl/wms/get-region.cgi?layer=cd-110\&lat=$lat\&long=$long");
    if ($response->is_success) {
    	for my $line (split(/\n/, $response->content)) {
    		my ($uri, $long, $lat, $type) = split(/\t/, $line);
    		if ($type eq 'district') {
	    		$uri =~ /\/us\/(..)(\/cd\/110\/(\d+))?/;
	    		my ($s, $d) = ($1, $3);
	    		if (!$d) { $d = 0; }
	    		push @districts, [uc($s), $d];
	    	}
    	}
    }

}

if (scalar(@districts) == 0) {
	print <<EOF;
Status: 500
Content-type: text/plain

No latitude/longitude, zipcode, street address, or IP address specified or address resolution failed.

$error
EOF

} elsif (scalar(@districts) == 1) {
	$state = $districts[0][0];
	$dist = $districts[0][1];

	print <<EOF;
Content-type: text/xml

<congressional-district>
	<session>$session</session>
EOF

	if ($lat && $long) {
		print <<EOF;
	<latitude>$lat</latitude>
	<longitude>$long</longitude>
EOF
	}

	print <<EOF;
	<state>$state</state>
	<district>$dist</district>
EOF

	printreps($state, $dist, "\t");

	print <<EOF;
</congressional-district>
EOF

} elsif (scalar(@districts) > 1) {
	print <<EOF;
Content-type: text/xml

<congressional-districts>
EOF
	foreach my $d (@districts) {
		$state = $$d[0];
		$dist = $$d[1];
		print <<EOF;
	<congressional-district>
		<session>$session</session>
		<state>$state</state>
		<district>$dist</district>
EOF
		printreps($state, $dist, "\t\t");
		print <<EOF;
	</congressional-district>
EOF
	}
	print <<EOF;
</congressional-districts>
EOF
}

DBSQL::Close();

sub printreps {
	my ($state, $dist, $indent) = @_;
	for my $id (getreps('sen', $state, undef)) {
		print "$indent<member type=\"senator\" id=\"$id\"/>\n";
	}
	for my $id (getreps('rep', $state, $dist)) {
		print "$indent<member type=\"representative\" id=\"$id\"/>\n";
	}
}

sub getreps {
	my ($type, $state, $dist) = @_;
	return DBSQL::SelectVector(people_roles, ['personid'],
		[DBSQL::SpecEQ(type, $type), DBSQL::SpecEQ(state, $state),
			defined($dist) ? DBSQL::SpecEQ(district, $dist) : 1,
			DBSQL::SpecLE(startdate, DBSQL::MakeDBDate(time)),
			DBSQL::SpecGE(enddate, DBSQL::MakeDBDate(time))]);
}
