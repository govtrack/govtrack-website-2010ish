#!/usr/bin/perl

use CGI;
use XML::LibXML;
use LWP::UserAgent;

print <<EOF;
Content-type: text/xml

EOF

my $UA = LWP::UserAgent->new(keep_alive => 2, timeout => 15, agent => "GovTrack.us", from => "www.govtrack.us");

my $address = CGI::param('address');
my $response = $UA->get("http://www.govtrack.us/perl/district-lookup.cgi?address=" . CGI::escape($address));
if ($response->is_success && $response->content =~ /^</) {
	my $parser = XML::LibXML->new();
	my $doc = $parser->parse_string($response->content);
	print CGI::escapeHTML($doc->toString);
	if ($doc->findvalue('congressional-district/latitude')) {
		my $state = $doc->findvalue('congressional-district/state');
		my $district = $doc->findvalue('congressional-district/district');
		$address = CGI::escapeHTML(CGI::escape($address));
		my $marker = $doc->findvalue('congressional-district/longitude') . ',' . $doc->findvalue('congressional-district/latitude');
		if ($district > 0) {
			print "<script>document.location = 'findyourreps.xpd?state=$state\&district=$district\&address=$address\&marker=$marker</script>";
		} else {
			print "<script>document.location = 'findyourreps.xpd?state=$state\&address=$address\&marker=$marker</script>";
		}
		exit(0);
	}
}

print "<div>Address not found.</div>";

