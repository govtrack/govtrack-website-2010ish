# Perl module by Joshua Tauberer <http://razor.occams.info>,
# adapted from the code cited below. To the extent I have any
# copyright claims to the Perl port of the routine, I release
# my contribution into the public domain.
# 2008-04-15
#############################################################

# ANSI C code from the article
# "Computing the Area of a Spherical Polygon"
# by Robert D. Miller
# in "Graphics Gems IV", Academic Press, 1994

package SphericalPolygonArea;

use strict;

use Math::Trig;

# Square mi and km per spherical degree for the Earth.
$SphericalPolygonArea::EarthSqMiPerSphericalDegree = 273218.4;
$SphericalPolygonArea::EarthSqKmPerSphericalDegree = 707632.4;

my $HalfPi = 1.5707963267948966192313;
my $Degree = 57.295779513082320876798;	# degrees per radian

#double Hav(double X)
#  Haversine function: hav(x)= (1-cos(x))/2.
sub Hav {
    return (1.0 - cos($_[0])) / 2.0;
}

#double SphericalPolyArea(double *Lat, double *Lon, int N)
#  Returns the area of a spherical polygon in spherical degrees,
#  given the latitudes and longitudes.
sub GetArea {
	# Pass this subroutine the long/lat pairs as an array
	# of arrayrefs. Example:
	# $area = GetArea([-73, 45], [-70, 45], [-71, 43]);

	my $N = scalar(@_)-1;

	my ($J, $K);

	my ($Lam1, $Lam2, $Beta1, $Beta2);
	my ($CosB1, $CosB2);
	my ($Sum);

	$Sum = 0;
    for ($J = 0; $J <= $N; $J++) {
    	my $K = $J + 1;
    	if ($J == 0) {
			$Lam1 = deg2rad($_[$J][0]);	  $Beta1 = deg2rad($_[$J][1]);
			$Lam2 = deg2rad($_[$J+1][0]);  $Beta2 = deg2rad($_[$J+1][1]);
			$CosB1 = cos($Beta1); $CosB2 = cos($Beta2);
		} else {
			$K = ($J+1) % ($N+1);
			$Lam1 = $Lam2;		$Beta1 = $Beta2;
			$Lam2 = deg2rad($_[$K][0]);	$Beta2 = deg2rad($_[$K][1]);
			$CosB1 = $CosB2;	$CosB2 = cos($Beta2);
		}

		if ($Lam1 != $Lam2) {
			my $HavA = Hav($Beta2-$Beta1) + $CosB1*$CosB2*Hav($Lam2-$Lam1);
			my $A = 2*asin(sqrt($HavA));
			my $B = $HalfPi - $Beta2;
			my $C = $HalfPi - $Beta1;
			my $S = 0.5*($A+$B+$C);
			my $T = tan($S/2) * tan(($S-$A)/2) * tan(($S-$B)/2) * tan(($S-$C)/2);

			my $Excess = abs(4*atan(sqrt(abs($T))))*$Degree;
			if ($Lam2 < $Lam1) { $Excess = -$Excess; }

			$Sum = $Sum + $Excess;
		}
	}
    return abs($Sum);
}

