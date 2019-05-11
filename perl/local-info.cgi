#!/usr/bin/perl

use CGI;
use XML::LibXML;
use LWP::UserAgent;
use DBSQL;

require "sparql.pl";

my $year = 2008;

my $ua = LWP::UserAgent->new(keep_alive => 2, timeout => 6, agent => "GovTrack.us", from => "operations@govtrack.us");
my $xmlparser = XML::LibXML->new();

sub pvsapiurl {
	my $command = shift;
	my $apikey = '97bf64e601430615124e216376730540';
	return "http://api.votesmart.org/$command?key=$apikey\&o=xml";
}

my %StateName = (
AL => 'Alabama', AK => 'Alaska', AS => 'American Samoa', AZ => 'Arizona',
AR => 'Arkansas', CA => 'California', CO => 'Colorado', CT => 'Connecticut',
DE => 'Delaware', DC => 'District of Columbia', FM => 'Federated States of Micronesia',
FL => 'Florida', GA => 'Georgia', GU => 'Guam', HI => 'Hawaii', ID => 'Idaho',
IL => 'Illinois', IN => 'Indiana', IA => 'Iowa', KS => 'Kansas', KY => 'Kentucky',
LA => 'Louisiana', ME => 'Maine', MH => 'Marshall Islands', MD => 'Maryland',
MA => 'Massachusetts', MI => 'Michigan', MN => 'Minnesota', MS => 'Mississippi',
MO => 'Missouri', MT => 'Montana', NE => 'Nebraska', NV => 'Nevada', NH => 'New Hampshire',
NJ => 'New Jersey', NM => 'New Mexico', NY => 'New York', NC => 'North Carolina',
ND => 'North Dakota', MP => 'Northern Mariana Islands', OH => 'Ohio', OK => 'Oklahoma',
OR => 'Oregon', PW => 'Palau', PA => 'Pennsylvania', PR => 'Puerto Rico', RI => 'Rhode Island',
SC => 'South Carolina', SD => 'South Dakota', TN => 'Tennessee', TX => 'Texas', UT => 'Utah',
VT => 'Vermont', VI => 'Virgin Islands', VA => 'Virginia', WA => 'Washington',
WV => 'West Virginia', WI => 'Wisconsin', WY => 'Wyoming');

my %StateTerritory;
for my $s (AS, DC, FM, GU, MH, MP, PW, PR, VI) {
	$StateTerritory{$s} = 1;
}

print "Content-type: text/html; charset=utf-8\n\n";
binmode(STDOUT, ":utf8");

$uri = CGI::param('uri');

#print $uri;

if (CGI::param('layer') eq 'states' && $uri =~ m#^http://www.rdfabout.com/rdf/usgov/geo/us/([a-z][a-z])$#) {
	# This is a state.
	$state = $1;
	$statename = $StateName{uc($state)};
	
	if ($StateTerritory{uc($state)}) {
		print "<p>$statename is a U.S. territory and does not have any senators.</p>";
	} else {
	
	do "db_open.pl";
	@pids = DBSQL::SelectVector(people_roles, ['personid'],
		["startdate <= now() and enddate >= now() and state='$state' and type='sen'"]);
	DBSQL::Close();
	
	print "<h3>${statename}'s Senators</h3>\n";
	
	if (scalar(@pids) == 0) {
		print "<p>This state's senator offices are current vacant.</p>\n";
	}
	if (scalar(@pids) == 1) {
		print "<p>One of this state's senator offices are current vacant.</p>\n";
	}
	
	for my $pid (@pids) {
		# Load info via GovTrack API.
		
		my $info = load_api('http://www.govtrack.us/congress/person_api.xpd?session=0&id=' . $pid);
		if (!$info) {
			print "<p>Information about the current senator for this state not currently available due to a technical error.</p>";
		} else {
			my $gp = posessive($info->findvalue('person/Gender'));
			print "<h4>Sen. " . $info->findvalue('person/FullName') . "</h4>\n";
			print "<table><tr valign='top'><td>";
			if (-e "/home/govtrack/data/photos/$pid.jpeg") {
				print "<img src='http://www.govtrack.us/data/photos/$pid-100px.jpeg' style='margin-right: 1em; border: 1px solid black'/>";
			}
			print "</td><td>";
			$start = $info->findvalue('person/CongressionalTerms/Term[1]/Start');
			$end = $info->findvalue('person/CongressionalTerms/Term[1]/End');
			print "<p style='margin-top: 0px'>... currently represents this state and has held this position since $start. $gp term ends in $end.</p>\n";
			print "<p>For more information, see <a href='http://www.govtrack.us/congress/person.xpd?id=$pid' target='_top'>" . $info->findvalue('person/FullName') . "'s page on GovTrack</a>.\n";
			ShowElectionInfo1($pid);
			print "</td></tr></table>";
		}
	}

	}

	if (CGI::param('quick') eq '') {
		ShowElectionInfo2($state, 'C', '');
		ShowStats();
	}


} elsif ($uri =~ m#^http://www.rdfabout.com/rdf/usgov/geo/us/([a-z][a-z])(/cd/(\d+)/(\d+))?$#) {
	# This is a state or CD.
	$state = $1;
	$session = $3;
	$district = $4;
	
	$statename = $StateName{uc($state)};
	$sessionname = ordinate($session);
	$districtname = ordinate($district);
	
	if ($district == 0) {
		$districtname = 'At-Large';
	}
	
	do "db_open.pl";
	($pid) = DBSQL::SelectFirst(people_roles, ['personid'],
		["startdate <= now() and enddate >= now() and state='$state' and district='$district' and type='rep'"]);
	DBSQL::Close();
	
	print "<h3>${statename}'s $districtname Congressional District</h3>\n";
	
	if (!$pid) {
		print "<p>This congressional seat is currently vacant.</p>\n";
	} else {
		# Load info via GovTrack API.
		
		my $info = load_api('http://www.govtrack.us/congress/person_api.xpd?session=0&id=' . $pid);
		if (!$info) {
			print "<p>Information about the current representative for this district is not currently available due to a technical error.</p>";
		} else {
			my $title = "Rep.";
			if ($info->findvalue('person/CurrentRole/Title/@Type') ne 'REP') {
				$title = $info->findvalue('person/CurrentRole/Title');
			}
			my $gp = posessive($info->findvalue('person/Gender'));
			print "<h4>$title " . $info->findvalue('person/FullName') . "</h4>\n";
			print "<table><tr valign='top'><td>";
			if (-e "/home/govtrack/data/photos/$pid.jpeg") {
				print "<img src='http://www.govtrack.us/data/photos/$pid-100px.jpeg' style='margin-right: 1em; border: 1px solid black'/>";
			}
			print "</td><td>";
			$start = $info->findvalue('person/CongressionalTerms/Term[1]/Start');
			$end = $info->findvalue('person/CongressionalTerms/Term[1]/End');
			print "<p style='margin-top: 0px'>... currently represents this district and has held this position since $start. $gp term ends in $end.</p>\n";
			print "<p>For more information, see <a href='http://www.govtrack.us/congress/person.xpd?id=$pid' target='_top'>" . $info->findvalue('person/FullName') . "'s page on GovTrack</a>.\n";
			if (CGI::param('quick') eq '') {
				ShowElectionInfo1($pid);
			}
			print "</td></tr></table>";
		}
	}

	if (CGI::param('quick') eq '') {
		ShowElectionInfo2($state, 'C', $district == 0 ? 'At-Large' : $district);
		ShowStats();
	}
		
} elsif ($uri =~ m#^http://www.rdfabout.com/rdf/usgov/geo/us/(..)/legislature/(upper|lower)/(\d+)$#) {
	# This is a state district.
	$state = $1;
	$chamber = $2;
	$district = $3;
	if (int($district) != 0) { $district = int($district); }
	$statename = $StateName{uc($state)};
	$districtname = ordinate($district);
	
	my $info = load_api(pvsapiurl('State.getState') . '&stateId=' . uc($state));
	if ($info) {
		$chambername = $info->findvalue('state/details/' . ($chamber eq 'lower' ? 'lowerLegis' : 'upperLegis'));
		#print "<pre>".escape($info->toString)."</pre>";
	}

	if (!$chambername) { $chambername = ($chamber eq 'lower' ? 'Lower' : 'Upper') . ' Legislature'; }

	print "<h3>${statename} $chambername\'s $districtname District</h3>\n";

	my $can;
	for my $officeId (@{ ($chamber eq 'lower' ? [7,8] : [9]) } ) {
		$info = load_api(pvsapiurl('Officials.getByOfficeState') . '&officeId=' . $officeId . '&stateId=' . uc($state));
		if ($info) {
			#print "<pre>".escape($info->toString)."</pre>";
			for my $c ($info->findnodes("candidateList/candidate[officeDistrictName='$district']")) {
				$can = $c;
			}
		}
	}

	if ($can) {
		my $id = $can->findvalue('candidateId');
		my $n = $can->findvalue('title') . ' ' . $can->findvalue('firstName') . " " . $can->findvalue('lastName');
		my $p = $can->findvalue('officeParties');
		print "<h4>$n</h4>\n";
		print "<p>... currently represents this district and is a member of the $p Party. More information on this person at <a href='http://www.votesmart.org/summary.php?can_id=$id' target='_top'>Project Vote Smart</a>.</p>";
	}
	
	ShowElectionInfo2($state, 'L', $district);

} else {
	print $uri;
}

#print $uri;

sub escape {
	my $x = shift;
	$x =~ s/\&/\&amp;/g;
	$x =~ s/</\&lt;/g;
	$x =~ s/>/\&gt;/g;
	return $x;
}

sub ordinate {
	my $o = $_[0];
	my $n = "$o" % 10;
	if ("$o" % 100 >= 11 && "$o" % 100 < 20) { $o .= "th"; }
	elsif ($n == 1) { $o .= "st"; }
	elsif ($n == 2) { $o .= "nd"; }
	elsif ($n == 3) { $o .= "rd"; }
	else { $o .= "th"; }
	return $o;
}

sub formatnum {
	my $num = shift;
	my @chars = split(//, $num);
	my @newchars;
	my $ct = 0;
	while (scalar(@chars)) {
		if (($ct % 3) == 0 && $ct > 0) {
			unshift @newchars, ",";
		}
		my $x = pop(@chars);
		unshift @newchars, $x;
		$ct++;
	}
	return join("", @newchars);
}

sub posessive {
	if ($_[0] eq 'M') { return "His"; }
	if ($_[0] eq 'F') { return "Her"; }
	return "Their";
}

sub load_api {
	my $url = shift;
	my $response = $ua->get($url);
    if (!$response->is_success) { return undef; }
    return $xmlparser->parse_string($response->content);
}

sub ShowStats {	
	my @results;
	eval {
	@results = SparqlQuery('http://www.rdfabout.com/sparql',
		"SELECT * WHERE {
			<$uri> <http://www.rdfabout.com/rdf/schema/census/population> ?population .
			<$uri> <http://www.rdfabout.com/rdf/schema/census/households> ?households .
			<$uri> <http://www.rdfabout.com/rdf/schema/census/details> [
				<tag:govshare.info,2005:rdf/census/details/100pct/totalPopulation> [
					<tag:govshare.info,2005:rdf/census/details/100pct/bothSexes> [
						<tag:govshare.info,2005:rdf/census/details/100pct/medianAge> ?age
					]
				] ;
				<tag:govshare.info,2005:rdf/census/details/100pct/housingUnits> [
					<tag:govshare.info,2005:rdf/census/details/100pct/rural> ?rural ;
					<tag:govshare.info,2005:rdf/census/details/100pct/urban> [ <http://www.w3.org/1999/02/22-rdf-syntax-ns#value> ?urban ]
				] ;
				<tag:govshare.info,2005:rdf/census/details/samp/population15YearsAndOverWithIncomeIn1999> [
					<tag:govshare.info,2005:rdf/census/details/samp/medianIncomeIn1999> ?income ] ;
			] .
		}");
	};
	if ($@ || !scalar(@results)) { return; }

	$pop = formatnum($results[0]{population});
	$hh = formatnum($results[0]{households});
	$urban = formatnum(int(100 * $results[0]{urban} / ($results[0]{households} + $results[0]{rural})));
	$income = formatnum($results[0]{income});
	print "<h4>Census Statistics (2000 Census)</h4>";
	print "<div>";
	print "<div>Population: $pop</div>";
	print "<div>Households: $hh</div>";
	print "<div>Median Age: $results[0]{age}</div>";
	print "<div>Urban: $urban\%</div>";
	print "<div>Median Income: \$$income</div>";
	print "</div>";
}

sub ShowElectionInfo1 {
	my $pid = shift;
	
	do "db_open.pl";
	my ($cid) = DBSQL::SelectFirst(people, ['pvsid'], ["id=$pid"]);
	DBSQL::Close();
	
	if (!$cid) { return; }

	my $info = load_api(pvsapiurl('CandidateBio.getBio') . '&candidateId=' . $cid);
	if (!$info) { return; }
	
	my $name = $info->findvalue('bio/candidate/lastName');
	
	my $running = $info->findvalue('bio/election/status');
	if ($running eq 'Running') {
		print "<p>According to Project Vote Smart, $name is running for relection in " . $info->findvalue('bio/office/nextElect') . "</p>\n";
	}
}

sub ShowElectionInfo2 {
	my $state = shift;
	my $officeTypeId = shift;
	my $district = shift;
	
	my $info = load_api(pvsapiurl('Election.getElectionByYearState') . '&year=2008&stateId=' . uc($state));
	
	($info) = $info->findnodes("elections/election[officeTypeId='$officeTypeId']");
	if (!$info) { return; }
	
	print "<h4>Election Information</h4>\n";
	for my $e ($info->findnodes('stage')) {
		my $info2 = load_api(pvsapiurl('Election.getStageCandidates') . '&electionId=' . $info->findvalue('electionId') . '&stageId=' . $e->findvalue('stageId'));
		if (!$info2) { next; }
		#print "<pre style='width: 20em; overflow: auto'>" . escape($info2->toString) . "</pre>";
		my @cans = $info2->findnodes("stageCandidates/candidate[district='$district']");
		if (scalar(@cans)) {
			print "<p>" . $e->findvalue('name') . ' Election Date: ' . $e->findvalue('electionDate') . "</p>\n";
			print "<div style='margin-left: 2em'><b>Candidates:</b>\n";
			for my $c ($info2->findnodes("stageCandidates/candidate[district='$district']")) {
				my $id = $c->findvalue('candidateId');
				my $n = $c->findvalue('firstName') . " " . $c->findvalue('lastName');
				my $p = $c->findvalue('party');
				my $st = $c->findvalue('status');
				if ($st ne 'Won') { $st = ''; } else { $st = " -- $st"; }
				print "<div><a href='http://www.votesmart.org/summary.php?can_id=$id' target='_top'>$n</a> ($p) $st</div>";
			}
			print "</div>\n";
		}
		
	}
	
	#print "<pre style='width: 20em; overflow: auto'>" . escape($info->toString) . "</pre>";
	
}
