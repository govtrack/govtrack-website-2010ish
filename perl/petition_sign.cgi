#!/usr/bin/perl

use CGI;
use DBSQL;

# next line required when running in mod_perl so
# it knows where we are
push @INC, "/home/govtrack/website/perl";

if (1) {
	print <<EOF;
Content-type: text/html

This petition is now closed.
EOF

	DBSQL::Close();
	exit(0);
}

do "db_open.pl";

my $name = CGI::param('name');
$name =~ s/^\s+//;
$name =~ s/s+$//;
if ($name eq '') {
	print <<EOF;
Content-type: text/html

You didn't give your name!
EOF

	DBSQL::Close();
	exit(0);
}

DBSQL::Insert(petition,
	topic => CGI::param('topic'),
	name => $name,
	address => CGI::param('address1'),
	city => CGI::param('city'),
	state => CGI::param('state'),
	zip => CGI::param('zip'),
	congdist => CGI::param('cd'),
	participation => CGI::param('participation'),
	privacy => CGI::param('privacy'),
	);

	print <<EOF;
Content-type: text/html

Thanks! Your signature has been added.
EOF

DBSQL::Close();
