#!/usr/bin/perl

use CGI;
use IO::Scalar;
use File::MimeInfo::Magic;
use LWP::UserAgent;

push @INC, "/home/govtrack/website/perl";
use DBSQL;

require "/home/govtrack/scripts/upload/util.pl";

my $UA = LWP::UserAgent->new(keep_alive => 2, timeout => 15, agent => "GovTrack.us Congressional District Lookup REST API", from => "www.govtrack.us");

my $title = getfield('title');
my $description = getfield('description', optional => 1);
my $pubdate = getfield('pubdate');
if ($pubdate eq 'now') { $pubdate = DBSQL::MakeDBDate(time); }
my $author = getfield('author', optional => 1);
my $committee = getfield('committee', optional => 1);
my $doctype = getfield('doctype');
my $billsession = getfield('billsession', optional => 1);
my $billtype = getfield('billtype', optional => 1);
my $billnumber = getfield('billnumber', optional => 1);
my $url = getfield('fileurl', optional => 1);
my $password = getfield('password');

my $submitter;
if ($password ne "admin1234") {
	badfield("Invalid password.");
}
$submitter = 'admin';

my $docfileclientname = getfield('docfile', optional => 1);

if (!$docfileclientname && !$url) {
	print <<EOF;
Content-type: text/html

<html>
<head><title>Upload Step 2</title</head>
<body>
	<p>Okay, great, now select your file...</p>
	<form method="post" enctype="multipart/form-data">
EOF

	print CGI::hidden('title', $title);
	print CGI::hidden('description', $description);
	print CGI::hidden('pubdate', $pubdate);
	print CGI::hidden('author', $author);
	print CGI::hidden('committee', $committee);
	print CGI::hidden('doctype', $doctype);
	print CGI::hidden('billsession', $billsession);
	print CGI::hidden('billtype', $billtype);
	print CGI::hidden('billnumber', $billnumber);
	print CGI::hidden('fileurl', $url);
	print CGI::hidden('password', $password);

	print <<EOF;
		<input type="file" name="docfile"/>
		<p><input type="submit" value="Upload File" id="submit" onclick="document.getElementById('submit').disabled=1"/></p>
		<p>After you click Upload File, please be patient until the upload completes.</p>
	</form>
</body>
</html>
EOF
	exit(0);
}

my $contenttype;
my $content;

if (!$docfileclientname) {
	# URL specified
	my $response = $UA->get($url);
	if (!$response->is_success) {
		badfield("URL is not valid or site is not responding.");
	}
	if ($response->header('content-type') eq 'text/html') {
		badfield("The URL is for a web page, not a document.");
	}
	$contenttype = $response->header('content-type');
	$content = $response->content;
} else {
	# PDF is uploaded to us via POST.
	$contenttype = CGI::uploadInfo(getfield('docfile'))->{'Content-Type'};
	$content = '';
	$fh = CGI::upload('docfile');
	while (<$fh>) { $content .= $_; }
	
	if ($contenttype eq 'application/octet-stream') {
		$contenttype = File::MimeInfo::Magic::mimetype(new IO::Scalar \$content);
		if (!$contenttype) {
			$contenttype = File::MimeInfo::Magic::globs(getfield('docfile'));
			if (!$contenttype) {
				$contenttype = 'application/octet-stream';
			}
		}
	}
}
	
my $bill = "$billtype$billsession-$billnumber";
if ($bill eq '-') { undef $bill; }

my $code = "";
foreach (1..12) { $code .= chr(int(rand(26)) + ord('a')); }
    
DBSQL::Open("govtrack", "root", undef);
my $id = DBSQL::Insert(drafts,
	title => $title,
	description => $description,
	pubdate => $pubdate,
	submitdate => DBSQL::MakeDBDate(time),
	author => $author,
	committee => $committee,
	doctype => $doctype,
	bill => $bill,
	contenttype => $contenttype,
	content => $content,
	url => $url,
	code => $code,
	status => 'verified',
	submitmode => 'web',
	submitter => $submitter
	);

eval {
	PostProcess($id);
};

DBSQL::Close();

print <<EOF;
Location: http://www.govtrack.us/drafts/file/$code

EOF

sub getfield {
	my $n = shift;
	my %opts = @_;
	my $x = CGI::param($n);
	$x =~ s/^\s+//;
	$x =~ s/\s+$//;
	if (!$opts{optional} && $x eq "") { badfield("You didn't enter anything in the $n field."); }
	return $x;
}

sub badfield {
	print "Content-type: text/html\n\n";
	print "<p>$_[0]</p>";
	print "<p>Please click the back button on your browser and try again. Thanks!</p>";
	exit(0);
}
