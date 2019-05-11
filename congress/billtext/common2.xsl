<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

<xsl:template match="usc-reference">
    <a target="_blank" class="usclink"
       href="http://www.law.cornell.edu/usc-cgi/newurl?type=titlesect&amp;title={@title}&amp;section={@section}"
       rel="/perl/usc-popup.cgi?ref={@title}_{@section}_{@paragraph}&amp;context_before=2&amp;context_after=4">
        <xsl:apply-templates/>
    </a>
</xsl:template>

<xsl:template match="public-law-reference">
    <a target="_blank" class="pllink"
       href="/congress/billsearch.xpd?q=P.L.+{@session}-{@number}">
        <xsl:apply-templates/>
    </a>
</xsl:template>

</xsl:stylesheet>
