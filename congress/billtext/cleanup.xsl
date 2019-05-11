<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

<!-- HTML cleanup, first pass -->

<xsl:template match="p|h1|h2|h3|h4|h5|h6|center|pre|blockquote" mode="cleanup">
    <xsl:choose>
        <!-- ignore empty spacing elements -->
        <xsl:when test="not(child::node())">
        </xsl:when>
        <!-- ignore block level elements that are around other block level elements. lots of <p><h3>...</h3></p> etc. -->
        <!-- modified so not triggered by illegal structure in removed sections for now -->
        <!--  <xsl:when test="descendant::p or descendant::h1 or descendant::h2 or descendant::h3 or descendant::h4 or 
                        descendant::h5 or descendant::h6 or descendant::center or descendant::pre or descendant::blockquote)">-->
        <xsl:when test="descendant::*[self::p or self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6 or 
                        self::center or self::pre or self::blockquote][not(ancestor::removed or ancestor::changed-from)]">
            <xsl:apply-templates mode="cleanup"/>
        </xsl:when>
        <xsl:otherwise>
            <xsl:call-template name="makep"/>
        </xsl:otherwise>
    </xsl:choose>
</xsl:template>

<xsl:template match="ul" mode="cleanup">
    <xsl:choose>
        <!-- add a p if it is missing. assumes if there is not a block element, it is just one big jumble.
             not sure if this assumption is reasonable, though it fits all the examples i've seen. -->
        <xsl:when test="not(descendant::p or descendant::h1 or descendant::h2 or descendant::h3 or descendant::h4 or 
                            descendant::h5 or descendant::h6 or descendant::center or descendant::pre or descendant::blockquote)"> 
            <ul><xsl:call-template name="makep"/></ul>
        </xsl:when>
        <xsl:otherwise>
            <ul><xsl:apply-templates mode="cleanup"/></ul>
        </xsl:otherwise>
    </xsl:choose>
</xsl:template>

<xsl:template name="makep" mode="cleanup">
    <p>
        <xsl:copy-of select="@*"/>
        <xsl:attribute name="style">
            <xsl:if test="ancestor-or-self::center">text-align:center;</xsl:if>
            <xsl:if test="ancestor-or-self::h1 or ancestor-or-self::h2 or ancestor-or-self::h3 or ancestor-or-self::h4 or 
                          ancestor-or-self::h5 or ancestor-or-self::h6 or ancestor::strong or ancestor::b">font-weight:bold;font-size:112%;</xsl:if>
            <xsl:if test="ancestor::em or ancestor::i">font-style:italic;</xsl:if>
            <!-- could set monospaced for pre here --> 
        </xsl:attribute>

        <!-- for determining section identifiers, we ignore changed-from and removed parts -->                
        <xsl:variable name="textval_"><xsl:copy-of select="descendant-or-self::text()[not(ancestor::changed-from or ancestor::removed)]"/></xsl:variable>
        <xsl:variable name="textval" select="normalize-space($textval_)"/>
        <xsl:choose>
            <xsl:when test="starts-with($textval, 'SEC. ')">
                <xsl:attribute name="id">sec<xsl:value-of select="substring-before(substring-after($textval, 'SEC. '),'. ')"/></xsl:attribute>
                <xsl:apply-templates mode="cleanup"/>
            </xsl:when>
            <xsl:when test="starts-with($textval, 'SECTION ')">
                <xsl:attribute name="id">sec<xsl:value-of select="substring-before(substring-after($textval, 'SECTION '),'. ')"/></xsl:attribute>
                <xsl:apply-templates mode="cleanup"/>
            </xsl:when>
            <xsl:when test="starts-with($textval, 'Sec. ') and substring-before(substring-after($textval, 'Sec. '),'. ') and
                            not(contains(substring-before(substring-after($textval, 'Sec. '),'. '), ' '))">
                <a class="toclink">
                    <xsl:attribute name="rel">sec<xsl:value-of select="substring-before(substring-after($textval, 'Sec. '),'. ')"/></xsl:attribute>
                    <xsl:apply-templates mode="cleanup"/>
                </a>
            </xsl:when>
            <xsl:when test="starts-with($textval, 'Section ') and substring-before(substring-after($textval, 'Section '),'. ') and
                            not(contains(substring-before(substring-after($textval, 'Section '),'. '), ' '))">
                <a class="toclink">
                    <xsl:attribute name="rel">sec<xsl:value-of select="substring-before(substring-after($textval, 'Section '),'. ')"/></xsl:attribute>
                    <xsl:apply-templates mode="cleanup"/>
                </a>
            </xsl:when>
            <xsl:when test="starts-with($textval, '`') or starts-with($textval, '&#8216;')">
                <xsl:attribute name="class">quote</xsl:attribute>
                <xsl:apply-templates mode="cleanup"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:apply-templates mode="cleanup"/>
            </xsl:otherwise>
        </xsl:choose>
    </p>
</xsl:template>

<xsl:template match="em|i|strong|b" mode="cleanup">
    <xsl:choose>
        <!-- ignore style elements that are outside block elements -->
        <xsl:when test="descendant::p or descendant::h1 or descendant::h2 or descendant::h3 or descendant::h4 or 
                        descendant::h5 or descendant::h6 or descendant::center or descendant::pre or descendant::blockquote">
            <xsl:apply-templates mode="cleanup"/>
        </xsl:when>
        <xsl:otherwise>
            <xsl:call-template name="cleanup_copy"/>
        </xsl:otherwise>
    </xsl:choose> 
</xsl:template>

<xsl:template match="@*|node()" name="cleanup_copy" mode="cleanup">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()" mode="cleanup"/>
    </xsl:copy>
</xsl:template>

<!-- temporary, fixes the messed-up html caused by the tags in remove and changed-from -->
<xsl:template match="removed|changed-from" mode="cleanup">
    <xsl:copy>
      <xsl:choose>
      <xsl:when test="ancestor::p or ancestor::h1 or ancestor::h2 or ancestor::h3 or ancestor::h4 or 
                      ancestor::h5 or ancestor::h6 or ancestor::center or ancestor::pre or ancestor::blockquote">
          <xsl:call-template name="strip_removed"/>
      </xsl:when>
      <xsl:otherwise>
          <p><xsl:call-template name="strip_removed"/></p>
      </xsl:otherwise>
      </xsl:choose>
    </xsl:copy>    
</xsl:template>

<xsl:template name="strip_removed" mode="cleanup">
    <xsl:for-each select="descendant::text()">
        <xsl:if test="not(position()=1)">&#160;</xsl:if>
        <xsl:value-of select="."/>
    </xsl:for-each>
</xsl:template>

</xsl:stylesheet>
