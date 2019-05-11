WMS Server

This is a work in progress.

Requirements:
* Perl, run as CGI or better under mod_perl
* MySQL

0) Prerequisites.

   Go into the triangle directory, delete the file 'triangle'
   if it's there already, and then run 'make' to compile this program.
   This auxiliary program meshes polygons which is used to find
   good places to put markers within polygons.
   
   Run wms.cgi from the command line. Install any missing modules
   until you get an error about an invalid BBOX, which for now is good.

1) Set up the database configuration by editing WmsConfig.pm.
   Enter the database name and the database username and password.
   The tables wmsidentifiers, wmsgeometry, and wmsstyles, and
   wmscache will be created automatically later on.

2) Initialize the geospacial data table by running:
	 perl init-wms-geometry.pl

3) Initialize the layer styling table by running:
	 perl init-wms-styles.pl
   
4) Initialize the tile cache table by running
     perl init-wms-cache.pl
   This can be run again in the future to clear out the cache
   and recreate the table.

5) Load in geospacial data.

     If the shapefile contains coordinates in something other than lat/long
     (see the .prj file), then the SOURCE_PROJECTION environment variable must be set
     following the Proj4 documentation (http://trac.osgeo.org/proj/wiki/GenParms).
     Most shapefiles that I've seen use lat/long so you'd skip this part.
     
     export SOURCE_PROJECTION="+proj=lcc +x_0=1500000 +y_0=5000000 +lon_0=-100 +lat_1=27.5 +lat_2=35 +lat_0=18 +units=m +datum=NAD83" 
     
     Then run:

     perl load-shapefile.pl txschooldistricts Districts_08_09.zip | more
     
     This is going to dump out a list of fields for each shape in the
     shapefile. Choose one that will serve as the unique identifier
     for each district. Then rerun the script passing that key as
     the next parameter.
     
     You can also choose one of these fields to use as the polygon labels
     on the maps. Pass that as the next parameter.
     
     perl load-shapefile.pl txschooldistricts Districts_08_09.zip DISTRICT NAME

     That first argument "txschooldistricts" is a key used to identify
     the layer name, which you'll need to put into the HTML page's
     map script.

6)   Try it.

     First try executing wms.cgi from the command line to make sure it runs:

     perl wms.cgi LAYERS=txschooldistricts google_tile_template_values=0,0,0

     If that ends with "Finished", continue...
     
     Next try making it go as a regular CGI script. Make it executable
     first:

     chmod 755 wms.cgi

     Then visit in your browser:

     http://yoursite.com/cgi/wmsfiles/wms.cgi?LAYERS=txschooldistricts&google_tile_template_values=1,3,3
     
     If you don't get a 500 error, you are probably ok. The script
     will return a 404 (not found) if you request a map file that
     doesn't have any shapes in it, so beware that that doesn't mean
     you've sent your browser to the wrong address. In the URL
     above, the test values are tile x, tile y, and zoom level. You
     can guess good values using Google's example in the section
     "Tile Coordinates" here:
     http://code.google.com/apis/maps/documentation/overlays.html

7)   Update the example_googlemaps.html page with your Google Maps
     API key and the layer name you gave above ("txschooldistricts")
     in the addLayer call. Then test it out on that page:
     
     http://yoursite.com/cgi/wmsfiles/example_googlemaps.html
     
     Or try revising the openlayers example.

8)   It's much faster when it's run under mod_perl rather than as
     straight CGI. But I'll leave instructions for that for later.
