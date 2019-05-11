#!/usr/bin/perl

use DBSQL;
use WmsConfig;

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

DBSQL::Execute("DROP TABLE wmsstyles;");
DBSQL::Execute("CREATE TABLE wmsstyles (styleset VARCHAR(20) NOT NULL, dataset INT NOT NULL, region INT NOT NULL, bordercolor TEXT, borderweight INT, fillcolor TEXT, radius FLOAT, textcolor TEXT, textbackfill TEXT, label TEXT, font TEXT, markerdata TEXT);");
DBSQL::Execute("CREATE UNIQUE INDEX reg ON wmsstyles (styleset(20), dataset, region);");

DBSQL::Close();

