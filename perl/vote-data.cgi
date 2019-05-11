#!/usr/bin/perl

use CGI;
use Text::CSV_XS;
use DBSQL;
use XML::LibXML;

$xmlparser = XML::LibXML->new();

do "db_open.pl";

my @fields;
my @records;

my $timespec = "1";
if (CGI::param('year')) {
	my $year = int(CGI::param('year'));
	$timespec = "votes.date >= '$year-01-01' AND votes.date <= '$year-12-31'";
} elsif (CGI::param('session')) {
	my $year1 = int(CGI::param('session'))*2+1787;
	my $year2 = $year1 + 1;
	$timespec = "votes.date >= '$year1-01-01' AND votes.date <= '$year2-12-31'";
}

if (CGI::param('votes') ne '1') {
	if (CGI::param('subject') eq '') {
		my @billinfocols;
		if (CGI::param('crssummary')) {
			@billinfocols = ('billsession', 'billtype', 'billnumber');
		}

		push @records, ["voteid", "date", "description", "result", @billinfocols];
		push @records, DBSQL::Select(votes, [id, date, description, result, @billinfocols],
			[$timespec,
			 (CGI::param('chamber') ? DBSQL::SpecStartsWith("id", CGI::param('chamber')) : "1"),
			 ],
			"ORDER BY date");

		if (CGI::param('crssummary')) {
			push @{$records[0]}, "crssummary";
			for (my $i = 1; $i < scalar(@records); $i++) {
				my $billsession = $records[$i]->[4];
				my $billtype = $records[$i]->[5];
				my $billnumber = $records[$i]->[6];
				if (!$billsession) {
					push @{$records[$i]}, "";
					next;
				}
				my $doc = $xmlparser->parse_file("/home/govtrack/data/us/$billsession/bills/$billtype$billnumber.xml");
				my $sum = $doc->findvalue('bill/summary');
				$sum =~ s/^\s*(.{0,500}\S).*/$1/s;
				push @{$records[$i]}, $sum;
			}
		}

	} else {
		push @records, ["voteid", "date", "description", "result", "billtype", "billnumber"];
		push @records, DBSQL::Select(
			"votes INNER JOIN billindex on votes.billsession=billindex.session and votes.billtype=billindex.type and votes.billnumber=billindex.number",
			[id, session, date, description, result, type, number],
			[$timespec,
			 (CGI::param('session') ? DBSQL::SpecEQ('session', CGI::param('session')) : 1),
			 (CGI::param('chamber') ? DBSQL::SpecStartsWith('id', CGI::param('chamber')) : 1),
			 DBSQL::SpecEQ('idx', 'crs'),
			 DBSQL::SpecEQ('value', CGI::param('subject'))],
			"ORDER BY date");
	}
} else {
	my @votes;
	if (CGI::param('subject') eq '') {
		@votes = DBSQL::Select(
			"votes LEFT JOIN people_votes ON id=voteid",
			[voteid, personid, vote, displayas],
			[$timespec,
			 (CGI::param('chamber') ? DBSQL::SpecStartsWith('voteid', CGI::param('chamber')) : 1),
			 ],
			"ORDER BY votes.date, personid");
	} else {
		@votes = DBSQL::Select(
			"votes LEFT JOIN people_votes ON id=voteid INNER JOIN billindex on votes.billsession=billindex.session and votes.billtype=billindex.type and votes.billnumber=billindex.number",
			[voteid, personid, vote, displayas],
			[$timespec,
			 (CGI::param('session') ? DBSQL::SpecEQ('session', CGI::param('session')) : 1),
			 (CGI::param('chamber') ? DBSQL::SpecStartsWith('voteid', CGI::param('chamber')) : 1),
			 DBSQL::SpecEQ('idx', 'crs'),
			 DBSQL::SpecEQ('value', CGI::param('subject'))],
			"ORDER BY votes.date, personid");
	}
	
	my $rc = 1;
	my $cc = 1;
	my %rowmap;
	my %colmap;
	push @records, ['voteid'];
	for my $rec (@votes) {
		my $r = $rowmap{$$rec[0]};
		if (!$r) {
			$rowmap{$$rec[0]} = $rc;
			$r = $rc;
			$rc++;
			$records[$r] = [];
			$records[$r][0] = $$rec[0];
		}

		my $c = $colmap{$$rec[1]};
		if (!$c) {
			$colmap{$$rec[1]} = $cc;
			$c = $cc;
			$cc++;
			$records[0][$c] = $$rec[1];
		}
		
		$records[$r][$c] = $$rec[2];
	}
	
	for my $rec (@records) {
		if (scalar(@$rec) < $cc) {
			$$rec[$cc] = undef;
		}
	}
}

DBSQL::Close();

binmode(STDOUT, ":utf8");

print <<EOF;
Content-type: text/plain
Content-Disposition: inline; filename=vote-data.csv

EOF
my $csv = Text::CSV_XS->new ();

for my $r (@records) {
	$csv->combine(@$r);
	print $csv->string() . "\n";
}
