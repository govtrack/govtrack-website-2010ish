#!/usr/bin/perl

use DBSQL;

for my $arg (@ARGV) { $ACTIONS{$arg} = 1; }

DBSQL::Open('govtrack', 'root', '');

if ($ACTIONS{TABLE}) {
	DBSQL::Execute("DROP TABLE wmsstyles;");
	DBSQL::Execute("CREATE TABLE wmsstyles (styleset VARCHAR(20) NOT NULL, dataset INT NOT NULL, region INT NOT NULL, bordercolor TEXT, borderweight INT, fillcolor TEXT, radius FLOAT, textcolor TEXT, label TEXT, font TEXT, markerdata TEXT);");
	DBSQL::Execute("CREATE UNIQUE INDEX reg ON wmsstyles (styleset(20), dataset, region);");
}

SimpleColoring('states', 'http://www.rdfabout.com/rdf/usgov/us/states') if ($ACTIONS{SIMPLE_STATES});
SimpleColoring('cd-110', 'http://www.rdfabout.com/rdf/usgov/congress/house/110', undef, 'CD1') if ($ACTIONS{SIMPLE_CD});
SimpleColoring('cd-110-outlines', undef, 'http://www.rdfabout.com/rdf/usgov/congress/house/110', 'CD2') if ($ACTIONS{SIMPLE_CD});
SimpleColoring('counties', 'http://www.rdfabout.com/rdf/usgov/us/counties', 'http://www.rdfabout.com/rdf/usgov/us/states') if ($ACTIONS{SIMPLE_COUNTIES});
SimpleColoring('countysubs', 'http://www.rdfabout.com/rdf/usgov/us/countysubs', 'http://www.rdfabout.com/rdf/usgov/us/states') if ($ACTIONS{SIMPLE_COUNTYSUBS});
SimpleColoring('cdps', 'http://www.rdfabout.com/rdf/usgov/us/cdps', 'http://www.rdfabout.com/rdf/usgov/us/states') if ($ACTIONS{SIMPLE_CDPS});
SimpleColoring('zipcodes', 'http://www.rdfabout.com/rdf/usgov/us/zctas', 'http://www.rdfabout.com/rdf/usgov/us/states') if ($ACTIONS{SIMPLE_ZIPCODE});
CDIncome() if ($ACTIONS{CD_INCOME});
CDIncomeParty() if ($ACTIONS{CD_INCOME_PARTY});
CDLanguageParty() if ($ACTIONS{CD_LANGUAGE_PARTY});
CDSpectrum() if ($ACTIONS{CD_SPECTRUM});
ContributionsToHoltByZipCode() if ($ACTIONS{ZIP_CONTRIBUTIONS});
SimpleColoring('sldl', 'http://www.rdfabout.com/rdf/usgov/us/sld/lower') if ($ACTIONS{SIMPLE_SLDL});
SimpleColoring('sldu', 'http://www.rdfabout.com/rdf/usgov/us/sld/upper') if ($ACTIONS{SIMPLE_SLDU});

DBSQL::Close();

sub SimpleColoring {
	my ($styleset, $dataset, $borderdataset, $special) = @_;

	DBSQL::Delete(wmsstyles, [DBSQL::SpecEQ(styleset, $styleset)]);
	if ($dataset) { SimpleColoring2($styleset, $dataset, 'colors', $special); }
	if ($borderdataset) { SimpleColoring2($styleset, $borderdataset, 'border', $special); }
	DBSQL::Delete(wmscache, [DBSQL::SpecEQ(styleset, $styleset)]);
}
	
sub SimpleColoring2 {
	my ($styleset, $dataset, $mode, $special) = @_;
	
	my @Colors = (
		'255,51,102', '255,255,0', '153,51,255', '102,255,0',
		'0,51,102', '255,0,204', '255,102,0', '0,102,102',
		'153,0,153', '204,102,102', '0,255,255', '153,153,102');
	
	($dataset) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
	if (!defined($dataset)) { die "No geometry is available for this data set."; }
	
	# Get a list of regions in this data set.
	my @regions = DBSQL::Select("wmsgeometry LEFT JOIN wmsidentifiers ON region=id",
		['region', 'value', 'long_min', 'long_max', 'lat_min', 'lat_max'], [DBSQL::SpecEQ(dataset, $dataset)]);
		
	# Remember which regions we've seen (since we may see them more
	# than once) and which colors we assigned (so we don't give the
	# same color to neighboring regions).
	my %color;
	foreach my $region (@regions) {
		if (defined($color{$$region[0]})) { next; }

		# Choose a color index not in use by a neighboring region
		
		# Look at which colors are in use in the bounding box.
		my @neighbors = DBSQL::SelectVector("wmsgeometry", ['region'],
			[DBSQL::SpecGE(long_max, $$region[2] - ($$region[3] - $$region[2])/8),
			 DBSQL::SpecLE(long_min, $$region[3] + ($$region[3] - $$region[2])/8),
			 DBSQL::SpecGE(lat_max, $$region[4] - ($$region[5] - $$region[4])/8),
			 DBSQL::SpecLE(lat_min, $$region[5] + ($$region[5] - $$region[4])/8),
			 DBSQL::SpecEQ(dataset, $dataset)]);
		my %inuse;
		for my $n (@neighbors) {
			if (defined($color{$n})) { $inuse{$color{$n}} = 1; }
		}
		
		# choose an unused color index for this region
		my $color;
		for (my $i = 0; $i < scalar(@Colors); $i++) {
			if (rand() < .33) { next; } # randomly skip colors for variety
			if (!$inuse{$i}) { $color = $i; last; }
		}
		if (!defined($color)) {
			warn "Not enough colors available.";
			$color = int(rand() * scalar(@Colors));
		}
		
		# mark that we've done this region with this color index

		$color{$$region[0]} = $color;

		my $fillcolor = $Colors[$color];
		
		my $label = $$region[1];
		$label =~ s/^.*\///;
		
		my $labelfill;
		my $textcolor = "0,0,0";
		
		if ($special eq 'CD1') { undef $label; }
		if ($special eq 'CD2') {
			if ($$region[1] =~ m|/(..)/cd/110/(\d+)|) {
				$label = uc($1) . $2;
				$labelfill = '0,0,0';
				$textcolor = '200,200,200';
			} else {
				undef $label;
			}
		}
		
		my $markerdata;
		if ($special eq 'CD1' || $special eq 'CD2') {
			# GovTrack now relies on this in a few places!!
			if ($mode eq 'border') { $markerdata = 'state'; }
			else { $markerdata = 'district'; }
		}
		
		DBSQL::Insert(wmsstyles,
			styleset => $styleset,
			dataset => $dataset,
			region => $$region[0],
			bordercolor => ($mode eq 'border' ? "0,0,80" : "0,0,255"),
			borderweight => ($mode eq 'border' ? "3" : "2"),
			fillcolor => ($mode eq 'border' ? undef : $fillcolor),
			textcolor => $textcolor,
			textbackfill => $labelfill,
			label => (($mode eq 'border' && $special ne "CD2") ? undef : $label),
			markerdata => $markerdata,
			);
	}
}

sub CDIncome {
	my $styleset = 'cd-income';

	my $dataset = 'http://www.rdfabout.com/rdf/usgov/congress/house/110';
	($dataset) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
	if (!defined($dataset)) { die "No geometry is available for this data set."; }

	DBSQL::Delete(wmsstyles, [DBSQL::SpecEQ(styleset, $styleset)]);

	my @regions = DBSQL::Select("wmsidentifiers, wmsgeometry", ['id', 'value'], [DBSQL::SpecEQ(dataset, $dataset), "id=region"]);
	
	my @regions2;
	my %ids;
	for my $x (@regions) {
		if (defined($ids{$$x[1]})) { next; }
		$ids{$$x[1]} = $$x[0];
		push @regions2, "\$district = <$$x[1]>";
	}
	my $filter = "FILTER(" . join(" || ", @regions2) . ")";

		$query = <<EOF;
PREFIX census: <http://www.rdfabout.com/rdf/schema/census/>
PREFIX samp: <tag:govshare.info,2005:rdf/census/details/samp/>
SELECT ?district ?value WHERE {
   ?district census:details [
      samp:totalPopulation [
         samp:perCapitaIncomeIn1999 ?value
         ]
      ]
   $filter
}
EOF

	require "/home/rdfabout/census/sparql.pl";
	my @bindings = SparqlQuery("http://www.rdfabout.com/sparql", $query);
	
	my %values;
	my $maxvalue;
	foreach my $binding (@bindings) {
		my $region = $$binding{district};
		my $value = $$binding{value};
		$values{$region} = $value;
		if ($value > $maxvalue) { $maxvalue = $value; }
	}

	$maxvalue *= .75;

	foreach my $region (keys(%values)) {
		my $value = $values{$region};
	
		my @clr = (0,0,255);
		if ($value/$maxvalue > .5) { $clr[0] = int(($value / $maxvalue - .5) * 2 * 255); }
		if ($value/$maxvalue <= .5) { $clr[2] = int(($value / $maxvalue) * 2 * 255); }
		my $fillcolor = join(",", @clr);
		
		DBSQL::Insert(wmsstyles,
			styleset => $styleset,
			dataset => $dataset,
			region => $ids{$region},
			bordercolor => "255,255,50",
			borderweight => "1",
			fillcolor => $fillcolor,
			textcolor => "255,255,50",
			label => "$value"
			);
	}
}


sub CDIncomeParty {
	my $styleset = 'cd-income-party';

	my $dataset = 'http://www.rdfabout.com/rdf/usgov/congress/house/110';
	($dataset) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
	if (!defined($dataset)) { die "No geometry is available for this data set."; }

	DBSQL::Delete(wmsstyles, [DBSQL::SpecEQ(styleset, $styleset)]);

	my @regions = DBSQL::Select("wmsidentifiers, wmsgeometry", ['id', 'value'], [DBSQL::SpecEQ(dataset, $dataset), "id=region"]);
	
	my @regions2;
	my %ids;
	for my $x (@regions) {
		if (defined($ids{$$x[1]})) { next; }
		$ids{$$x[1]} = $$x[0];
		push @regions2, "\$district = <$$x[1]>";
	}
	my $filter = "FILTER(" . join(" || ", @regions2) . ")";

		$query = <<EOF;
PREFIX pol: <http://www.rdfabout.com/rdf/schema/politico/>
PREFIX usgov: <http://www.rdfabout.com/rdf/schema/usgovt/>
PREFIX census: <http://www.rdfabout.com/rdf/schema/census/>
PREFIX samp: <tag:govshare.info,2005:rdf/census/details/samp/>
SELECT ?district ?income ?party WHERE {
   ?district census:details [
      samp:totalPopulation [
         samp:perCapitaIncomeIn1999 ?income
         ]
      ].
   ?pol pol:hasRole [ pol:forOffice [ pol:represents ?district; usgov:congress "110" ] ] .
   ?pol usgov:party ?party .
   $filter
}
EOF

	require "/home/rdfabout/census/sparql.pl";
	my @bindings = SparqlQuery("http://www.rdfabout.com/sparql", $query);
	
	my %values;
	my $maxincome;
	foreach my $binding (@bindings) {
		my $region = $$binding{district};
		my $income = $$binding{income};
		my $party = $$binding{party};

		$values{$region}{income} = $income;
		$values{$region}{party} = $party;
		
		if ($income > $maxincome) { $maxincome = $income; }
	}

	$maxincome *= .5;

	foreach my $region (keys(%values)) {
		my $income = $values{$region}{income};
		my $party = $values{$region}{party};
	
		my $clr = int(sqrt(($income-$maxincome/10)/$maxincome) * 255);
		if ($clr > 255) { $clr = 255; }
		$fillcolor = "$clr,$clr,$clr";
		
		my $bdrclr = "255,255,255";
		if ($party eq 'Democrat') { $fillcolor = "0,0,$clr"; }
		if ($party eq 'Republican') { $fillcolor = "$clr,0,0"; }
		
		DBSQL::Insert(wmsstyles,
			styleset => $styleset,
			dataset => $dataset,
			region => $ids{$region},
			bordercolor => $bdrclr,
			borderweight => "1",
			fillcolor => $fillcolor,
			textcolor => "255,255,50",
			label => "$income"
			);
	}
}


sub CDLanguageParty {
	my $styleset = 'cd-language-party';

	my $dataset = 'http://www.rdfabout.com/rdf/usgov/congress/house/110';
	($dataset) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
	if (!defined($dataset)) { die "No geometry is available for this data set."; }

	DBSQL::Delete(wmsstyles, [DBSQL::SpecEQ(styleset, $styleset)]);

	my @regions = DBSQL::Select("wmsidentifiers, wmsgeometry", ['id', 'value'], [DBSQL::SpecEQ(dataset, $dataset), "id=region"]);
	
	my @regions2;
	my %ids;
	for my $x (@regions) {
		if (defined($ids{$$x[1]})) { next; }
		$ids{$$x[1]} = $$x[0];
		push @regions2, "\$district = <$$x[1]>";
	}
	my $filter = "FILTER(" . join(" || ", @regions2) . ")";

		$query = <<EOF;
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX pol: <http://www.rdfabout.com/rdf/schema/politico/>
PREFIX usgov: <http://www.rdfabout.com/rdf/schema/usgovt/>
PREFIX census: <http://www.rdfabout.com/rdf/schema/census/>
PREFIX samp: <tag:govshare.info,2005:rdf/census/details/samp/>
SELECT ?district ?party ?poptotal ?hebrew WHERE {
   ?district census:details [
      samp:population5YearsAndOver [
         samp:_18YearsAndOver [
         	rdf:value ?poptotal ;
         	samp:hebrew ?hebrew
         ]
      ]
   ].
   ?pol pol:hasRole [ pol:forOffice [ pol:represents ?district; usgov:congress "110" ] ] .
   ?pol usgov:party ?party .
   $filter
}
EOF

	require "/home/rdfabout/census/sparql.pl";
	my @bindings = SparqlQuery("http://www.rdfabout.com/sparql", $query);
	
	my %values;
	my $maxvalue;
	foreach my $binding (@bindings) {
		my $region = $$binding{district};
		my $party = $$binding{party};

		$values{$region}{party} = $party;
		$values{$region}{value} = $$binding{hebrew} / $$binding{poptotal};
		if ($values{$region}{value} > $maxvalue) { $maxvalue = $values{$region}{value}; }
	}

	$maxvalue *= .7;

	foreach my $region (keys(%values)) {
		my $value = $values{$region}{value};
		my $party = $values{$region}{party};
	
		my $clr = int(sqrt($value/$maxvalue) * 220) + 25;
		if ($clr > 255) { $clr = 255; }
		$fillcolor = "$clr,$clr,$clr";
		
		my $bdrclr = "255,255,255";
		if ($party eq 'Democrat') { $fillcolor = "0,0,$clr"; }
		if ($party eq 'Republican') { $fillcolor = "$clr,0,0"; }
		
		my $valuepct = int($value*10000)/100;
		
		DBSQL::Insert(wmsstyles,
			styleset => $styleset,
			dataset => $dataset,
			region => $ids{$region},
			bordercolor => $bdrclr,
			borderweight => "1",
			fillcolor => $fillcolor,
			textcolor => "255,255,50",
			label => "$valuepct\%"
			);
	}
}

sub CDSpectrum {
	my $styleset = 'cd-spectrum';

	my $dataset = 'http://www.rdfabout.com/rdf/usgov/congress/house/110';
	($dataset) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
	if (!defined($dataset)) { die "No geometry is available for this data set."; }

	DBSQL::Delete(wmsstyles, [DBSQL::SpecEQ(styleset, $styleset)]);

	my %V;
	my $min;
	my $max;
	open S, "<../data/us/110/repstats/svd.txt";
	while (!eof(S)) {
		my $line = <S>; chop $line;
		my ($id, $d0, $d1, $d2, $d3, $d4, $d5, $d6, $lastname, $party, $state, $dist, $name) = split(/\t/, $line);
		if ($line eq '' || $dist eq 'sen') { next; }
		my $session = 110;
		my $uri;
		$state = lc($state);
		if ($dist > 0) {
        	$uri = "http://www.rdfabout.com/rdf/usgov/geo/us/$state/cd/$session/$dist";
		} else {
			$uri = "http://www.rdfabout.com/rdf/usgov/geo/us/$state";
		}

		my $d = $d1;
		$V{$uri} = [$d, $lastname];
		if (!defined($min) || $d < $min) { $min = $d; }
		if (!defined($max) || $d > $max) { $max = $d; }
	}
	close S;

	foreach my $region (keys(%V)) {
		my $value = $V{$region}[0];
		my $name = $V{$region}[1];

		($rid) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $region)]);
		if (!defined($rid)) { warn "$region"; next; }
		
		my ($red, $green, $blue) = (0,0,0);
		$red = int((($min+$max)/2-$value)/(($max-$min)/2) * 255) if ($value < ($min+$max)/2);
		$blue = int(($value-($min+$max)/2)/(($max-$min)/2) * 255) if ($value > ($min+$max)/2);
		
		$c = 1 - abs($value - ($min+$max)/2) / (($max-$min)/2);
		$c = $c*$c*$c;
		for $x ($red, $green, $blue) {
			$x = $x + $c*(255-$x)
		}
	
		my $fillcolor = "$red,$green,$blue";
		
		DBSQL::Insert(wmsstyles,
			styleset => $styleset,
			dataset => $dataset,
			region => $rid,
			bordercolor => '0,0,0',
			borderweight => "1",
			fillcolor => $fillcolor,
			textcolor => "255,255,50",
			label => $name
			);
	}
}

sub ContributionsToHoltByZipCode {
	my $styleset = 'fec-contributions-holt-zipcode';

	my $dataset = 'http://www.rdfabout.com/rdf/usgov/us/zctas';
	($dataset) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ('value', $dataset)]);
	if (!defined($dataset)) { die "No geometry is available for this data set."; }

	DBSQL::Delete(wmsstyles, [DBSQL::SpecEQ(styleset, $styleset)]);

		$query = <<EOF;
PREFIX fec: <http://www.rdfabout.com/rdf/schema/usfec/>
SELECT ?zcta ?amount ?population WHERE {
 ?zip fec:zipAggregatedContribution [
    fec:toCampaign  <http://www.rdfabout.com/rdf/usgov/fec/campaign/2006/H6NJ12144> ;
    fec:amount ?amount 
   ];
   fec:zcta ?zcta .
   ?zcta <http://www.rdfabout.com/rdf/schema/census/population> ?population .
} ORDER BY DESC(?amount) 
EOF

	require "/home/rdfabout/census/sparql.pl";
	my @bindings = SparqlQuery("http://www.rdfabout.com/sparql", $query);
	
	my %values;
	foreach my $binding (@bindings) {
		my $region = $$binding{zcta};
		my $value = $$binding{amount};
		$value /= $$binding{population};
		$value = int($value*100)/100;
		$values{$region} = $value;
	}
	
	my @order = sort({ $values{$a} <=> $values{$b} } keys(%values));
	for (my $i = 0; $i < scalar(@order); $i++) {
		my $region = $order[$i];
		my $value = $values{$region};
	
		my @clr;
		$clr[0] = int($i/scalar(@order)*255);
		$clr[1] = 0;
		$clr[2] = 0;
		my $fillcolor = join(",", @clr);
		
		my $valuestr = $value;
		$valuestr =~ s/(\d)((\d\d\d)+)$/$1,$2/g;
		
		my ($rid) = DBSQL::SelectFirst('wmsidentifiers', ['id'], [DBSQL::SpecEQ(value, $region)]);
		if (!defined($rid)) { warn $region; next; }
		
		DBSQL::Insert(wmsstyles,
			styleset => $styleset,
			dataset => $dataset,
			region => $rid,
			bordercolor => "0,0,0",
			borderweight => "2",
			fillcolor => $fillcolor,
			textcolor => "255,255,50",
			label => "\$$valuestr pc"
			);
	}
	
	return;

	my @regions = DBSQL::SelectVector(wmsgeometry, ['region'], [DBSQL::SpecEQ(dataset, "http://www.rdfabout.com/rdf/usgov/congress/house/110")]);
	my %seen;
	foreach my $region (@regions) {
		if ($seen{$region}) { next; } $seen{$region} = 1;
		DBSQL::Insert(wmsstyles,
			styleset => $styleset,
			dataset => "http://www.rdfabout.com/rdf/usgov/congress/house/110",
			region => $region,
			bordercolor => "0,0,0",
			borderweight => "1",
			fillcolor => undef,
			textcolor => "255,255,50",
			label => undef
			);
	}
}


