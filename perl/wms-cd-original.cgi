#!/usr/bin/perl

use CGI;
use GD;
use Geo::ShapeFile;
use Data::Serializer;

$CGI::DISABLE_UPLOADS = 1;

# next line required when running in mod_perl so
# it knows where we are
push @INC, "/home/govtrack/website/perl";
require "sql.pl";

# General Parameters
$CacheDir = "/home/govtrack/website/media/gmap-overlays";
$Watermark = "GovTrack.us";
$CacheTime = 6000; # seconds
$RefreshOnScriptChange = 0; # not sure it's working with mod_perl
$Session = 110;

# for the watermark and debugging
$fontname = '/usr/share/fonts/bitstream-vera/Vera.ttf';
$fontsize = 8;

# important parameters:
#    LAYERS, STYLES
#    BBOX (-84.375,31.95216223802497,-78.75,36.59788913307022) i.e. (bottom left, top right)
#    WIDTH, HEIGHT

# Load the parameters of the image to output

$layers = CGI::param('LAYERS');
$styles = CGI::param('STYLES');
$srs = CGI::param('SRS');

($long1, $lat1, $long2, $lat2) = split(/,/, CGI::param('BBOX'));
$width = CGI::param('WIDTH');
$height = CGI::param('HEIGHT');

# Sanity checks
if ($long1 >= $long2) { die "Invalid BBOX"; }
if ($lat1 >= $lat2) { die "Invalid BBOX"; }
if ($width < 10) { die "Invalid image dimension"; }
if ($height < 10) { die "Invalid image dimension"; }
if ($width > 1024) { die "Invalid image dimension"; }
if ($height > 1024) { die "Invalid image dimension"; }

$debug = ($ENV{SERVER_NAME} eq '');

$info = <<EOF;
BBOX: ($long1,$lat1)-($long2,$lat2)
DIMS: $width x $height
LAYERS/STYLES: $layers | $styles
EOF
if ($debug) { print STDERR $info; }

$img = LoadTile($long1, $lat1, $long2, $lat2, $width, $height, $layers, $styles);
if (!defined($img)) {
	print "Status: 404\n\n";
	exit(0);
}

# Draw some debugging info onto the tile?
if (0) {
	$fgcolor = $img->colorAllocate(255, 100, 100);
	$img->rectangle(0,0, $width-1,$height-1, $fgcolor);
	$img->stringFT($fgcolor, $fontname, $fontsize, 0, 10,10, $info);
}

#if (CGI::referer() =~ /lawsonforcongress/) {
#	$fgcolor = $img->colorAllocate(255, 50, 50);
#	$img->stringFT($fgcolor, $fontname, $fontsize, 0, 10,10, "Use of these maps does not comply");
#	$img->stringFT($fgcolor, $fontname, $fontsize, 0, 10,20, "with GovTrack.us's TOS.");
#}

if ($debug) {
	print STDERR "Finished.\n";
	exit(0);
}

# Output the image

my $data = $img->gif;
my $datalen = length($data);

binmode STDOUT;

print <<EOF;
Content-Type: image/gif
Content-Length: $datalen
Cache-Control: max-age: $CacheTime, public

EOF
print $data;

# That's it.

sub LoadTile {
	my ($long1, $lat1, $long2, $lat2, $width, $height, $layers, $styles) = @_;

	my $filename;
	if (defined($CacheDir)) {
		$filename = "$CacheDir/$layer";
		for $x ($long1, $lat1, $long2, $lat2, $width, $height) {
			$filename .= "," . 1*$x; # double check $x is numeric by multiplying by 1
		}
		for $x ($layers, $styles) {
			$filename .= "," . Clean($x);
		}
		$filename .= ".gif";
	}
	
	# Don't cache these.
	if ($layers =~ /state=/ || $layers =~ /district=/ || $srs eq 'EPSG:4326') { undef $filename; }
	
	if (!defined($filename) || !-e $filename || -z $filename || ($RefreshOnScriptChange && -M $filename > -M $ENV{SCRIPT_FILENAME})) {
		my ($img, $nshapes) = DrawTile($long1, $lat1, $long2, $lat2, $width, $height, $layers, $styles);
		if ($nshapes > 5 && defined($filename)) {
			open OUT, ">$filename";
			flock OUT, 2;
			binmode OUT;
			print OUT $img->gif;
			flock OUT, 0;
			close OUT;
		}
		return $img;
	}

	return GD::Image->newFromGif($filename)
}


sub DrawTile {
	my ($long1, $lat1, $long2, $lat2, $width, $height, $layers, $styles) = @_;

	my $img = new GD::Image($width, $height);

	my $white = $img->colorAllocate(255, 255, 255);
	my $black = $img->colorAllocate(0, 0, 255);

	$img->transparent($white);
	$img->setThickness(3);
	#$img->filledRectangle(0,0, $width,$height, $white);

	#$img->stringFT($fgcolor, $fontname, $fontsize, 0, $img->width/3,$img->height/3, CGI::param('BBOX'));

	eval { DBOpen("govtrack", "govtrack", undef); };
	
	# If we're supposed to show just one district,
	# then hide others.
	my @specs;
	if ($layers =~ /state=(\w\w)/) {
		@specs = (DBSpecEQ(state, $1));
	} elsif ($layers =~ /district=(\w\w)(\d+)/) {
		@specs = (DBSpecEQ(state, $1), DBSpecEQ(district, $2));
	}
		
	my @districts;
	
	eval {
		@districts = DBSelect(cdgeometry, ["state", "district", "polygon"],
			[DBSpecLE(long_min, $long2), DBSpecGE(long_max, $long1),
			DBSpecLE(lat_min, $lat2), DBSpecGE(lat_max, $lat1),
			DBSpecEQ(session, $Session),
			@specs]);
	};
		
	DBClose();
	
	## If there are fewer than some number of polygons in this box,
	## then load the full-resolution polygons.
	#if (scalar(@districts) > 0 && scalar(@districts) < 15) {
	#	@districts = DBSelect(cdgeometry, ["state", "district", "polygon"],
	#		[DBSpecLE(long_min, $long2), DBSpecGE(long_max, $long1),
	#		DBSpecLE(lat_min, $lat2), DBSpecGE(lat_max, $lat1),
	#		DBSpecEQ(session, $Session)]);
	#}
		
	my $nshapes;

	my $serializer = Data::Serializer->new();

	foreach my $rec (@districts) {
		my ($state, $dist, $poly) = @$rec;
		
		$nshapes++;
		
		my $d = (ord($state) % 60) + $dist;
		
		my @clrcycle = (0, 255, 127);
		my @clr = ($d % 3, ($d/3 + 1) % 3, ($d/3/3 + 2) % 3);

		if ($clr[0] == $clr[1] && $clr[1] == $clr[2] && $clr[0] == 1) {
			$clr[$d % 3]--;
		}

		my $fgcolor = $img->colorAllocate($clrcycle[$clr[0]], $clrcycle[$clr[1]], $clrcycle[$clr[2]]);

		my $polygon = $serializer->deserialize($poly);

			my $gdpoly = new GD::Polygon;
			my ($lastx, $lasty) = (undef, undef);
			foreach my $pt (@$polygon) {
				my ($x, $y) = @$pt;
				
				# Cull points too close to the last point
				if ($lastx && abs($x-$lastx) < ($long2-$long1)/$width*3
					&& abs($y-$lasty) < ($lat2-$lat1)/$height*3) {
					next;
				}

				$gdpoly->addPt(Project($x, $y, $long1, $lat1, $long2, $lat2, $width, $height));
				($lastx, $lasty) = ($x, $y);
			}
			$img->filledPolygon($gdpoly, $fgcolor) if ($layers =~ /filled/);
			
			#$img->setAntiAliased($black);
			$img->openPolygon($gdpoly, $black) if ($layers =~ /outline/);
	}

	if ($Watermark ne '' && $layers =~ /filled/) {
		my $fgcolor = $img->colorAllocate(255, 255, 255);
		$img->stringFT($fgcolor, $fontname, $fontsize, 0, 20,20, $Watermark);
	}
	
	return ($img, $nshapes);
}


sub Project {
	my ($LONG, $LAT, $long1, $lat1, $long2, $lat2, $width, $height) = @_;

	if ($srs eq 'EPSG:4326') {
		return (int(($LONG-$long1)/($long2-$long1)*$width + .5), int($height - ($LAT-$lat1)/($lat2-$lat1)*$height + .5));
	}

	my ($X, $Y) = Mercator($LONG, $LAT, $long1, $lat1, $long2, $lat2, $width, $height);
	my ($x1, $y1) = Mercator($long1, $lat1, $long1, $lat1, $long2, $lat2, $width, $height);
	my ($x2, $y2) = Mercator($long2, $lat2, $long1, $lat1, $long2, $lat2, $width, $height);

	$X = ($X - $x1) / ($x2 - $x1) * $width;
	$Y = $height - ($Y - $y1) / ($y2 - $y1) * $height;

	return ($X, $Y);
}

sub Mercator {
	my ($LONG, $LAT, $long1, $lat1, $long2, $lat2, $width, $height) = @_;
	my $r = $width / ($long2-$long1) * 360 / 2 / 3.1415926;
	my $X = $LONG * 3.141593 / 180 * $r;
	my $Y = $r / 2 * log( (1+sin($LAT*3.14159/180))/(1-sin($LAT*3.14159/180)) );
	return ($X, $Y);
}

sub RoundDown {
	# Round down $a to the nearest multiple of $b.
	my ($a, $b) = @_;
	my $c = int($a / $b) * $b;
	if ($a < 0 && $c != $a) { $c -= $b; }
	return $c;
}

sub Clean {
	my $x = shift;
	$x =~ s/[^A-Za-z]//g;
	return $x;
}
