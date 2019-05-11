#!/usr/bin/perl

die;

use CGI;
use GD;
use LWP::UserAgent;
#use Convert::Color;
#use Convert::Color::RGB;
#use Convert::Color::HSV;

do "/home/govtrack/website/perl/sql.pl";

################

$Session = 110;
$CacheTime = 6000; # seconds

$fontname = '/usr/share/fonts/bitstream-vera/Vera.ttf';
$fontsize = 8;

################

$State = CGI::param('state');
$District = CGI::param('district');
$Width = CGI::param('width');
$Height = CGI::param('height');
$Context = CGI::param('context');

$size = CGI::param('size');
if ($size != 0) { $Width = $size; $Height = $size; }

if ($Width < 256 || $Width > 4096 || $Height < 256 || $Height > 4096) {
	die "Invalid image size.";
}

if ($Context < 0 || $Context > 10) { $Context = 0; }

$CGI::DISABLE_UPLOADS = 1;
$UA = LWP::UserAgent->new(keep_alive => 2, timeout => 30, agent => "GovTrack.us", from => "operations@govtrack.us");

DBOpen("govtrack", "govtrack", undef);

$img = CreateImage();

$imgdata = $img->png;
$datalen = length($imgdata);

binmode STDOUT;

print <<EOF;
Content-Type: image/png
Content-Length: $datalen
Cache-Control: max-age: $CacheTime, public

EOF

print $imgdata;

DBClose();

sub CreateImage {
	my ($width, $height) = ($Width, $Height);

	my ($long_min, $long_max, $lat_min, $lat_max)
		= DBSelectFirst(cdgeometry, ["min(long_min), max(long_max), min(lat_min), max(lat_max)"],
			[DBSpecEQ(session, $Session), DBSpecEQ(state, $State), DBSpecEQ(district, $District)]);
			
	my @district_dims = ($long_max-$long_min, $lat_max-$lat_min);
	
	$long_min -= $district_dims[0] * (.15 + $Context);
	$long_max += $district_dims[0] * (.15 + $Context);
	$lat_min -= $district_dims[1] * (.15 + $Context);
	$lat_max += $district_dims[1] * (.15 + $Context);

	@district_dims = ($long_max-$long_min, $lat_max-$lat_min);
                                                    
	# Get the dimensions of the district and choose
	# an image size for it preserving aspect ratio.

	if ($district_dims[0]/$district_dims[1]*$height <= $width) {
		$width = $district_dims[0]/$district_dims[1]*$height;
	} else {
		$height = $district_dims[1]/$district_dims[0]*$width;
	}
	
	# Choose a resolution.
	
	my $res = 90;
	my $zoom = 2;
	while (1) {
		if ($res < ($long_max-$long_min)/($width/256/1.5)) { last; }
		$res /= 2;
		$zoom++;
	}
	
	# Now adjust the image size so that we can fit 256x256px
	# squares perfectly within the image.
	my $newwidth = ($long_max-$long_min)/$res*256;
	$height = $newwidth * $height/$width;
	$width = $newwidth;
	
	# Prepare the GD image.

	my $img = new GD::Image($width, $height, 1);

	my $white = $img->colorAllocate(255, 255, 255);
	my $black = $img->colorAllocate(0, 0, 0);
	my $grey = $img->colorAllocate(170, 150, 150);

	#$img->transparent($white);
	$img->filledRectangle(0,0, $width,$height, $white);

	# Draw the layers.
	
	#$img->stringFT($black, $fontname, $fontsize, 0, $img->width/3,$img->height/3,
	#	$res);
	
	for my $serverdata (
		['http://tile.openstreetmap.org/', 'png', 100, 'tile'],
		['http://www.govtrack.us/perl/wms/wms.cgi?LAYERS=cd-110:http://www.rdfabout.com/rdf/usgov/geo/us/' . $State . '/cd/110/' . $District . '&WIDTH=256&HEIGHT=256&SRS=EPSG:4326&FORMAT=image/png&', 'png', 20, 'wms'],
		) {
		for (my $long = -180; $long < 0; $long += $res) {
			my $long2 = $long + $res;
			if ($long > $long_max || $long2 < $long_min) { next; }
			for (my $lat = 0; $lat < 80; ) {
				my $lat2;
				
				my $url = $$serverdata[0];
				if ($$serverdata[3] eq 'wms') {
					$lat2 = $lat + $res;
					$url .= "BBOX=$long,$lat,$long2,$lat2";
				} else {
					# Get tile number below where we want.
					use Math::Trig;
					my $xtile = int( ($long+180)/360 *2**$zoom ) ;
					my $ytile = int( (1 - log(tan($lat*pi/180) + sec($lat*pi/180))/pi)/2 *2**$zoom )+1;
					
					# Go up a tile.
					$ytile--;
					
					$url .= "$zoom/$xtile/$ytile.png";
					
					# Get latitude at the top of this tile.
					$lat2 = atan(sinh(pi * (1 - 2 * $ytile / 2**$zoom))) * 180/pi;
				}
				
				if ($long > $long_max || $long2 < $long_min || $lat > $lat_max || $lat2 < $lat_min) { $lat = $lat2; next; }
				
				#print STDERR "$url\n";
				my $response = $UA->get($url);
			    if (!$response->is_success) { die "$url: " . $response->code . " " . $response->message; }
		    
				my $tile;
				
				if ($$serverdata[1] eq 'png') {
					$tile = GD::Image->newFromPngData($response->content, 1);
				} else {
					$tile = GD::Image->newFromGifData($response->content);
				}
				
				my $tg = $img;
				if ($$serverdata[2] != 100) {
					my $img_r = new GD::Image($width, $height, 1);
					$img_r->filledRectangle(0,0, $width,$height, $img_r->colorAllocate(255,255,255));
					$tg = $img_r;
				}
				
				$tg->copyResampled($tile,
					$width*($long-$long_min)/($long_max-$long_min) + .5, # dst x
					$height*($lat_max-$lat2)/($lat_max-$lat_min) + .5, # dst y
					0,0, # src x,y
					$res*$width/($long_max-$long_min) + .5, # dst width
					$res*$height/($lat_max-$lat_min) + .5, # dst height
					256,256 # source width,height
					);
					
				if ($$serverdata[2] != 100) {
					if (0) {
					$img->copyMerge($tg,
						$width*($long-$long_min)/($long_max-$long_min) + .5, # dst x
						$height*($lat_max-$lat2)/($lat_max-$lat_min) + .5, # dst y
						$width*($long-$long_min)/($long_max-$long_min) + .5, # src x
						$height*($lat_max-$lat2)/($lat_max-$lat_min) + .5, # src y
						$res*$width/($long_max-$long_min) + .5, # dst width
						$res*$height/($lat_max-$lat_min) + .5, # dst height
						$$serverdata[2]);
					} else {
					for (my $x = 0; $x < $width; $x++) {
						for (my $y = 0; $y < $height; $y++) {
							#my $dc = Convert::Color::RGB->new($img->rgb($img->getPixel($x, $y)))->as_hsv;
							#my $sc = Convert::Color::RGB->new($tg->rgb($tg->getPixel($x, $y)))->as_hsv;
							#my $c = Convert::Color::HSV->new(
							#	$sc->value*$dc->hue + (1-$sc->value)*$sc->hue,
							#	$dc->saturation,
							#	$dc->value);
							my ($dr, $dg, $db) = $img->rgb($img->getPixel($x, $y));
							my ($sr, $sg, $sb) = $tg->rgb($tg->getPixel($x, $y));
							my ($xr, $xg, $xb) = (merge($dr, $sr, $sr, $sg, $sb), merge($dg, $sg, $sr, $sg, $sb), merge($db, $sb, $sr, $sg, $sb));
							if ($xr != $dr || $xg != $dg || $xb != $db) {
								$img->setPixel($x, $y, $img->colorAllocate($xr, $xg, $xb));
							}
						}
					}
					}
				}
				
				$lat = $lat2;
			}
		}
	}

	# Write out the image.
	
	$img->stringFT($black, $fontname, $fontsize, 0, 4,14,
		"By GovTrack.us \& Open Street Map. Free reuse under CC:BY-SA.");

	return $img;
}
	
sub merge {
	my ($s, $t, $r, $g, $b) = @_;
	my $v = ($r + $g + $b)/(255*3);
	$v = sqrt($v);
	return $v*$s + (1-$v)*$t;
}
