<?xml version="1.0" encoding="UTF-8"?>
<?post-process href="../style/master.xsl" type="text/xsl" ?>

<xsl:stylesheet
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	version="1.0">

	<xsl:template match="/Article">
  	<Page HideAds="1">
  		<AllowEmail/>
  	
  		<Title><xsl:value-of select="Title"/></Title>
	
  		<Breadcrumbs><a href="/about.xpd">About GovTrack</a> &gt; <a href="/articles">Media Coverage of GovTrack</a></Breadcrumbs>
  		
		<Body-A>
			<h1>
	 			<xsl:value-of select="Title"/>
	 		</h1>
	 		<p>
				<div>
					<xsl:if test="Author/@Contact">
						<a href="{Author/@Contact}"><xsl:apply-templates select="Author"/></a>
					</xsl:if>
					<xsl:if test="count(Author/@Contact)=0">
						<xsl:apply-templates select="Author"/>
					</xsl:if>
					<xsl:if test="count(Pub)&gt;0">
						-- <xsl:apply-templates select="Pub"/>
					</xsl:if>
				</div>
					
				<div><xsl:apply-templates select="Date"/></div>
	 		</p>
	 		
	 	</Body-A>
	 	
	 	<Body-B>
	 		<xsl:if test="Logo">
	 			<center><img src="{Logo}"/></center>
	 		</xsl:if>
	 		
	 		<p style="font-style: italic">
				<xsl:apply-templates select="Abstract"/>
	 		</p>
	 		
	 		<center><hr width="80%"/></center>
	 		
	 		<xsl:if test="count(Body/section) &gt; 0">
	 			<ol>
	 				<xsl:for-each select="Body/section">
	 					<li>
	 						<a href="#{.}">
	 							<xsl:value-of select="."/>
	 						</a>
	 						<xsl:variable name="tag" select="following::*[position()=1][name()='tagline']"/>
	 						<xsl:if test="$tag">
	 							<div style="width: 75%"><xsl:value-of select="$tag"/></div>
	 						</xsl:if>
	 					</li>
	 				</xsl:for-each>
	 			</ol>
		 		<center><hr width="80%"/></center>
	 		</xsl:if>
		
			<div class="welcome" style="line-height: 150%; font-size: 100%">
			<xsl:apply-templates select="Body"/>
			</div>
		</Body-B>
	</Page>
	</xsl:template>
	
	<xsl:template match="section">
		<a name="{.}"/>
		<h3><xsl:apply-templates/></h3>
	</xsl:template>

	<xsl:template match="tagline">
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
  
	<xsl:template match="code">
		<div style="margin: 1em; background-color: #FFFFCC; border: thin solid #EEEEAA; padding-left: .5em; padding-right: .5em">
		<div style="font-weight: bold; padding-top: .5em; border-bottom: thin solid #EEEEAA"><xsl:value-of select="@title"/></div>
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
