<?xml version="1.0" encoding="UTF-8"?>
<!--<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN"
  "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd">-->

<xsl:stylesheet
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	version="1.0">

	<xsl:template match="/Page">
	        <xsl:processing-instruction name="mime-type">text/xml</xsl:processing-instruction>
	        <xsl:processing-instruction name="charset">UTF-8</xsl:processing-instruction>
 		
		<xsl:apply-templates select="Body/*"/>
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
