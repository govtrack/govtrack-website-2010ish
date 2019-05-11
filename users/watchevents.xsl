<xsl:stylesheet
	version="1.0"
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns:govtrack-util="assembly://GovTrackWeb/GovTrack.Web.Util"
	xmlns:govtrack-login="assembly://GovTrackWeb/GovTrack.Web.Login"
	xmlns:govtrack-index="assembly://GovTrackWeb/GovTrack.Web.Pages.Index"
	exclude-result-prefixes="govtrack-util govtrack-index"
	>

	<xsl:template match="watchevents[count(@format)=0 or @format='html']">
		<div> <!-- must have a single root node for email updates! -->

		<!-- WATCH EVENTS -->
			
		<xsl:variable name="staletrackers">
			<xsl:for-each select="$monitors[substring-after(.,'option:')='']">
				<xsl:variable name="m" select="govtrack-login:MonitorLink(.)"/>
				<xsl:if test="$m/stale='True'">
					<xsl:copy-of select="$m"/>
				</xsl:if>
			</xsl:for-each>
		</xsl:variable>
		
		<xsl:if test="count($staletrackers) &gt; 0">
			<p><b>Note:</b> Some of your trackers are obsolete and won't generate any more events:
				<xsl:for-each select="$staletrackers">
					<a href="{href}"><xsl:value-of select="title"/></a>
					<xsl:if test="not(position()=last())">, </xsl:if>
				</xsl:for-each>.
			</p>
		</xsl:if>

		<xsl:if test="count($events) = 0">
			<p>There are no recent tracked events for these trackers.</p>
		</xsl:if>

		<p><xsl:value-of select="govtrack-index:GetNextSession()"/></p>

		<!--
		<xsl:if test="count($events[govtrack-util:IsInFuture(date)]) > 0">
			<h3>Upcoming Events</h3>
		</xsl:if>
		
		<div style="margin-top: 1em">
		<xsl:for-each select="$events">
			<xsl:sort select="date" order="ascending"/>
			<xsl:if test="govtrack-util:IsInFuture(date)">
				<xsl:call-template name="ShowEvent"/>
			</xsl:if>
		</xsl:for-each>
		</div>	

		<xsl:if test="count($events[govtrack-util:IsInFuture(date)]) > 0 and count($events[not(govtrack-util:IsInFuture(date))]) > 0">
			<h3>Recent Events</h3>
		</xsl:if>
		-->

		<div style="margin-top: 1em">
		<xsl:for-each select="$events">
			<xsl:sort select="date" order="descending"/>
			<!--<xsl:if test="not(govtrack-util:IsInFuture(date))">-->
				<xsl:call-template name="ShowEvent"/>
			<!--</xsl:if>-->
		</xsl:for-each>
		</div>	

		</div>		
	</xsl:template>

	<xsl:template name="ShowEvent">
			<div style="margin-bottom: 1.25em;" class="event">
				<xsl:if test="image">
					<div style="float: right; margin: 1em;">
						<a href="{link}">
							<img src="{image}" style="border: 1px solid #000088"/>
						</a>
					</div>			
				</xsl:if>
				
				<div class="date">
					<xsl:value-of select="date_string"/>
					-
					<xsl:value-of select="typename"/>
				</div>
				
				<div class="item">
					<a href="{link}"><xsl:value-of select="title"/></a>
				</div>
				<div class="welcome">		
				<xsl:if test="not(summary = '')">
					<div>
						<xsl:value-of select="summary"/>
					</div>
				</xsl:if>
				<div style="padding-left: 1em;">
					<xsl:for-each select="specifics/*">
						<div style="margin-top: .25em; margin-bottom: .25em">
							<xsl:if test="not(link='')"><a href="{link}" class="light"><xsl:value-of select="tag"/></a></xsl:if>
							<xsl:if test="(link='')"><b><xsl:value-of select="tag"/></b></xsl:if>
							<xsl:if test="count(text)">
							<xsl:text>: </xsl:text>
							<xsl:value-of select="text"/>
							</xsl:if>
						</div>
					</xsl:for-each>
				</div>

					<xsl:if test="count(monitor-name) &gt; 0 and govtrack-util:GetIsWebRequest()">
						<div style="font-size: 90%; color: #444444" id="eventmonitor{position()}">
							<xsl:choose>
							<xsl:when test="not(govtrack-login:HasMonitor(monitor-code))">
								<a href="javascript:SetInnerHtml('eventmonitor{position()}', 'Adding tracker...'); SetInnerHTMLFromAjaxResponse('/users/ajax_addmonitor.xpd?monitor={govtrack-util:JSEncode(govtrack-util:UrlEncode(monitor-code))}&amp;elemid=eventmonitor{position()}', 'eventmonitor{position()}')">Track</a><xsl:text> </xsl:text><xsl:value-of select="govtrack-util:Trunc(monitor-name, 80)"/>.
							</xsl:when>
							<xsl:otherwise>
								<a href="javascript:SetInnerHtml('eventmonitor{position()}', 'Removing tracker...'); SetInnerHTMLFromAjaxResponse('/users/ajax_addmonitor.xpd?monitor={govtrack-util:JSEncode(govtrack-util:UrlEncode(monitor-code))}&amp;action=remove&amp;elemid=eventmonitor{position()}', 'eventmonitor{position()}')">Stop tracking </a> <xsl:value-of select="govtrack-util:Trunc(monitor-name, 80)"/>.
							</xsl:otherwise>
							</xsl:choose>
						</div>
					</xsl:if>
					
					<xsl:if test="count($monitors) &gt; 1">
					<div style="font-size: 90%; color: #444444">
						(You are seeing this event because you are tracking
						<xsl:for-each select="monitors/*">
							<xsl:sort select="text"/>
							<xsl:if test="position()=last() and position() &gt; 1"> and </xsl:if>
							<a href="{link}" class="light"><xsl:value-of select="govtrack-util:Trunc(text, 80)"/></a>
							<xsl:if test="not(position()=last() or position()=last()-1)">, </xsl:if>
						</xsl:for-each>
						<xsl:text>)</xsl:text>
					</div>
					</xsl:if>
				</div>

			<xsl:if test="image">
				<div style="clear: both"/>
			</xsl:if>

			</div>
	</xsl:template>

	<xsl:template match="watchevents[@format='text']">
		<xsl:if test="count($events) = 0">
There are no recent tracked events for these trackers.
		</xsl:if>

<xsl:value-of select="govtrack-index:GetNextSession()"/>
<xsl:text xml:space="preserve">
</xsl:text>
		
		<xsl:for-each select="$events">
			<xsl:sort select="date" order="descending"/>

<xsl:text xml:space="preserve">
</xsl:text>
<xsl:value-of select="govtrack-util:DTToDateString(date)"/>
<xsl:text> - </xsl:text>
<xsl:value-of select="typename"/>
<xsl:text xml:space="preserve">
</xsl:text>
<xsl:value-of select="title"/>
<xsl:text xml:space="preserve">
</xsl:text>

				<xsl:if test="not(summary = '')">
<xsl:value-of select="summary"/>
<xsl:text xml:space="preserve">
</xsl:text>
				</xsl:if>

				<xsl:for-each select="specifics/*">
<xsl:text xml:space="preserve">   </xsl:text>
<xsl:value-of select="tag"/><xsl:if test="count(text)"><xsl:text>: </xsl:text> <xsl:value-of select="text"/></xsl:if>
<xsl:text>  [</xsl:text><xsl:value-of select="link"/><xsl:text xml:space="preserve">]
</xsl:text>
				</xsl:for-each>
					
				<xsl:if test="count($monitors) &gt; 1">
<xsl:text>(Related trackers: </xsl:text>
					<xsl:for-each select="monitors/*">
						<xsl:sort select="text"/>
						<xsl:if test="position()=last() and position() &gt; 1"> and </xsl:if>
						<xsl:value-of select="text"/>
						<xsl:if test="not(position()=last() or position()=last()-1)">, </xsl:if>
					</xsl:for-each>
					<xsl:text>)</xsl:text>
				</xsl:if>

<xsl:text>
[</xsl:text><xsl:value-of select="link"/><xsl:text>]

</xsl:text>
		</xsl:for-each>
	</xsl:template>
	
</xsl:stylesheet>
