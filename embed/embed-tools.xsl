<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0"
	xmlns:govtrack-util="assembly://GovTrackWeb/GovTrack.Web.Util"
	exclude-result-prefixes="govtrack-util">
	
	<xsl:template match="embed-config">
		<h3>Configure Your Widget</h3>

<table>
<xsl:for-each select="option[@name]">
<tr><td><xsl:value-of select="@name"/>:</td>
<td>
	<xsl:choose>
	<xsl:when test="@type='select'">
		<select onchange="{@var}=this.value; update()">
			<xsl:variable name="d" select="@default"/>
			<xsl:for-each select="govtrack-util:Split(@values,',')">
				<option><xsl:if test="$d=."><xsl:attribute name="selected">1</xsl:attribute></xsl:if><xsl:value-of select="."/></option>
			</xsl:for-each>
		</select>
	</xsl:when>
	<xsl:otherwise>
		<input value="{@default}" onkeyup="{@var}=this.value; update()" />
	</xsl:otherwise>
	</xsl:choose>
</td></tr>
</xsl:for-each>
</table>
		
		<h3>Sample and Code</h3>
		<table>
		<tr valign="top">
		<td style="padding-right: 1em">
			<p>Here's how your widget will look:</p>
			<iframe id="widgetsample" width="450" height="450">
			</iframe>
		</td>
		<td>
			<p>Copy and paste the HTML code here into your webpage:</p>
			<textarea id="widgetcode" cols="45" rows="20" wrap="virtual">
			</textarea>
		</td>
		</tr>
		</table>

		<script>
<xsl:for-each select="option">
var <xsl:value-of select="@var"/> = "<xsl:value-of select="@default"/>";</xsl:for-each>
		
function update() {
	<xsl:value-of select="script"/>

	var code = "<xsl:value-of select="govtrack-util:JSEncode(code)"/>"
		.replace(/&lt;/g, unescape("%3C")).replace(/&gt;/g, unescape("%3E")).replace(/&amp;/g, unescape("%26"));

	<xsl:for-each select="option">
	code = code.replace(/\$<xsl:value-of select="@var"/>\$/g, <xsl:value-of select="@var"/>);</xsl:for-each>

	getObj("widgetcode").value = code;
	var f = getObj("widgetsample");
	var d;
	if (f.contentDocument)
		d = f.contentDocument;
	else if (f.contentWindow)  
		d = f.contentWindow.document;  
	else
		d = f.document;
	
	/*d.open();
	d.write("<html><body>");
	//d.write(code); // works in Firefox but makes IE crash!
	d.write("</body></html>");
	d.close();*/
	
	f.src = "http://www.govtrack.us/perl/replay.cgi?data=" + escape(code);
}
		update();
		</script>
	</xsl:template>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>
	<xsl:template match="node()" mode="text">
		<xsl:apply-templates select="node()"/>
	</xsl:template>

</xsl:stylesheet>

