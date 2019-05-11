#!/usr/bin/perl

if (0 && $ENV{HTTP_REFERER} !~ /^http:\/\/www.govtrack.us\// && $ENV{HTTP_REFERER} !~ /popvox/) {
print <<EOF;
Status: 503

Service termporarily disabled.
EOF
exit(0);
}

use CGI;
use GD;
use GD::Image;
use Data::Serializer;
use POSIX;
use MIME::Base64;
use Apache2::RequestUtil;
use HTTP::Date;

$CGI::DISABLE_UPLOADS = 1;

use DBSQL;

use WmsConfig;

##########################################################

# General Parameters
$CacheMethod = 'db'; # 'db' or 'disk' or 'none'
$CacheDir = '/tmp/wms'; # server-side disk cache location, if used
$CacheTime = 900; # client-side cache, expiration in seconds

$Watermark = undef; # string to watermark tiles with
$fontname = '/usr/share/fonts/truetype/ttf-dejavu/DejaVuSans-Bold.ttf';

##########################################################

binmode STDOUT;

#if ($ENV{HTTP_REFERER} =~ /youngguns/) {
#	print <<EOF;
#Status: 500
#
#EOF
#}

# important parameters:
#    LAYERS, STYLES
#    BBOX (-84.375,31.95216223802497,-78.75,36.59788913307022) i.e. (bottom left, top right)
#    WIDTH, HEIGHT

# Load the parameters of the image to output

sub get_param_ci {
	my $paramname = shift;
	my $ret = CGI::param($paramname);
	if (defined($ret)) { return $ret; }
	return CGI::param(lc($paramname));
}

$layers = get_param_ci('LAYERS');
$styles = get_param_ci('STYLES');
$srs = get_param_ci('SRS');

($long1, $lat1, $long2, $lat2) = split(/,/, get_param_ci('BBOX'));

$width = get_param_ci('WIDTH');
$height = get_param_ci('HEIGHT');

my @tile_info;

# For Google's My Maps layers, passing a title as google_tile_template_values=zoom,x,y is useful.
if (get_param_ci('google_tile_template_values')) {
	my ($x, $y, $zoom) = split(/,/, get_param_ci('google_tile_template_values'));
	
	if (get_param_ci('LAYERS') =~ /^cd-110(-outlines)?(:http:..www.rdfabout.com.rdf.usgov.geo.us.(..).cd.110.(\d+))?/) {
		my $feature;
		if ($3) { my ($st, $di) = ($3, $4); $st = lc($st); $di = sprintf("%02d", $di); $feature = "/$st-$di"; }
		print <<EOF;
Status: 303
Location: http://gis.govtrack.us/map/tiles/2010-cd$feature/$zoom/$x/$y.png

EOF
		exit(0);
	}
	
	$srs = 'EPSG:54004';
	$width = 256;
	$height = 256;
	($lat2, $long1, $lat1, $long2) = gmapTileBounds($x, $y, $zoom);
	@tile_info = ($zoom, $x, $y);
}

# Sanity checks
if ($long1 >= $long2) { die "Invalid BBOX: $long1,$lat1, $long2,$lat2"; }
if ($lat1 >= $lat2) { die "Invalid BBOX: $long1,$lat1, $long2,$lat2"; }
if ($width < 10) { die "Invalid image dimension"; }
if ($height < 10) { die "Invalid image dimension"; }
if ($width > 1024) { die "Invalid image dimension"; }
if ($height > 1024) { die "Invalid image dimension"; }
if ($srs ne 'EPSG:54004' && $srs ne 'EPSG:4326' && $srs ne "EPSG:3857") { die "Unsupported SRS: $srs."; }

$debug = ($ENV{SERVER_NAME} eq '');

if (!$tile_info[0]) {
	@tile_info = gmapLatLonToTile($lat2, $long1, $long2);
}
my $tile_info = join("; ", @tile_info);

$info = <<EOF;
BBOX: ($long1,$lat1)-($long2,$lat2)
DIMS: $width x $height
LAYERS/STYLES: $layers | $styles
TILE: $tile_info
EOF
if ($debug) { print STDERR $info; }

# Generate the tile image. It may return undef if there's no image here.

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD);

my $img;
eval {	
	$img = LoadTile($long1, $lat1, $long2, $lat2, $width, $height, $layers, $styles);
};
my $error = $@;

DBSQL::Close();

if ($debug && $error) { die $error; }

# If there was an error, but there was no tile (e.g. because of the error),
# then create a new tile. Then, return a tile with the error message.
if ($error && !defined($img)) {
	$img = new GD::Image($width, $height, 1);
	#$img->useFontConfig(1);

	my $white = $img->colorAllocate(255, 255, 255); # first color allocated is background...?
	my $black = $img->colorAllocate(0, 0, 255);

	$img->transparent($white);
	$img->filledRectangle(0,0, $width,$height, $white);
}
if ($error) {
	warn $error;
	$error =~ s/\s/ /g;
	if ($ENV{MOD_PERL}) {
		my $r = Apache2::RequestUtil->request;
		$r->no_cache(1);
	} else {
		print <<EOF;
Expires: 1s
Cache-Control: none
Error-Message: $error
EOF
	}
	my $black = $img->colorAllocate(0, 0, 255);
	$img->stringFT($black, $fontname, 8, 0, 0,0, "[$error]");
}

# If there's no tile here, the standard thing to do is return a 404.
if (!defined($img)) {
	if ($ENV{MOD_PERL}) {
		my $r = Apache2::RequestUtil->request;
		$r->headers_out->{"Status"} = "404";
	} else {
		print "Status: 404\n\n";
	}
	exit(0);
}

# Draw some debugging info onto the tile?
if (0) {
	$fgcolor = $img->colorAllocate(0,0,0);
	$img->stringFT($fgcolor, $fontname, 8, 0, 10,10, $tile_info);
}

# Opportunity for us to watermark tiles, e.g. for TOS violation.
if ($ENV{HTTP_REFERER} =~ /youngguns/) {
	my $white = $img->colorAllocate(200, 200, 200);
	my $black = $img->colorAllocate(50, 50, 50);
	$img->stringFT($white, $fontname, 12, 0, 10,10, "Use requires credit to");
	$img->stringFT($white, $fontname, 12, 0, 10,25, "www.GovTrack.us");
	$img->stringFT($black, $fontname, 12, 0, 60,128, "Use requires credit to");
	$img->stringFT($black, $fontname, 12, 0, 60,143, "www.GovTrack.us");
}

if ($debug) {
	print STDERR "Finished.\n";
	exit(0);
}

# Output the tile image.

my $data;
my $type;
if (get_param_ci('FORMAT') eq "image/png") {
	$data = $img->png;
	$type = "image/png";
} else {
	$data = $img->gif;
	$type = "image/gif";
}
my $datalen = length($data);

my $expires = HTTP::Date::time2str(time + $CacheTime);

if ($ENV{MOD_PERL}) {
	my $r = Apache2::RequestUtil->request;
	$r->content_type($type);
	$r->headers_out->{"Content-Length"} = $datalen;
	if (!$error) {
		$r->headers_out->{"Expires"} = $expires;
		$r->headers_out->{"Cache-Control"} = "public";
	}
} else {
	print <<EOF;
Content-Type: $type
Content-Length: $datalen
EOF

	# don't cache if there was an error
	if (!$error) {
		print <<EOF;
Expires: $expires
Cache-Control: public
EOF
	}

	print "\n";
}

print $data;

# That's it!

######################################################################################3

sub LoadTile {
	my ($long1, $lat1, $long2, $lat2, $width, $height, $layers, $styles) = @_;
	
	my ($tilezoom, $tilex, $tiley) = gmapLatLonToTile($lat2, $long1, $long2);

	my $cacheable = 1;
	if ($width != 256 || $height != 256) { $cacheable = 0; }
	
	my $region_uri;
	if ($layers =~ /^([^:]*):(.+)$/) {
		$layers = $1;
		$region_uri = $2;
		if ($CacheMethod eq 'disk') {
			$cacheable = 0;
		}
	}
	
	my $styleset = $layers;
	my $layer = Clean($layers);
	
	my $filename;
	my %cachekey;
	
	if ($cacheable) {
	if ($CacheMethod eq 'disk') {
		if (defined($CacheDir) && -d $CacheDir) {
			$filename = "$CacheDir/$layer/" . Clean($srs) . "/${tilezoom}_${tilex}_${tiley}.png";
		}
	
		# Load tile from cache, unless a cache-reset file has been touch'd.
		if (defined($filename) && -e $filename && !-z $filename) {
			if (!-e "$CacheDir/$layer/clear-cache" || -M $filename < -M "$CacheDir/$layer/clear-cache") {
				my $img;
				eval {
					$img = GD::Image->newFromPng($filename);
				};
				if ($img) { return $img; }
				
				# cache file corrupted
				unlink $filename;
			}
		}

	} elsif ($CacheMethod eq 'db') {
		%cachekey = (
			styleset => $layer, regionfilter => $region_uri,
			srs => $srs,
			zoom => $tilezoom,
			tilex => $tilex,
			tiley => $tiley,
			);
		my ($data, $timestamp) = DBSQL::SelectFirst(wmscache, ["data", "stamp"], [DBSQL::SpecFromHash(%cachekey)]);
		if (defined($data)) {
			if ($ENV{MOD_PERL}) {
				my $r = Apache2::RequestUtil->request;
				$r->set_last_modified(HTTP::Date::str2time($timestamp));
			} else {
				$timestamp = HTTP::Date::time2str(HTTP::Date::str2time($timestamp));
				print "Last-Modified: $timestamp\n";
			}
			my $img;
			eval {
				$img = GD::Image->newFromPngData(decode_base64($data));
			};
			if ($img) { return $img; }

			# Failed reading: Delete cached entry.
			DBSQL::Delete(wmscache, [DBSQL::SpecFromHash(%cachekey)]);
		}
	}
	}
	
	if (1 && $ENV{HTTP_REFERER} !~ /popvox/) {
		return undef;
	}
	
	# Draw the tile.
	my ($img, $nshapes) = DrawTile($long1, $lat1, $long2, $lat2, $width, $height, $styleset, $layers, $styles, $region_uri);
	
	if ($nshapes == 0) {
		# Hopefully the db lookup was fast in the case that there's nothing actually to draw.
		return undef;
	}
	
	# Cache title.
	if ($cacheable) {
		if ($CacheMethod eq 'disk' && defined($filename)) {
			mkdir "$CacheDir/$layer";
			open (OUT, ">$filename") or warn "Writing WMS tile to cache: $@";
			flock OUT, 2;
			binmode OUT;
			print OUT $img->png;
			flock OUT, 0;
			close OUT;
		} elsif ($CacheMethod eq 'db') {
			eval {
				DBSQL::Insert(wmscache, %cachekey, data => encode_base64($img->png));
			};
		}
	}
	
	## Flag that this did not come from the cache, for debugging.
	#$img->filledRectangle(0,0, 1,1, $img->colorAllocate(255,0,0));
	
	return $img;
}


sub DrawTile {
	my ($long1, $lat1, $long2, $lat2, $width, $height, $styleset, $layers, $styles, $region_uri) = @_;

	my $img = new GD::Image($width, $height, 1);
	#$img->useFontConfig(1);

	my $white = $img->colorAllocate(255, 255, 255); # first color allocated is background...?
	my $black = $img->colorAllocate(0, 0, 255);

	$img->transparent($white);
	$img->filledRectangle(0,0, $width,$height, $white);

	#$img->stringFT($fgcolor, $fontname, 8, 0, $img->width/3,$img->height/3, get_param_ci('BBOX'));
	
	## If the styleset is non-numeric, search for its numeric key, if any.
	#my ($stylesetid) = DBSQL::SelectFirst("wmsidentifiers", ["id"], [DBSQL::SpecEQ(value, $styleset)]);
	#if ($stylesetid) { $styleset = $stylesetid; }

	# What data set does this styleset correspond to?
	my @datasets = DBSQL::SelectVectorDistinct("wmsstyles", ["dataset"], [DBSQL::SpecEQ(styleset, $styleset)]);
	if (scalar(@datasets) == 0) { return; }
	
	my @regionids;
	if ($region_uri) {
		if ($region_uri =~ s/\%$//) {
			@regionids = DBSQL::SelectVector("wmsidentifiers", ["id"], [DBSQL::SpecStartsWith('value', $region_uri)]);
		} else {
			@regionids = DBSQL::SelectVector("wmsidentifiers", ["id"], [DBSQL::SpecEQ('value', $region_uri)]);
		}
	}
	
	# Select the regions that intersect the viewing rectangle.
	# The ORDER BY clause ensures that we drawn filled regions first
	# because we want outline-only regions to be drawn on top.
	# We have to look for things slightly outside of the bounding
	# box because circles can have centers outside of the bounding
	# box, but a radius defined in the styleset that extents into
	# this box, or text that crosses two images (let's assume that's
	# the max).
	#
	# Also don't pick up regions so small they are less than a pixel
	# wide.
	my @regions = DBSQL::Select("wmsgeometry INNER JOIN wmsstyles ON wmsgeometry.region=wmsstyles.region AND wmsgeometry.dataset=wmsstyles.dataset",
			["wmsgeometry.region", "polygon",
			 "bordercolor, borderweight, fillcolor, radius, textcolor, textbackfill, label, font, innerpt_long, innerpt_lat"],
		[((scalar(@regionids) == 0) ? '1' : DBSQL::SpecIn('wmsgeometry.region', @regionids)),
		DBSQL::SpecEQ(styleset, $styleset),
		DBSQL::SpecIn('wmsgeometry.dataset', @datasets),
		DBSQL::SpecIn('wmsstyles.dataset', @datasets),
		DBSQL::SpecLE(long_min, $long2 + ($long2-$long1)),
		DBSQL::SpecGE(long_max, $long1 - ($long2-$long1)),
		DBSQL::SpecLE(lat_min, $lat2 + ($lat2-$lat1)),
		DBSQL::SpecGE(lat_max, $lat1 - ($lat2-$lat1)),
		DBSQL::SpecGE("long_max-long_min", ($long2-$long1)/$width),
		DBSQL::SpecGE("lat_max-lat_min", ($lat2-$lat1)/$width),
		],
		"ORDER BY fillcolor IS NULL");

	my $nshapes;

	my $serializer = Data::Serializer->new();

	foreach my $rec (@regions) {
		my ($region, $poly,
			$bordercolor, $borderweight, $fillcolor, $radius,
			$textcolor, $textbackfill, $label, $font, $intpt_long, $intpt_lat) = @$rec;
		
		$nshapes++;
		
		my $fgcolor = $fillcolor ne '' ? $img->colorAllocate(split(/, ?/, $fillcolor)) : undef;
		my $bcolor = $bordercolor ne  '' ? $img->colorAllocate(split(/, ?/, $bordercolor)) : undef;

		my $polygon = $serializer->deserialize($poly);
		my ($minx, $miny, $maxx, $maxy) = (undef, undef, undef, undef);
		
		if (scalar(@$polygon) == 1) {
			my $pt = $$polygon[0];
			my ($x, $y) = @$pt;
			my ($cx, $cy) = Project($x, $y, $long1, $lat1, $long2, $lat2, $width, $height);
			if (abs($cy - $height) < 4) { $cy = $height; } # some sort of GD drawing bug here? strange horizontal lines in some places
			$img->filledEllipse($cx,$cy, $radius*2,$radius*2, $bcolor) if (defined($bcolor));
			$img->ellipse($cx,$cy, $radius*2,$radius*2, $fgcolor) if (defined($fgcolor));
			
		} elsif (scalar(@$polygon) == 2) {
		
		} else {
			my $gdpoly = new GD::Polygon;
			my ($lastx, $lasty) = (undef, undef);
			foreach my $pt (@$polygon) {
				my ($x, $y) = @$pt;
				
				# Cull points too close to the last point
				if ($lastx && abs($x-$lastx) < abs($long2-$long1)/$width
					&& abs($y-$lasty) < abs($lat2-$lat1)/$height) {
					next;
				}
	
				my ($px, $py) = Project($x, $y, $long1, $lat1, $long2, $lat2, $width, $height);
				if (abs($py - $height) < 2) { $py = $height; } # some sort of GD drawing bug here? strange horizontal lines in some places
				$gdpoly->addPt($px, $py);
				($lastx, $lasty) = ($x, $y);
			
				if ($px < $minx || !defined($minx)) { $minx = $px; }
				if ($py < $miny || !defined($miny)) { $miny = $py; }
				if ($px > $maxx || !defined($maxx)) { $maxx = $px; }
				if ($py > $maxy || !defined($maxy)) { $maxy = $py; }
			}
	
			$img->filledPolygon($gdpoly, $fgcolor) if (defined($fgcolor));

			if ($borderweight > 0 && defined($bcolor)) {
				if (sqrt(($maxx-$minx)+($maxy-$miny))/8 < $borderweight) {
					$borderweight = sqrt(($maxx-$minx)+($maxy-$miny))/8;
				}
				$img->setThickness($borderweight);
				$img->openPolygon($gdpoly, $bcolor);
			}
		}
		
		if ($label ne '') {
			if (!defined($textcolor)) { $textcolor = '0,0,0'; }
			my $tcolor = $img->colorAllocate(split(/, */, $textcolor));
			my ($tx, $ty) = Project($intpt_long, $intpt_lat, $long1, $lat1, $long2, $lat2, $width, $height);
			my ($fontsize) = split(/, */, $font);
			if (!$fontsize) { $fontsize = 11; }
			if (scalar(@$polygon) < 3) {
				$img->stringFT($tcolor, $fontname, $fontsize, 0, $tx+$radius*.8,$ty-$radius*.8, $label);
			} else {
				my @bounds = GD::Image->stringFT($tcolor, $fontname, $fontsize, 0, 0,0, $label);
				# Check roughly that text fits within area of polygon.
				if ($tx-$bounds[2]/2 > $minx+1+$borderweight + 3
				 && $tx+$bounds[2]/2 < $maxx-1-$borderweight - 3
				 && $ty+$bounds[5] > $miny+1+$borderweight + 3
				 && $ty+$bounds[1] < $maxy-1-$borderweight - 3) {
					if (defined($textbackfill)) {
						my $tbcolor = $img->colorAllocate(split(/, */, $textbackfill));
						$img->filledRectangle($tx-$bounds[2]/2-1, $ty+$bounds[1]+1, $tx+$bounds[2]/2, $ty+$bounds[5]-1, $tbcolor);
					}

					$img->stringFT($tcolor, $fontname, $fontsize, 0, $tx-$bounds[2]/2,$ty, $label);
				}
			}
		}
	}

	if ($Watermark ne '' && $layers =~ /filled/) {
		my $fgcolor = $img->colorAllocate(255, 255, 255);
		$img->stringFT($fgcolor, $fontname, 8, 0, 20,20, $Watermark);
	}
	
	if (get_param_ci('FORMAT') eq "image/gif") {
		$img->trueColorToPalette();
	}

	return ($img, $nshapes);
}


sub Project {
	my ($LONG, $LAT, $long1, $lat1, $long2, $lat2, $width, $height) = @_;

	if ($srs eq 'EPSG:4326') {
		return (int(($LONG-$long1)/($long2-$long1)*$width + .5), int($height - ($LAT-$lat1)/($lat2-$lat1)*$height + .5));
	}

	my ($X, $Y) = Project2($LONG, $LAT);
	my ($x1, $y1) = Project2($long1, $lat1);
	my ($x2, $y2) = Project2($long2, $lat2);

	$X = ($X - $x1) / ($x2 - $x1) * $width;
	$Y = $height - ($Y - $y1) / ($y2 - $y1) * $height;
	
	return (int($X), int($Y));
}

sub Project2 {
	my ($LONG, $LAT) = @_;

	my $pi = '3.1415926535898';
	my $os = 2 * $pi * 6378137 / 2.0;
	$X = $LONG * $os / 180.0;
	$Y = log( tan((90 + $LAT) * $pi / 360.0 )) / ($pi / 180.0);
	$Y = $Y * $os / 180.0;
	
	return ($X, $Y);
}

sub Clean {
	my $x = shift;
	$x =~ s/[^A-Za-z0-9\-_.]//g;
	return $x;
}

sub gmapTileBounds {
	my ($x, $y, $zoom) = @_;
	
	my $dlong = 360 / 2**$zoom;
	my $long1 = -180 + $x * $dlong;
	my $long2 = $long1 + $dlong;

	my $lat2 = (2 * atan2 ( exp( (.5 - $y/(2**$zoom)) * (3.1415926535898*2) ), 1) - 3.1415926535898/2) / (3.1415926535898/180);
	my $lat1 = (2 * atan2 ( exp( (.5 - ($y+1)/(2**$zoom)) * (3.1415926535898*2) ), 1) - 3.1415926535898/2) / (3.1415926535898/180);
	
	return ($lat2, $long1, $lat1, $long2);
}

sub gmapLatLonToTile {
	# Determine the zoom level and tile coordinate for the requested
	# tile, on the assumption that the request is coming from something
	# like Google Maps that only requests tiles like it does.
	my ($lat1, $long1, $long2) = @_;
	my $dl = $long2 - $long1;
	
	# To get the zoom level, take the log2 of the number of times
	# the longitude width divides 360 degrees. i.e. if it divides
	# it once ($dl == 360) then the zoom level is log(1) = 0.
	# If it divides it four times ($dl == 90), then the zoom level
	# is log2(4) == log(4)/log(2) == 2.
	my $zoomlevel = int(log(360 / $dl) / log(2) + .5);
	
	if ($zoomlevel == 0) { return (0,0,0); }
	
	# To get the x coordinate, it's just the location of the left
	# edge, measured from -180 degrees.
	my $x = int(($long1 + 180) / $dl + .5);
	
	# The y coordinate is much more complicated since the tiles
	# are not evenly spaced in latitude coordinates and don't even
	# go all the way to the poles. This gets us a value from -.5 to .5...
	my $y = log(tan(($lat1 * 3.1415926535898/180 + 3.1415926535898/2) / 2)) / (3.1415926535898*2);
	$y = int((.5 - $y) * (2 ** $zoomlevel) + .5);
	
	# Sanity check results.
	if ($zoomlevel < 0 || $zoomlevel > 20
		|| $x < 0 || $x >= (2**$zoomlevel)
		|| $y < 0 || $y >= (2**$zoomlevel)) {
		die "$zoomlevel $x $y";
	}
	
	return ($zoomlevel, $x, $y);
}
