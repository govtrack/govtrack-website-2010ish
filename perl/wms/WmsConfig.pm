package WmsConfig;

$DATABASE = 'govtrack';
$DATABASE_USER = 'govtrack';
$DATABASE_PASSWORD = '';

open PW, "</home/govtrack/priv/mysqlpw";
$DATABASE_PASSWORD = <PW>; chop $DATABASE_PASSWORD;
close PW;

1;

