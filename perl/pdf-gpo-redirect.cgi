#!/usr/bin/perl

use CGI;

$path = CGI::param('path');

if ($path !~ /(\d+)\/.*\/([a-z]+)(\d+)([a-z]+\d?)?\.(pdf|txt)/) {
	print <<EOF;
Content-type: text/plain

Invalid request.
EOF
}

$session = $1;
$billtype = $2;
$billnumber = $3;
$billstatus = $4;
$billformat = $5;

if (!$billstatus) {
	$dest = readlink("/home/govtrack/data/us/bills.text/" . $path);
	if ($dest !~ /$billtype$billnumber([a-z]+)\.$billformat/) {
		print <<EOF;
Content-type: text/plain

Information not available. Internal data problem: bad link.
EOF
	}
	$billstatus = $1;
}

if ($billformat eq 'pdf') { $billformat = 'txt.pdf'; }

$url = "http://frwebgate.access.gpo.gov/cgi-bin/getdoc.cgi?dbname=${session}_cong_bills\&docid=f:$billtype$billnumber$billstatus.$billformat";

print <<EOF;
Location: $url

EOF
