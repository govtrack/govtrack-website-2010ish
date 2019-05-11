#!/usr/bin/perl

use CGI;
use DBSQL;

# next line required when running in mod_perl so
# it knows where we are
push @INC, "/home/govtrack/website/perl";

do "db_open.pl";

print <<EOF;
Content-type: text/html

EOF

my @ret = DBSQL::Select(HASH, petition, ['date', 'name', 'address', 'city', 'state', 'congdist', 'participation', 'privacy'], [DBSQL::SpecEQ('topic', CGI::param('topic'))], "ORDER BY date");

my $n = scalar(@ret);

if ($n == 0) {
	print <<EOF;
<p><b>You'll be the first to sign!</b></p>
EOF
} elsif ($n == 1) {
	print <<EOF;
<p><b>One signature so far!</b></p>
EOF
} else {
	print <<EOF;
<p><b>$n signatures so far!</b></p>
EOF
}

my $private = 0;

for my $r (@ret) {
	$$r{state} = uc($$r{state});
	
	if ($$r{'privacy'} eq 'partial') {
		print <<EOF;
<p>$$r{name} -- $$r{state}-$$r{congdist} ($$r{date})</p>
EOF
	}
	
	if ($$r{'privacy'} eq 'none') {
		print <<EOF;
<div style="margin-top: 1em">$$r{name} ($$r{date})</div><div>$$r{city}, $$r{state} --- district $$r{congdist}</div>
EOF
	}
	
	if ($$r{'privacy'} eq 'full') {
		$private++;
	}
}

if ($private > 0) {
	print <<EOF;
<p><b>And $private more private signatures.</b></p>
EOF
}

DBSQL::Close();
