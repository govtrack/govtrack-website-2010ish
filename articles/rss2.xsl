<?xml version="1.0" ?>
<?post-process href="../style/master.xsl" type="text/xsl" ?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0"
	xmlns:govtrack-util="assembly://GovTrackWeb/GovTrack.Web.Util"
	xmlns:dc="http://purl.org/dc/elements/1.1/">
<xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>

<xsl:template match="/">
	<Page>
		<Title>Who's Talking About GovTrack</Title>
		<Body>
		<div class="PageTitle">Who's talking about GovTrack?</div>
		<xsl:apply-templates/>
		</Body>
	</Page>
</xsl:template>


<xsl:template match="/rss/channel">
<p>
<a><xsl:attribute name="href"><xsl:value-of select="link"/></xsl:attribute>
<xsl:value-of select="title"/></a>
</p>
<p>
<xsl:value-of select="lastBuildDate"/>
</p>
<dl>
<xsl:for-each select="item">
    <dt>
        <a><xsl:attribute name="href"><xsl:value-of select="link"/></xsl:attribute>
        <xsl:value-of select="title"/></a>
    </dt>
    <dd>
        <!--<xsl:value-of select="govtrack-util:RemoveHTMLTags(description)"/>-->
        <xsl:value-of select="govtrack-util:RemoveHTMLTags(description)" disable-output-escaping="yes"/>
        (<xsl:value-of select="substring-before(dc:date,'T')"/>)
    </dd>
</xsl:for-each>
</dl>
<p>
<xsl:value-of select="copyright"/>
</p>
</xsl:template>
</xsl:stylesheet>
