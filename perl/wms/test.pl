use SphericalPolygonArea;

print SphericalPolygonArea::GetArea(
	[-73.56, 40.78], [-73.50,40.79], [-73.50,40.76], [-73.54,40.73]
	) * $SphericalPolygonArea::EarthSqMiPerSphericalDegree . "\n";

