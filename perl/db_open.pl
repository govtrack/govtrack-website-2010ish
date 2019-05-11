use DBSQL;

my $pw;
open PW, "</home/govtrack/priv/mysqlpw";
$pw = <PW>;
chop $pw;
close PW;

DBSQL::Open("govtrack", "govtrack", $pw);

1;

