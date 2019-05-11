#!/usr/bin/perl

use Data::Dumper;
use Data::Serializer;
use CGI;
$CGI::DISABLE_UPLOADS = 1;

push @INC, "/home/govtrack/website/perl";
require "sparql.pl";

my $limit = 1000;
my $debug_query;

$serializer = Data::Serializer->new(
	serializer => 'Storable',
	compress => 1,
	encoding => 'b64',
	serializer_token => 0
	);

print <<EOF;
Content-type: text/html

<html>
	<head>
		<title>Extractor Demo</title>
		<style>
			pre.debug { font-size: 85%; border: 1px solid #999999; background-color: #FFFFEE; padding: 4px; }
		</style>
	</head>
	<body>
<h2>Extractor Demo</h2>
EOF

$SIG{__DIE__} = sub { my $x = $_[0]; $x =~ s/</\&lt;/g; print "<div style='color: red'>Error! $x</div>"; exit(0); };

if (CGI::param('q')) {
	%q = %{ $serializer->deserialize(CGI::param('q')) };
}

# STEP 1: CHOOSE A DATA SOURCE
if (!CGI::param('q')) {
	print <<EOF;
<h3>Step 1: Choose Data Source</h3>
<p>The <i>Extractor</i> will help you create a table of data from an RDF SPARQL data source and then create a visualization of that data.</p>
<p>Choose a data source:</p>
<form>
	<select size=1 name="q">
EOF
	
		for my $src (
			['http://www.rdfabout.com/sparql', 'Josh\'s Endpoint'],
			['http://demo.openlinksw.com/sparql/', 'OpenLinkSW'],
			#['http://revyu.com/sparql', 'Revyu'],
			['http://dbpedia.org/sparql', 'dbpedia'],
			['http://purl.org/dbtune/sparql', 'DBTune'],
			['http://www4.wiwiss.fu-berlin.de/factbook/sparql', 'World Factbook (FU-Berlin)']
			) {

			my $q = { src => $$src[0] };
			$q = CGI::escapeHTML($serializer->serialize($q));
			print "<option value=\"$q\">$$src[1]</option>\n";
		}

	print <<EOF;
	</select>
	<input type=submit value="Go on to Step 2 >">
</form>
EOF

# STEP 2: CHOOSE A SET OF OBJECTS FOR THE ROWS OF THE TABLE
} elsif (!$q{rowtype}) {
	print <<EOF;
<h3>Step 2: Choose Table Row Object Type</h3>
<p>Now you have to choose what the rows of your table represent. For instance, if you are
going to create a table of population statistics from U.S. counties, each row in the
table represents a county.</p>
<p>Choose what type of object the rows of your table correspond to:</p>
<form>
	<select size=1 name="q">
EOF

	# Get a list of class types in the data source.
	my @bindings = SparqlQuery($q{'src'}, <<EOF);
SELECT DISTINCT ?typeuri ?typename WHERE {
	[] <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> ?typeuri .
	?typeuri <http://www.w3.org/2000/01/rdf-schema#label> ?typename .
}
EOF

	# For each, build an updated query object.
	for my $b (@bindings) {
		my %q2 = %{ $serializer->deserialize(CGI::param('q')) };
		$q2{rowtype} = $$b{typeuri};
		my $q2 = ser(\%q2);
		print "<option value=\"$q2\">$$b{typename}</option>\n";
	}

	print <<EOF;
	<input type=hidden name=action value="filters">
	<input type=submit value="Go on to Step 3 >">
</form>
EOF

# FILTER THE ROWS
} elsif (CGI::param('action') eq 'filters') {
	# Get a descriptive name for the row type
	my $rowtypename = getLabel($q{rowtype});
	
	print <<EOF;
<h3>Step 3: Add Columns to Table</h3>
<p>You've chosen to make a table of ${rowtypename}s. Now you can add
columns to the table and restrict the values of the columns to narrow
your table.</p>
EOF

	# How many rows match the current (possibly empty) filter?
	my @bindings = LoadTable(final => 1);
	my $rowcount = scalar(@bindings);
	$debug_query = $last_query;
	
	my $limitmsg;
	if ($rowcount == $limit) { $limitmsg = "It might be that the maximum of $limit rows was reached."; }
	
	print <<EOF;
<p>There are $rowcount rows in your table right now. $limitmsg Add columns
and then apply restrictions to their values to get fewer rows in your table.</p>
EOF

	# Modify any of the current filters.
	if (scalar(@{ $q{cols} }) > 0) {
		print "<h4>Table Columns</h4>\n";
	}
	
	for my $c (@{ $q{cols} }) {
		DisplayColumns($c, \@bindings);
	}
	
	# Add a column on a property.
	my %ents;
	for my $b (@bindings) {
		$ents{'<' . $$b{row} . '>'} = 1;
	}
	my %properties = (
		GetProperties($q{rowtype}),
		GetInstantiatedProperties(keys(%ents)));

	print <<EOF;
<h4>Add a Column</h4>
<p>Add a column to the table by choosing a property of the $rowtypename
items found in the database. You can filter the value of the column
later to restrict which rows appear in your table.</p>
<form>
	<select size=1 name="q">
EOF

	for my $p (keys(%properties)) {
		# Add a new column into the data structure.
		my %q2 = %{ $serializer->deserialize(CGI::param('q')) };
		push @{ $q2{cols} }, { p => $p };
		my $q2 = ser(\%q2);
		print "<option value=\"$q2\">$properties{$p}</option>";
	}
	
	print <<EOF;
	</select>
	<input type=hidden name=action value="filters">
	<input type=submit value="Add Column">
</form>
EOF

	my $qe = CGI::escapeHTML(CGI::param('q'));
	print <<EOF;
<h4>View Table</h4>
<p>If you're done adding columns, go take a look at your spreadsheet.
You can always come back and add columns and filter out rows.</p>
<form>
	<input type=hidden name=q value="$qe">
	<input type=submit value="View Table">
</form>
EOF

# VIEW THE TABLE	
} else {

	my $qe = CGI::escapeHTML(CGI::param('q'));

print <<EOF;
<h3>Your Table</h3>

<p>Here's your table!</p>

<form>
	<input type=hidden name=q value="$qe">
	<input type=hidden name=action value="filters">
	<input type=submit value="Edit Columns">
</form>

<table border=1>
<tr>
EOF

	ExpandColumnNames(@{$q{cols}});

	print <<EOF;
</tr>
EOF

	my @bindings = LoadTable(final => 1);
	$debug_query = $last_query;
	for my $row (@bindings) {
		print "<tr>";
		ExpandColumnValues($row, @{$q{cols}});
		print "</tr>\n";
	}
	
	print <<EOF;
</table>
EOF

}

if (CGI::param('q')) {
	print "<hr/>\n";
	print "<pre class=debug>";
	print "The following is debugging information. It is the SPARQL query.\n";
	print CGI::escapeHTML($debug_query) . "</pre>\n";
	print "<pre class=debug>";
	print "The following is debugging information. It is the Perl structure that represents the query.\n";
	print CGI::escapeHTML(Dumper(\%q)) . "</pre>\n";
}

print <<EOF;
<hr/>
<div style="font-size: 85%">A really experimental project by <a href="http://razor.occams.info">Josh Tauberer</a>.</div>
	</body>
</html>
EOF

sub getLabel {
	my ($uri) = @_;
	my @bindings = SparqlQuery($q{src}, <<EOF);
SELECT DISTINCT ?name WHERE {
	{ <$uri> <http://www.w3.org/2000/01/rdf-schema#label> ?name . }
	UNION
	{ <$uri> <http://purl.org/dc/elements/1.1/title> ?name . }
	UNION
	{ <$uri> <http://xmlns.com/foaf/0.1/name> ?name . }
}
EOF
	if (!$bindings[0]) { return GetUriNiceName($uri); }
	return $bindings[0]{name};
}

sub sgn {
	if ($_[0] > 0) { return 1; }
	if ($_[0] < 0) { return -1; }
	return 0;
}

sub sublist {
	my ($array, $offset, $length) = @_;
	my @ret;
	if (!defined($offset)) { $offset = 0; }
	if (!defined($length)) { $length = scalar(@$array) - $offset; }
	for (my $i = $offset; $i < $offset + $length; $i++) {
		push @ret, $$array[$i];
	}
	return @ret;
}

sub LoadTable {
	my %opts = @_;

	my $qopts = { var => 0, select => $opts{select_variable}, select_properties => $opts{select_properties},
		svars => [], final => $opts{final} };
	my $queryfilters = '';
	for my $c (@{ $q{cols} }) {
		$queryfilters .= ConstructQuery('row', $c, $qopts);
	}
	
	my $sel = "*";
	if ($opts{final}) { $sel = "?row " . join(' ', @{ $$qopts{'svars'} }); } # we need row somewhere
	if ($opts{select_variable}) {
		if (!$opts{select_properties}) {
			$sel = "?$opts{select_variable} ?$opts{select_variable}_label ?$opts{select_variable}_type";
		} else {
			$sel = "?property_uri ?property_label";
		}
	}
	
	$query = <<EOF;
SELECT DISTINCT $sel WHERE {
	?row <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <$q{rowtype}> .
	$queryfilters
}
LIMIT $limit
EOF

	#print "<pre class=debug>The following is debugging information. It is a query that was run:\n" . CGI::escapeHTML($query) . "</pre>";
	$last_query = $query;
	
	return SparqlQuery($q{src}, $query);
}

sub ConstructQuery {
	my ($pvar, $col, $qopts) = @_;
	
	if ($$col{deleted}) { return; }
	
	my $squery;
	if (!$$col{value}) {
		my $cvar = 'q' . (++$$qopts{var});
		$$col{var} = $cvar;
		
		if (!$$qopts{select_properties}) {
			if (!$$qopts{final} || !scalar(@{ $$col{subcols} })) {
				$squery .= "OPTIONAL { ?$cvar <http://www.w3.org/2000/01/rdf-schema#label> ?${cvar}_label  }\n";
				$squery .= "OPTIONAL { ?$cvar <http://xmlns.com/foaf/0.1/name> ?${cvar}_label  }\n";
				$squery .= "OPTIONAL { ?$cvar <http://purl.org/dc/elements/1.1/title> ?${cvar}_label  }\n";
			}
			if (!$$qopts{final}) { $squery .= "OPTIONAL { ?$cvar <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> ?${cvar}_type  }\n"; }
			if ($$qopts{final} && !scalar(@{ $$col{subcols} })) { $squery .= "FILTER(!isBlank(?$cvar)).\n"; }
		} else {
			if ($$qopts{select} eq $cvar) {
				if ($$qopts{select_properties} == 1) {
					$squery .= "?$cvar ?property_uri [].\n";
				} else {
					$squery .= "[] ?property_uri ?$cvar.\n";
				}
				$squery .= "OPTIONAL { ?property_uri <http://www.w3.org/2000/01/rdf-schema#label> ?property_label . }\n";
			}
		}

		for my $c (@{ $$col{subcols} }) {
			$squery .= ConstructQuery($cvar, $c, $qopts);
		}
		
		$val = "?$cvar";
		
		if (!scalar(@{ $$col{subcols} })) {
			push @{ $$qopts{svars} }, "?$cvar";
			push @{ $$qopts{svars} }, "?${cvar}_label";
		}
	} else {
		$val = $$col{value};
	}
	
	my $predicate = $$col{'p'};
	my $relation;
	if (substr($predicate, 0, 1) ne '!') {
		$relation = "?$pvar <$predicate> $val .";
	} else {
		$predicate = substr($predicate, 1);
		$relation = "$val <$predicate> ?$pvar .";
	}
	
	return "$relation\n$squery";
}

sub ser {
	return CGI::escapeHTML($serializer->serialize($_[0]));
}

sub GetProperties {
	my @types = @_;
	
	my %properties;
	
	my $src = $q{'src'};
	
	for my $type (@types) {
		# domain...
		my @bindings = SparqlQuery($src, <<EOF);
SELECT DISTINCT ?propertyuri ?propertyname WHERE {
	?propertyuri <http://www.w3.org/2000/01/rdf-schema#label> ?propertyname .
	?propertyuri <http://www.w3.org/2000/01/rdf-schema#domain> <$type> .
}
LIMIT 100
EOF
		for my $b (@bindings) {
			$properties{$$b{propertyuri}} = $$b{propertyname};
		}

		# range...
		@bindings = SparqlQuery($src, <<EOF);
SELECT DISTINCT ?propertyuri ?propertyname WHERE {
	?propertyuri <http://www.w3.org/2000/01/rdf-schema#label> ?propertyname .
	?propertyuri <http://www.w3.org/2000/01/rdf-schema#range> <$type> .
}
LIMIT 100
EOF
		# Prefix the URI with a bang to indicate a reverse-directed property
		# Modify the label to make it clear as an inverse-property. We could
		# do that better.
		for my $b (@bindings) {
			$properties{'!' . $$b{propertyuri}} = 'This is a ' . $$b{propertyname} . ' of...';
		}
	}

	return %properties;
}

sub GetInstantiatedProperties {
	my @entities = @_;
	
	if ($q{'src'} !~ /rdfabout.com/) { return; } # only I support the needed FILTER optimization
	if (scalar(@entities) == 0) { return; }
	
	my %properties;
	
	my $src = $q{'src'};
	
	my @e;
	for my $ent (@entities) {
		push @e, "?x = $ent";
		if (scalar(@e) > 200) { last; }
	}
	my $ents = join(" || ", @e);
	
	# domain...
	my @bindings = SparqlQuery($src, <<EOF);
SELECT DISTINCT ?propertyuri ?propertyname WHERE {
	OPTIONAL { ?propertyuri <http://www.w3.org/2000/01/rdf-schema#label> ?propertyname . }
	?x ?propertyuri [] .
	FILTER($ents) .
}
LIMIT 100
EOF
	for my $b (@bindings) {
		if ($$b{propertyname} eq '') {
			$$b{propertyname} = GetUriNiceName($$b{propertyuri});
		}
		$properties{$$b{propertyuri}} = $$b{propertyname};
	}

	# range...
	@bindings = SparqlQuery($src, <<EOF);
SELECT DISTINCT ?propertyuri ?propertyname WHERE {
	?propertyuri <http://www.w3.org/2000/01/rdf-schema#label> ?propertyname .
	[] ?propertyuri ?x .
	FILTER($ents) .
}
LIMIT 100
EOF
	# Prefix the URI with a bang to indicate a reverse-directed property
	# Modify the label to make it clear as an inverse-property. We could
	# do that better.
	for my $b (@bindings) {
		$properties{'!' . $$b{propertyuri}} = 'This is a ' . $$b{propertyname} . ' of...';
	}

	return %properties;
}

sub DisplayColumns {
	my $c = shift;
	my $all_bindings = shift;
	my $parent;
	
	if ($$c{deleted}) { return; }
	
	# Get a descriptive name for the column predicate.
	my $p = $$c{p};
	my $d = 0;
	if (substr($$c{p},0,1) eq "!") {
		$p = substr($p, 1);
		$d = 1;
	}
	
	my $fname = getLabel($p);
	if ($d) {
		$fname = "Is $fname Of";
	}

	# Create a serialized string with this filter removed.
	$$c{deleted} = 1;
	my $filter_remove = ser(\%q);
	delete $$c{deleted};
	
	if (!defined($$c{value})) {
		my $added = 'added';
		if ($parent) {
			$added = 'expanded this column by adding';
		}
	
		my $bindings = [ LoadTable(select_variable => $$c{var}) ];
		#print CGI::escapeHTML($last_query);

		my @opts;
		my %seen;
		my %types;
		my %entities;
		my %entnames;
		my $hasbnode;
		for my $b (@{$bindings}) {
			my $val;
			my $display;
			if ($$b{$$c{var} . '__valuetype'} eq 'uri') {
				$val = "<" . $$b{$$c{var}} . ">";
				$display = $$b{$$c{var} . '_label'};
				if ($display eq '') { $display = $val; }
				if (defined($$b{$$c{var} . "_type"}) && scalar(keys(%types)) <= 10) {
					$types{$$b{$$c{var} . "_type"}} = 1;
				}
				if (scalar(keys(%entities)) < 50) { $entities{$val} = 1; }
				$entnames{CGI::escapeHTML($display)} = 1;
			} elsif ($$b{$$c{var} . '__valuetype'} eq 'literal') {
				$val = "\"" . $$b{$$c{var}} . "\"";
				$display = $$b{$$c{var}};
				if ($$b{$$c{var} . '__datatype'}) {
					$val .= '^^<' . $$b{$$c{var} . '__datatype'} . '>';
				}
			} elsif ($$b{$$c{var} . '__valuetype'} eq 'bnode') {
				$hasbnode = 1;
				next;
			} else {
				next;
			}
			if ($seen{$val}) { next; } $seen{$val} = 1;
			$display =~ s/^(.{60}).*/$1.../;
			push @opts, [$display, $val];
			if (scalar(@opts) > 100) { last; }
		}

		print <<EOF;
<p>You have $added a <b>$fname</b> column to the table.</p>
<div style="margin-left: 2em">
EOF

		if (scalar(@opts)) {
			print <<EOF;
<p>To restrict what the value can be in this column, removing unwanted rows
from your table, you may choose a value:</p>
<table>
<tr>
<td>
<form>
	<select size=1 name="q">
EOF

			for my $opt (sort({ $$a[0] cmp $$b[0] } @opts)) {
				$$c{value} = $$opt[1]; # temporarily modify %q
				$q2 = ser(\%q);
				$$opt[0] = CGI::escapeHTML($$opt[0]);
				print "<option value=\"$q2\">$$opt[0]</option>";
				delete $$c{value};		# restore previous %q
			}
			print <<EOF;
	</select>
	<input type=hidden name=action value="filters">
	<input type=submit value="Restrict $fname to This Value">
</form>
</td>
<td>
<form>
	<input type=hidden name=q value="$filter_remove">
	<input type=hidden name=action value="filters">
	<input type=submit value="Remove Column">
</form>
</td>
</tr>
</table>
EOF
		} else {
			print <<EOF;
<form>
	<input type=hidden name=q value="$filter_remove">
	<input type=hidden name=action value="filters">
	<input type=submit value="Remove $fname Column">
</form>
EOF
		}
		
		if (scalar(keys(%types)) || scalar(keys(%entities)) || $hasbnode) {
			my $examples = "";
			my @entnames = keys(%entnames);
			my $entnames = join('; ', splice(@entnames, 0, 5));
			if ($entnames ne '') {
				$examples = " (examples are <i>$entnames</i>)";
			}

			# Get the properties of all of the types of things
			# in this column.
			my %properties = (
				GetProperties(keys(%types)),
				GetInstantiatedProperties(keys(%entities))
				);

			if ($hasbnode) {
				for my $b (LoadTable(select_variable => $$c{var}, select_properties => 1)) {
					if ($$b{property_uri}) {
						$properties{$$b{property_uri}} = ($$b{property_label} ? $$b{property_label} : getLabel($$b{property_uri}));
					}
				}
				for my $b (LoadTable(select_variable => $$c{var}, select_properties => 2)) {
					if ($$b{property_uri}) {
						$properties{'!' . $$b{property_uri}} = "This Is " . ($$b{property_label} ? $$b{property_label} : getLabel($$b{property_uri})) . " Of...";
					}
				}
			}
			
			my $complex = "complex values";
			if (scalar(keys(%types))) {
				my ($t0) = keys(%types);
				$complex = getLabel($t0) . 's';
			}
			
			print <<EOF;
<div>This column contains $complex$examples.  You can expand this column to get deeper properties of the entities in this column. Choose a property of the entities in this column to expand into a new column.</div>
<form>
	<select size=1 name="q">
EOF

			for my $p (keys(%properties)) {
				# Add a new column into the data structure.
				push @{ $$c{subcols} }, { 'p' => $p };
				my $q2 = ser(\%q);
				print "<option value=\"$q2\">$properties{$p}</option>";
				pop @{ $$c{subcols} }; # undo change
			}
	
			print <<EOF;
	<input type=hidden name=action value="filters">
	<input type=submit value="Add Column">
</form>
EOF

			for my $c2 (@{ $$c{subcols} }) {
				DisplayColumns($c2, $all_bindings, 1);
				# we should check for infinite recursion
			}

		}

		print <<EOF;
</div>
EOF

	} else {
		# The filter is valued.
		$fvaluedisplay = CGI::escapeHTML($$c{value});
		if ($$c{value} =~ /^<(.*)>$/) {
			$fvaluedisplay = CGI::escapeHTML(getLabel($1));
		}
		print <<EOF;
<p>You have applied a <b>$fname</b> restriction with a value of $fvaluedisplay.</p>
<div style='margin-left: 2em'>
<form>
	<input type=hidden name=q value="$filter_remove">
	<input type=hidden name=action value="filters">
	<input type=submit value="Remove Restriction">
</form>
</div>
EOF
		
	}
	
}

sub ExpandColumnNames {
	my @cols = @_;
	for my $c (@cols) {
		if ($$c{deleted}) { next; }
		if ($$c{value}) { next; }
		
		if (!scalar(@{ $$c{subcols} })) {
			# Get a descriptive name for the column predicate.
			my $p = $$c{p};
			my $d = 0;
			if (substr($$c{p},0,1) eq "!") {
				$p = substr($p, 1);
				$d = 1;
			}
	
			my $fname = getLabel($p);
			if ($d) {
				$fname = "Is $fname Of";
			}
			$fname = CGI::escapeHTML($fname);
	
			print "<th>$fname</th> ";
		}
		
		ExpandColumnNames(@{$$c{subcols}});
	}
}

sub ExpandColumnValues {
	my $binding = shift;
	my @cols = @_;
	for my $c (@cols) {
		if ($$c{deleted}) { next; }
		if ($$c{value}) { next; }

		if (!scalar(@{ $$c{subcols} })) {
			my $val = CGI::escapeHTML($$binding{$$c{var}});
			if ($$binding{$$c{var} . '__valuetype'} eq 'uri'
				&& $$binding{$$c{var} . '_label'}) {
				$val = CGI::escapeHTML($$binding{$$c{var} . '_label'});
			}
			print "<td>$val</td> ";
		}
		ExpandColumnValues($binding, @{$$c{subcols}});
	}
}

sub GetUriNiceName {
	my $uri = shift;
	if ($uri =~ /[#\/]([^#\/]+)$/) {
		return $1;
	} else {
		return $uri;
	}
}
