#!/usr/bin/perl

use DBSQL;
use WmsConfig;

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

DBSQL::Execute("DROP TABLE wmsidentifiers;");
DBSQL::Execute("CREATE TABLE wmsidentifiers (id INT, value TEXT NOT NULL);");
DBSQL::Execute("CREATE UNIQUE INDEX id ON wmsidentifiers (id);");
DBSQL::Execute("CREATE UNIQUE INDEX value ON wmsidentifiers (value(255));");

DBSQL::Execute("DROP TABLE wmsgeometry;");
DBSQL::Execute("CREATE TABLE wmsgeometry (dataset INT NULL, region INT NOT NULL, long_min DOUBLE NOT NULL, long_max DOUBLE NOT NULL, lat_min DOUBLE NOT NULL, lat_max DOUBLE NOT NULL, innerpt_long DOUBLE NOT NULL, innerpt_lat DOUBLE NOT NULL, polygon LONGBLOB NOT NULL, smallpolygon BLOB NOT NULL, area FLOAT NOT NULL);");
DBSQL::Execute("CREATE INDEX reg ON wmsgeometry (dataset, region);");
DBSQL::Execute("CREATE INDEX long_min ON wmsgeometry (dataset, long_min);");
DBSQL::Execute("CREATE INDEX long_max ON wmsgeometry (dataset, long_max);");
DBSQL::Execute("CREATE INDEX lat_min ON wmsgeometry (dataset, lat_min);");
DBSQL::Execute("CREATE INDEX lat_max ON wmsgeometry (dataset, lat_max);");

DBSQL::Close()

