#!/usr/bin/perl

use CGI;
use DBSQL;

# next line required when running in mod_perl so
# it knows where we are
push @INC, "/home/govtrack/website/perl";

do "db_open.pl";

my $ref = CGI::param('ref');
my $context_before = CGI::param('context_before');

$ref =~ s/_+$//g;

my $seq;
while ($ref) {
	($seq) = DBSQL::SelectFirst(usc_flattened, ["seq"], [DBSQL::SpecEQ('ref', $ref)]);
	if ($seq) { last; }
	
	# If we can't find the exact paragraph, back off to
	# a higher-level paragraph. In that case, though, also
	# back off the number of lines of context.
	if ($ref =~ s/_[^_]+$//) { $context_before = int($context_before/2); next; }
	
	last;
}

if ($seq) {
	print "Content-type: text/html\n\n";
	print <<EOF;
<html>
<head>
</head>
<body>
<div style="float: right; margin-left: 1em; margin-top: 3px;"><a href="http://www.law.cornell.edu/"><img src="/media/lii_logo_bug_sm_red.gif" border="0"/></a></div>
<div style="font-weight: bold; font-size: 130%; margin: 5px 0px 5px 0px">The United States Code</div>
<div style="border-bottom: 1px solid black; padding-bottom: 2px; margin-bottom: 1em; font-size: 95%;">Excerpt from the Cornell University Legal Information Institute. Click the link in the bill text to read more.</div>
<div style="font-family: Verdana, arial, helvetica, sans-serif; font-size: 10pt">
EOF
	my $start = $seq - $context_before;
	my $end = $seq + CGI::param('context_after');
	
	# If there is context before this point, display the context too.
	while (1) {
		my ($iscontext) = DBSQL::SelectFirst(usc_flattened, ["iscontext"], [DBSQL::SpecEQ('seq', $start-1)]);
		if (!defined($iscontext) || $iscontext == 0) { last; }
		$start--;
	}
	
	my @graphs = DBSQL::Select(usc_flattened, ["indent", "text", "ref", "iscontext"], [DBSQL::SpecGE('seq', $start), DBSQL::SpecLE('seq', $end)]);
	
	my %seen;
	
	for my $g (@graphs) {
		my $in = 2*$$g[0];
		my $h = 'margin-top: .5em; margin-bottom: .5em; ';
		if ($ref && $$g[2] eq $ref) { $h .= 'background-color: #FFE; font-size: 105%;' }
		if ($$g[3]) { $h = 'color: #666'; }
		if ($$g[3] && $seen{$$g[1]}) { next; } # don't duplicate context for the same thing
		print "<div ref=\"$$g[2]\" style=\"margin-left: ${in}em; $h\">$$g[1]</div>\n";
		if ($$g[3]) { $seen{$$g[1]} = 1; }
	}
	print <<EOF;
</div>
</body>
</html>
EOF
} else {
	print "Content-type: text/xml\n\n";
	print "<html><div>This section of the U.S. Code was not found. It may be newly codified law or a reference to a section that has been repealed.</div></html>\n";
}

DBSQL::Close();
