#!/usr/bin/perl

use CGI;

print "Content-type: text/html\n\n";
print CGI::param('data');
