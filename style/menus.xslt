<?xml version="1.0" encoding="UTF-8" ?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns:xspmenus="xspmenus"
	xmlns:HttpContext="HttpContext"
	exclude-result-prefixes="xspmenus HttpContext">
	
	<xsl:preserve-space elements="*"/>
	
	<xsl:template match="xspmenus:imgmenu">
		<xsl:variable name="id" select="generate-id()"/>
		<div
			id="{$id}"
			onMouseOver="DHTML_ImageSwap('{$id}_image','{@imghot}'); DHTML_ShowMenu('{$id}', '{$id}_menu', 1);"
			onMouseOut="DHTML_ImageSwapRestore('{$id}_image'); DHTML_ShowMenu('{$id}', '{$id}_menu', 0); return true; "
			>
			<a href="{@href}">
				<img src="{@img}" id="{$id}_image" border="0"/>
			</a>
			
			<xsl:if test="count(*) &gt; 0">
				<div
					id="{$id}_menu"
					style="position: absolute; visibility: hidden;" class="menu"
					>
					<xsl:apply-templates/>
				</div>
			</xsl:if>
		</div>
	</xsl:template>
	
	<xsl:template match="xspmenus:textmenu">
		<xsl:variable name="id" select="@id"/>
		<div
			id="{$id}" class="textmenu"
			onmouseover="DHTML_DoMenu('{$id}', '{$id}_menu', 1);"
			onmouseout="DHTML_DoMenu('{$id}', '{$id}_menu', 0); return true; "
			><xsl:if test="not(count(@href)=0)">
				<xsl:attribute name="onclick">document.location = "<xsl:value-of select="@href"/>";</xsl:attribute>
			</xsl:if>

			<xsl:if test="*/@icon">
				<img src="/media/{*/@icon}" align="top" style="padding-right: 5px"/>
			</xsl:if>

			<xsl:value-of select="@text"/>
		
			<xsl:if test="count(*) &gt; 0">
				<div
					id="{$id}_menu"
					style="position: absolute; visibility: hidden;" class="menu"
					>
					<xsl:apply-templates/>
				</div>
			</xsl:if>
		</div>
	</xsl:template>	
	
	<xsl:template match="xspmenus:texthotlink">
		<xsl:variable name="id" select="@id"/>
		<div id="{$id}_text" class="texthotlink">

			<xsl:if test="not(@href='')">
				<xsl:attribute name="onmouseout">DHTML_TextRestore('<xsl:value-of select="$id"/>_text'); return true; </xsl:attribute>
				<xsl:attribute name="onmouseover">DHTML_TextHilight('<xsl:value-of select="$id"/>_text'); return true; </xsl:attribute>
				<xsl:attribute name="onclick">document.location = "<xsl:value-of select="@href"/>";</xsl:attribute>
			</xsl:if>

			<xsl:choose>
			<xsl:when test=".='-'">
				<hr size="1" height="1" style="height: 1px"/>
			</xsl:when>
			<xsl:otherwise>
				<xsl:if test="@icon">
					<img src="/media/{@icon}" align="top" style="padding-right: 5px"/>
				</xsl:if>
				<xsl:apply-templates/>
			</xsl:otherwise>
			</xsl:choose>
		</div>
	</xsl:template>

	<xsl:template match="xspmenus:bar">
		<xsl:variable name="width" select="@width"/>
		<xsl:variable name="height" select="@height"/>
		<table border="0" cellspacing="0" cellpadding="0">
			<tr>
				<xsl:for-each select="*">
					<td width="{$width}" height="{$height}" style="padding-right: 1em; padding-left: .5em">
						<xsl:apply-templates select="."/>
					</td>
				</xsl:for-each>
			</tr>
		</table>
	</xsl:template>

	<xsl:template match="xspmenus:tabstrip">
		<xsl:variable name="id" select="@id"/>
		<xsl:variable name="method" select="@method"/>
		<xsl:variable name="tabwidth" select="@tabwidth"/>
		<xsl:variable name="class">
			<xsl:if test="not(count(@class)=0)"><xsl:value-of select="@class"/></xsl:if>
			<xsl:if test="count(@class)=0">tabstrip</xsl:if>
		</xsl:variable>

		<xsl:variable name="params">
			<xsl:for-each select="xspmenus:param">
				<xsl:value-of select="@name"/>=<xsl:value-of select="HttpContext:param(@name)"/>
				<xsl:if test="position()&lt;last()">&amp;</xsl:if>
			</xsl:for-each>
		</xsl:variable>
		
		<xsl:variable name="active">
			<xsl:if test="not(count(xspmenus:body[@tab=HttpContext:param($id)]))"><xsl:value-of select="xspmenus:body[position()=1]/@tab"/></xsl:if>
			<xsl:if test="count(xspmenus:body[@tab=HttpContext:param($id)])"><xsl:value-of select="HttpContext:param($id)"/></xsl:if>
		</xsl:variable>
	
		<table border="0" cellspacing="0" cellpadding="0" class="{$class}" width="98%">
				<tr>
				<xsl:for-each select="xspmenus:tab">
					<td
						id="{$id}_{@id}_tab"
						style="text-align: center;"
						onMouseOver="DHTML_TextHilight('{$id}_{@id}_tab'); return true;"
						onMouseOut="DHTML_TextRestore('{$id}_{@id}_tab'); return true;"
						width="{100 div (count(parent::*/xspmenus:tab))}%"
						>
						
						<xsl:choose>
						<xsl:when test="$active=@id">
							<xsl:attribute name="class"><xsl:value-of select="$class"/>tab_active</xsl:attribute>
						</xsl:when>
						<xsl:otherwise>
							<xsl:attribute name="class"><xsl:value-of select="$class"/>tab</xsl:attribute>
						</xsl:otherwise>
						</xsl:choose>
						
						<xsl:choose>
						<xsl:when test="$method='dhtml'">
							<xsl:attribute name="onClick">
								<xsl:for-each select="parent::*/xspmenus:tab">
									DHTML_ShowHide('<xsl:value-of select="$id"/>_<xsl:value-of select="@id"/>_body', 0);
									DHTML_SetClass('<xsl:value-of select="$id"/>_<xsl:value-of select="@id"/>_tab', '<xsl:value-of select="$class"/>tab');
								</xsl:for-each>
								DHTML_ShowHide('<xsl:value-of select="$id"/>_<xsl:value-of select="@id"/>_body', 1);
								DHTML_TextActivate('<xsl:value-of select="$id"/>_<xsl:value-of select="@id"/>_tab');
								getObj('<xsl:value-of select="$id"/>_<xsl:value-of select="@id"/>_tab').classNameOld = "";
							</xsl:attribute>
						</xsl:when>
						<xsl:otherwise>
							<xsl:attribute name="onClick">
								document.location = '<xsl:value-of select="HttpContext:request()"/>?<xsl:value-of select="$id"/>=<xsl:value-of select="@id"/>&amp;<xsl:value-of select="$params"/>'
							</xsl:attribute>
						</xsl:otherwise>
						</xsl:choose>

						<div style="width: 100%;">

						<!--<xsl:if test="$active=@id">
							<img src="/media/stock_down-16.gif" align="top" style="padding-right: 3px"/>
						</xsl:if>
						<xsl:if test="not($active=@id) and not($method='dhtml')">
							<img src="/media/stock_right-16_faded.gif" align="top"
								style="padding-right: 5px"/>
						</xsl:if>-->
						
						<xsl:apply-templates select="node()"/>

						</div>
					</td>
				</xsl:for-each>
			</tr>
			
			<tr valign="top">
				<td colspan="{count(xspmenus:tab)}" class="{$class}top"></td>
			</tr>

			<tr valign="top">
				<td colspan="{count(xspmenus:tab)}" class="{$class}body">
					<xsl:choose>
					<xsl:when test="@method='dhtml'">
						<xsl:for-each select="xspmenus:body">
							<div id="{$id}_{@tab}_body">
								<xsl:if test="not($active=@tab)"><xsl:attribute name="style">display: none</xsl:attribute></xsl:if>

								<xsl:apply-templates select="node()"/>
							</div>
						</xsl:for-each>
					</xsl:when>
					<xsl:otherwise>
						<xsl:apply-templates select="xspmenus:body[@tab=$active]/node()"/>
					</xsl:otherwise>
					</xsl:choose>
				</td>
			</tr>

			<tr valign="top">
				<td colspan="{count(xspmenus:tab)}" class="{$class}bottom"></td>
			</tr>
		</table>
	</xsl:template>

	<xsl:template match="togglebutton">
		<xsl:variable name="id" select="concat('dhtmlbutton', generate-id(.))"/>
		<a id="{$id}" class="togglebutton"
			href="javascript:DHTML_ToggleVisible('{@target}', '{$id}', '{@hide}', '{@show}')">
			<xsl:value-of select="@show"/>
		</a>
	</xsl:template>

	<!-- IDENTITY TRANSFORMATION -->
	<xsl:template match="@*|node()">
	<xsl:copy>
		<xsl:apply-templates select="@*|node()"/>
	</xsl:copy>
	</xsl:template>
	
</xsl:stylesheet>

  
