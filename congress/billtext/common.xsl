<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

<!--  common xsl used by plaintext.xpd and inlinetext.xpd. -->

<xsl:include href="common2.xsl"/>

<xsl:template match="body">
    <div>
        <xsl:apply-templates/>
    </div>
</xsl:template>
    
<!-- add click handlers to toc links -->
<xsl:template match="a[@class='toclink']">
    <a onclick="$.btns.click_toclink(this);">
        <xsl:apply-templates select="@*|node()"/>
    </a>
</xsl:template>

<!-- To save time on load, we set href and onclick attributes dynamically on 
     mouseover using javascript. If we set them here, it would look like this: 
        <span class="expanded" title="Collapse this section" onclick="$.btns.toggle_section(this);"/>
        <a class="extractor" title="Extract this section"
           href="/embed/sample-billtext.xpd?bill={$bill}&amp;version={$version}&amp;nid={@nid}"/>
        <a class="linker" title="Link to this section"
           href="billtext.xpd?bill={$bill}&amp;version={$version}&amp;nid={@nid}"/>
-->
<xsl:variable name="chooser">
    <div class="chooser">
        <span class="expanded" title="Collapse this section" />
        <a class="extractor" title="Extract this section" />
        <a class="linker" title="Link to this section" />
    </div>
</xsl:variable>

<!-- code to create "sections", which consist of 
     a p followed optionally by a ul. ignoring attrs other than nid and style. haschange template 
     is not in this file and indicates whether the section has any changed content. -->
<xsl:template name="make_section" match="p">
    <xsl:variable name="margin" select="count(ancestor::ul)*3"/>
    <div class="section" nid="{@nid}" onmouseover="$.btns.section_over(event,this);" onmouseout="$.btns.section_out(event,this);">
        <xsl:if test="@id"><xsl:copy-of select="@id"/></xsl:if>
        <xsl:call-template name="haschange"/>
        <xsl:copy-of select="$chooser"/>
        <p style="{@style}margin-left:{$margin}em;">
            <xsl:if test="@class"><xsl:copy-of select="@class"/></xsl:if>
            <xsl:apply-templates/>
        </p>
        <xsl:call-template name="copyul"/>
    </div>
</xsl:template>

<xsl:template name="copyul">
    <xsl:if test="following-sibling::*[1][self::ul]">
        <xsl:apply-templates select="following-sibling::*[1]/*"/>
    </xsl:if>
</xsl:template>

<!-- we only copy uls that were not copied by the copyul template. there should not be
     any such uls (except possibly at the top level), but we do this to make sure we 
     aren't leaving out any text. -->
<xsl:template match="ul">
    <xsl:if test="not(preceding-sibling::*[1][self::p])">
        <xsl:apply-templates/>
    </xsl:if>
</xsl:template>

<!--  removes the starting '`' from quotes. assumes no other classes -->
<!--  might leave empty inserted tags, though -->
<xsl:template match="p[@class='quote']//text()[not(ancestor::changed-from or ancestor::removed)][1][starts-with(normalize-space(.),'`')]">
    <xsl:value-of select="substring-after(.,'`')"/>
</xsl:template>

<!--  removes inserted sections that will be made empty my removing the '`' -->
<xsl:template match="inserted[parent::p[@class='quote']][position()=1][starts-with(normalize-space(.),'`') and not(substring-after(.,'`'))]">
</xsl:template>

<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>

</xsl:stylesheet>
