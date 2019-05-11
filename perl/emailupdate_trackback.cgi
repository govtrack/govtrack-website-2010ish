#!/usr/bin/perl

use CGI;
use DBSQL;

# next line required when running in mod_perl so
# it knows where we are
push @INC, "/home/govtrack/website/perl";

do "db_open.pl";

DBSQL::Update(users,
	[DBSQL::SpecEQ('id', CGI::param('userid')),
	 DBSQL::SpecEQ('md5(email)', CGI::param('emailhash'))],
	emailupdate_trackback => DBSQL::MakeDBDate(time));

print <<EOF;
Content-type: text/plain

// Nothing here.
alert(0);
EOF

DBSQL::Close()
