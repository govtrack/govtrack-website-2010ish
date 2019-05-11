package WmsUploadUtils;

1;

sub CheckAuth {
	my $styleset = shift;
	
	my $script = $ENV{SCRIPT_FILENAME};
	my $path = $script;
	$path =~ s/[^\/]+$//;
	
	open (F, "<$path/access.txt") or die "Could not open access file: $@";
	while (!eof(F)) {
		my $line = <F>; chop $line;
		my ($f_stylesheet, $f_key) = split(/\s+/, $line);

		if ($styleset =~ /^$f_stylesheet$/
			&& CGI::param('key') eq $f_key) {
			return 1;
		}
	}
	close F;
	
	return 0;
}

sub ParseLine {
	my $entry = $_[0];
	
	$entry =~ s/^\s+//;
	$entry =~ s/\s+$//;
	if ($entry eq '' || $entry =~ /^\#/) { return (undef, undef); }
	
	my @fields;
	if ($entry =~ /\t/) {
		# If there are any tabs, split the line using tab
		# as the delimiter.
		@fields = split(/\t+/, $entry);
	} else {
		# If there are no tabs, split the line using any
		# space as a delimiter. This is useful when the
		# information is submitted from an HTML form.
		@fields = split(/\s+/, $entry);
	}
	
	my $regionuri = shift(@fields);
	my %hash;
	for my $f (@fields) {
		if ($f !~ /^(\w+):(.*)$/) {
			print "Invalid style term: $f\n";
			next;
		}
		$hash{$1} = $2;
	}
	
	return ($regionuri, %hash);
}

