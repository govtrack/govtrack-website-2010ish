#!/usr/bin/perl

use CGI;
use DBSQL;

use WmsConfig;

my $layer = CGI::param('layer');

DBSQL::Open($WmsConfig::DATABASE, $WmsConfig::DATABASE_USER, $WmsConfig::DATABASE_PASSWORD) if (!DBSQL::IsOpen());

my ($shorttitle, $title, $description, $author, $email, $link)
	 = DBSQL::SelectFirst(wmslayers, [shorttitle, title, description, author, email, 'link'],
	 	[DBSQL::SpecEQ(styleset, $layer)]);

if (!defined($title)) {
	print <<EOF;
Status: 404

No layer exists with that name or no layer name given.
EOF
	exit(0);
}

$layer = CGI::escapeHTML($layer);	
$shorttitle = CGI::escapeHTML($shorttitle);
$title = CGI::escapeHTML($title);
$description = CGI::escapeHTML($description);
$author = CGI::escapeHTML($author);
$email = CGI::escapeHTML($email);
	 	
print <<EOF;
Content-type: text/xml

<?xml version="1.0" encoding="UTF-8"?>
<Module>
	<ModulePrefs
		title="$title" 
		description="$description"
		author="$author"
		author_email="$email"
		title_url="$link"
		screenshot="http://www.govtrack.us/perl/wms/gmapplet_screenshot.png"
		thumbnail="http://www.govtrack.us/perl/wms/gmapplet_thumbnail.png"
		height="150">

		<Require feature="sharedmap"/>

	</ModulePrefs>

	<Content type="html">
	
	<![CDATA[
	<!--<p>Click on a location for more information about it.</p>-->
	
	<p>Unfortunately this Google My Maps map is no longer available.
	Google has made some changes that cause our maps to not work
	properly here. Instead, please use GovTrack's
	<a href="http://www.govtrack.us/congress/findyourreps.xpd" target="_top">Congressional
	District Maps</a> page.</p>

	<div id="wmsmappletinfo">
	</div>
	
<script src="http://www.govtrack.us/scripts/jsr_class.js"></script>
<script src="http://www.govtrack.us/scripts/ajax.js"></script>

<script>
function addLayer(layer_name, map) {
	var tilelayer = new GTileLayer(null, null, null, {
		tileUrlTemplate: "http://www.govtrack.us/perl/wms/wms.cgi?LAYERS=" + layer_name + "\&FORMAT=image/gif\&google_tile_template_values={X},{Y},{Z}",
		isPng: false,
		opacity: 0.5 });
	tilelayer.getOpacity = function() { return 0.5; };
	map.addOverlay(new GTileLayerOverlay(tilelayer));
}

var map = new GMap2();

//addLayer("$layer", map);

GEvent.addListener(map, "click", function (overlay, point) {
	if (point) {
		var url = 'http://www.govtrack.us/perl/wms/get-region.cgi?layer=$layer\&lat=' + point.lat() + '&long=' + point.lng() + '&format=json&json_callback=wms_info_callback';
		req = new JSONscriptRequest(url);
		req.buildScriptTag();
		req.addScriptTag();
	}
});	

function wms_info_callback(wms_listing) {
	if (wms_listing.length == 0) { return; }

	var item = wms_listing[wms_listing.length-1];
	uri = item[0];
	SetInnerHtml('wmsmappletinfo', 'Loading information...');
	_IG_FetchContent("http://www.govtrack.us/perl/local-info.cgi?quick=1\&uri=" + uri,
		function(data) {
			SetInnerHtml('wmsmappletinfo', data);
		});
}



</script>
		
	]]>
	</Content>
</Module>
EOF
