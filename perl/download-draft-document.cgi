#!/usr/bin/perl

use CGI;
use DBSQL;
use MIME::Types;

# next line required when running in mod_perl so
# it knows where we are
push @INC, "/home/govtrack/website/perl";

DBSQL::Open("govtrack", "root", undef);

my $code = CGI::param('code');
my ($id, $type, $content) = DBSQL::SelectFirst('drafts', ['id', 'contenttype', 'content'], [DBSQL::SpecEQ('code', $code)]);
if (!$type) {
	print <<EOF;
Status: 404

File not found.	
EOF
} else {
	DBSQL::Execute("UPDATE drafts SET downloads = downloads + 1 WHERE id = $id");

	my $mimetypes = MIME::Types->new;
	my ($ext) = $mimetypes->type($type)->extensions;
	print CGI::header(-type => $type, -attachment => "draft_$code.$ext", -expires => "+30d", -Content_length=>length($content));
	print "\n";
	print $content;
}

DBSQL::Close();
