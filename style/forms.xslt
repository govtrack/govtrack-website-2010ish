<?xml version="1.0" encoding="UTF-8" ?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns:xspforms="xspforms"
	xmlns:xpd="HttpContext"
	exclude-result-prefixes = "xpd xspforms">

	<xsl:template match="xspforms:script-placeholder">
		<script type="text/javascript">
		function GetFormText(form, element) {
			var theform = document.getElementById(form);
			return theform.elements[element].value;
		}
		</script>
	</xsl:template>
	
	<xsl:template match="xspforms:form">
		<!--<a name="form_{@id}"></a>-->

		<xsl:if test="count(@code) &gt; 0">
			<xsl:value-of select="xpd:addformhandler(@id, @code)"/>
		</xsl:if>
		
		<xsl:if test="xpd:param('PostFormID') = @id and xpd:param('AutoPostbackField') = ''">
			<!-- anything to do to execute form action -->
		</xsl:if>
		
		<form name="{@id}" id="{@id}" style="margin: 0px">
			<xsl:choose>
				<xsl:when test="@nosubmit">
					<xsl:attribute name="onsubmit">return false</xsl:attribute>
				</xsl:when>
				<xsl:when test="@action">
					<xsl:attribute name="method">post</xsl:attribute>
					<xsl:attribute name="action"><xsl:value-of select="@action"/></xsl:attribute>
				</xsl:when>
				<xsl:when test="@method='get'">
					<xsl:attribute name="method">get</xsl:attribute>
					<xsl:attribute name="action"><xsl:value-of select="xpd:request()"/></xsl:attribute>
				</xsl:when>
				<xsl:otherwise>
					<xsl:attribute name="method">post</xsl:attribute>
					<xsl:attribute name="action"><xsl:value-of select="xpd:request()"/></xsl:attribute>
				</xsl:otherwise>
			</xsl:choose>

			<div>
			<xsl:if test="not(@id)=''">
			<input type="hidden" name="PostFormID" value="{@id}"/>
			</xsl:if>
			<xsl:apply-templates/>
			</div>
		</form>
	</xsl:template>

	<xsl:template match="xspforms:form//xspforms:formreturn">
		<xsl:variable name="formid" select="ancestor::xspforms:form/@id"/>
		<xsl:if test="xpd:param('PostFormID') = $formid">
			<xsl:if test="not(''=xpd:getformreturn($formid))">
				<div class="{@class}"><xsl:value-of select="xpd:getformreturn($formid)"/></div>
			</xsl:if>
		</xsl:if>
	</xsl:template>

	<xsl:template match="xspforms:form//xspforms:submit">
		<input type="submit" value="{@text}"/>
	</xsl:template>
	
	<xsl:template match="xspforms:form//xspforms:hidden">
		<input type="hidden" name="{@name}" value="{@value}"/>
	</xsl:template>

	<xsl:template match="xspforms:form//xspforms:pass-param">
		<input type="hidden" name="{@name}" value="{xpd:param(string(@name))}"/>
	</xsl:template>

	<xsl:template match="xspforms:form//xspforms:text">
		<xsl:variable name="formid" select="ancestor::xspforms:form/@id"/>
	
		<input type="text" name="{@name}" id="{$formid}_{@name}"
			size="{@size}" maxlength="{@maxlen}"
			onkeyup="{@onchange}">
			<xsl:if test="count(@insidecaption)">
				<xsl:attribute name="onfocus">
					obj = document.getElementById("<xsl:value-of select="$formid"/>_<xsl:value-of select="@name"/>");
					if (obj.value == "<xsl:value-of select="@insidecaption"/>") { obj.value = "<xsl:value-of select="@default"/>"; obj.style.color = null; }
				</xsl:attribute>
				<xsl:attribute name="style">
					color: #666666;
				</xsl:attribute>
			</xsl:if>
			<xsl:if test="@autopostback='yes'">
				<xsl:attribute name="onblur">
					var theform = document.getElementById('<xsl:value-of select="$formid"/>');
					theform.elements['AutoPostbackField'].value = '<xsl:value-of select="@name"/>';
					theform.elements['AutoPostbackValue'].value = this.value;
					theform.elements['AutoPostbackState'].value = this.checked;
					theform.submit();
				</xsl:attribute>
			</xsl:if>		

			<xsl:choose>
				<xsl:when test="not(@always-use-default='yes') and (xpd:param('PostFormID') = $formid or @defaultfromquery)">
					<xsl:attribute name="value"><xsl:value-of select="xpd:param(string(@name))"/></xsl:attribute>
				</xsl:when>
				<xsl:when test="count(@insidecaption)">
					<xsl:attribute name="value"><xsl:value-of select="@insidecaption"/></xsl:attribute>
				</xsl:when>
				<xsl:when test="count(@default)">
					<xsl:attribute name="value"><xsl:value-of select="@default"/></xsl:attribute>
				</xsl:when>
			</xsl:choose>
		</input>
	</xsl:template>

	<xsl:template match="xspforms:form//xspforms:textarea">
		<xsl:variable name="formid" select="ancestor::xspforms:form/@id"/>
	
		<textarea name="{@name}" cols="{@cols}" rows="{@rows}" id="{$formid}_{@name}"
			onkeyup="{@onchange}">
			<xsl:if test="count(@insidecaption)">
				<xsl:attribute name="onfocus">
					obj = document.getElementById("<xsl:value-of select="$formid"/>_<xsl:value-of select="@name"/>");
					if (obj.value == "<xsl:value-of select="@insidecaption"/>") { obj.value = "<xsl:value-of select="@default"/>"; }
				</xsl:attribute>
			</xsl:if>
			<xsl:if test="@autopostback='yes'">
				<xsl:attribute name="onblur">
					var theform = document.getElementById('<xsl:value-of select="$formid"/>');
					theform.elements['AutoPostbackField'].value = '<xsl:value-of select="@name"/>';
					theform.elements['AutoPostbackValue'].value = this.value;
					theform.elements['AutoPostbackState'].value = this.checked;
					theform.submit();
				</xsl:attribute>
			</xsl:if>		

			<xsl:choose>
				<xsl:when test="not(@always-use-default='yes') and (xpd:param('PostFormID') = $formid or @defaultfromquery)">
					<xsl:value-of select="xpd:param(string(@name))"/>
				</xsl:when>
				<xsl:when test="count(@insidecaption)">
					<xsl:value-of select="@insidecaption"/>
				</xsl:when>
				<xsl:when test="count(@default)">
					<xsl:value-of select="@default"/>
				</xsl:when>
			</xsl:choose>
		</textarea>
	</xsl:template>
	
	<xsl:template match="xspforms:form//xspforms:password">
		<xsl:variable name="formid" select="ancestor::xspforms:form/@id"/>
		<input type="password" name="{@name}">
			<xsl:choose>
				<xsl:when test="xpd:param('PostFormID') = $formid or @defaultfromquery">
					<xsl:attribute name="value"><xsl:value-of select="xpd:param(string(@name))"/></xsl:attribute>
				</xsl:when>
				<xsl:when test="count(@default)">
					<xsl:attribute name="value"><xsl:value-of select="@default"/></xsl:attribute>
				</xsl:when>
			</xsl:choose>
		</input>
	</xsl:template>

	<xsl:template match="xspforms:form//xspforms:select">
		<xsl:variable name="formid" select="ancestor::xspforms:form/@id"/>
	
		<select size="1" name="{@name}" id="{$formid}_{@name}">
			<xsl:if test="@autopostback='yes'">
				<xsl:attribute name="onchange">
					document.getElementById('<xsl:value-of select="$formid"/>').elements['AutoPostbackField'].value = '<xsl:value-of select="@name"/>';
					document.getElementById('<xsl:value-of select="$formid"/>').elements['AutoPostbackValue'].value = document.getElementById('<xsl:value-of select="$formid"/>').elements['<xsl:value-of select="@name"/>'].value;
					document.getElementById('<xsl:value-of select="$formid"/>').submit();
				</xsl:attribute>
			</xsl:if>
			<xsl:if test="not(@autopostback='yes')">
				<xsl:attribute name="onchange">
					<xsl:value-of select="@onchange"/>
				</xsl:attribute>
			</xsl:if>
		
			<xsl:for-each select="xspforms:option">
				<option value="{@value}">
					<xsl:choose>
						<xsl:when test="not(parent::xspforms:select/@always-use-default='yes') and (xpd:param('PostFormID') = $formid or (count(parent::xspforms:select/@default)=0 and xpd:param('PostFormID')=''))">
							<xsl:if test="xpd:param(string(parent::xspforms:select/@name)) = @value">
								<xsl:attribute name="selected">1</xsl:attribute>
							</xsl:if>
						</xsl:when>
						<xsl:otherwise>
							<xsl:if test="parent::xspforms:select/@default = @value">
								<xsl:attribute name="selected">1</xsl:attribute>
							</xsl:if>
						</xsl:otherwise>
					</xsl:choose>
					<xsl:choose>
						<xsl:when test="count(@text)">
							<xsl:value-of select="@text"/>
						</xsl:when>
						<xsl:otherwise>
							<xsl:value-of select="."/>
						</xsl:otherwise>
					</xsl:choose>
				</option>
			</xsl:for-each>
		</select>
	</xsl:template>
	
	<xsl:template match="xspforms:form//xspforms:checkbox">
		<xsl:variable name="formid" select="ancestor::xspforms:form/@id"/>
	
		<input type="checkbox" name="{@name}" value="{@value}">
			<xsl:choose>
				<xsl:when test="not(@always-default = 'yes') and xpd:param('PostFormID') = $formid">
					<xsl:if test="xpd:param(string(@name)) = @value">
						<xsl:attribute name="checked"><xsl:value-of select="xpd:param(string(@name))"/></xsl:attribute>
					</xsl:if>
				</xsl:when>
				<xsl:when test="@default = 'checked'">
					<xsl:attribute name="checked"><xsl:value-of select="@default"/></xsl:attribute>
				</xsl:when>
			</xsl:choose>

			<xsl:if test="@autopostback='yes'">
				<xsl:attribute name="onclick">
					var theform = document.getElementById('<xsl:value-of select="$formid"/>');
					theform.elements['AutoPostbackField'].value = '<xsl:value-of select="@name"/>';
					theform.elements['AutoPostbackValue'].value = this.value;
					theform.elements['AutoPostbackState'].value = this.checked;
					theform.submit();
				</xsl:attribute>
			</xsl:if>		
		</input>
	</xsl:template>

	<xsl:template match="xspforms:form//xspforms:button">
		<xsl:variable name="formid" select="ancestor::xspforms:form/@id"/>
	
		<input type="button" name="{@name}" value="{@text}">
			<xsl:if test="@onclick=''">
			<xsl:attribute name="onclick">
				var theform = document.getElementById('<xsl:value-of select="$formid"/>');
				theform.elements['AutoPostbackField'].value = '<xsl:value-of select="@name"/>';
				theform.elements['AutoPostbackValue'].value = '<xsl:value-of select="@value"/>';
				theform.submit();
			</xsl:attribute>
			</xsl:if>
			<xsl:if test="not(@onclick='')">
			<xsl:attribute name="onclick">
				<xsl:value-of select="@onclick"/>
			</xsl:attribute>
			</xsl:if>
		</input>
	</xsl:template>
	

	<!-- IDENTITY TRANSFORMATION -->
	<xsl:template match="@*|node()">
	<xsl:copy>
		<xsl:apply-templates select="@*|node()"/>
	</xsl:copy>
	</xsl:template>


</xsl:stylesheet>

  
