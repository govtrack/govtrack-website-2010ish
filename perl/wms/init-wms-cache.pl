#!/usr/bin/perl

use DBSQL;

use WmsConfig;

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

DBSQL::Execute("DROP TABLE IF EXISTS wmscache;");
DBSQL::Execute("CREATE TABLE wmscache (styleset VARCHAR(20), regionfilter TEXT, srs ENUM ('EPSG:54004', 'EPSG:4326'), zoom TINYINT NOT NULL, tilex INT NOT NULL, tiley INT NOT NULL, data BLOB NOT NULL, stamp TIMESTAMP);");
DBSQL::Execute("CREATE INDEX cachekey ON wmscache (zoom, tilex, tiley, styleset, srs);");
	# the cache index is not unique because it doesn't include the regionfilter column

DBSQL::Close();
