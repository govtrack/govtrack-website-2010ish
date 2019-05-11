<?xml version="1.0" encoding="UTF-8"?>
<?post-process href="../style/master.xsl" type="text/xsl" ?>

<xsl:stylesheet
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	version="1.0">

	<xsl:template match="/Article">
  	<Page>
  		<AllowEmail/>
  	
  		<Title><xsl:value-of select="Title"/></Title>
	
		<Body>
			<div class="PageTitle" style="margin-top: .5em; margin-right: 3em">
	 			<xsl:value-of select="Title"/>
	 		</div>
	 		
	 		<p>
				<div>
					<xsl:if test="Author/@Contact">
						<a href="{Author/@Contact}"><xsl:apply-templates select="Author"/></a>
					</xsl:if>
					<xsl:if test="count(Author/@Contact)=0">
						<xsl:apply-templates select="Author"/>
					</xsl:if>
				</div>
					
				<div><xsl:apply-templates select="Date"/></div>
	 		</p>
	 		
	 		<xsl:if test="Logo">
	 			<center><img src="{Logo}"/></center>
	 		</xsl:if>
	 		
	 		<p style="font-style: italic">
				<xsl:apply-templates select="Abstract"/>
	 		</p>
	 		
	 		<center><hr width="80%"/></center>
	 		
 			<xsl:apply-templates select="Body" mode="toc"/>
 			
	 		<center><hr width="80%"/></center>
		
			<div class="welcome">
			<xsl:apply-templates select="Body"/>
			</div>
		</Body>
	</Page>
	</xsl:template>
	
	<xsl:template match="*" mode="toc">
		<xsl:if test="count(section) &gt; 0">
		<ol>
			<xsl:for-each select="section">
				<li>
					<a href="#{@title}">
						<xsl:value-of select="@title"/>
					</a>
					<xsl:apply-templates select="." mode="toc"/>
	 			</li>
			</xsl:for-each>
		</ol>
		</xsl:if>
	</xsl:template>	
	
	<xsl:template match="section">
		<a name="{.}"/>
		<xsl:choose>
		<xsl:when test="count(ancestor::section)=0">
			<h2><xsl:value-of select="@title"/></h2>
		</xsl:when>
		<xsl:otherwise>
			<h3><xsl:value-of select="@title"/></h3>
		</xsl:otherwise>
		</xsl:choose>
		
		<xsl:apply-templates/>
	</xsl:template>

	<xsl:template match="p">
		<p style="margin-right: 3em">
			<xsl:apply-templates/>
		</p>
	</xsl:template>
	
	<xsl:template match="figure">
		<div style="text-align: center; margin: 1em; padding: .5em; border: thin solid #9999DD">
			<div><img src="{@source}"/></div>
			<div><xsl:value-of select="@caption"/></div>
		</div>
	</xsl:template>

	<xsl:template match="linkout">
		<div style="margin: 1em; padding: .5em; border: thin solid #9999DD">
			<xsl:apply-templates/>
		</div>
	</xsl:template>
  
	<xsl:template match="code">
		<div style="margin: 1em; background-color: #FFFFCC; border: thin solid #EEEEAA; padding-left: .5em; padding-right: .5em">
		<div style="font-weight: bold; padding-top: .5em; border-bottom: thin solid #EEEEAA; font-size: 90%"><xsl:value-of select="@title"/></div>
		<!--<div style="white-space: pre; font-family: monospace; ">-->
		<pre>
			<xsl:apply-templates/>
		</pre>
		</div>
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
