<?xml version="1.0" encoding="UTF-8"?>
<?post-process href="forms.xslt" type="text/xsl" ?>

<xsl:stylesheet
	xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns:xspforms="xspforms"
	xmlns:httpcontext="HttpContext"
	xmlns:govtrack-util="assembly://GovTrackWeb/GovTrack.Web.Util"
	xmlns:govtrack-email="assembly://GovTrackWeb/GovTrack.Web.Email"
	xmlns:govtrack-bills="assembly://GovTrackWeb/GovTrack.Web.Bills"
	xmlns:govtrack-login="assembly://GovTrackWeb/GovTrack.Web.Login"
	xmlns:govtrack-comments="assembly://GovTrackWeb/GovTrack.Web.Comments"
	xmlns:govtrack-reps="assembly://GovTrackWeb/GovTrack.Web.Reps"
	xmlns:govtrack-index="assembly://GovTrackWeb/GovTrack.Web.Pages.Index"
	exclude-result-prefixes = "xspforms httpcontext govtrack-util govtrack-login govtrack-email govtrack-comments govtrack-bills govtrack-reps govtrack-index"
	version="1.0">
	
  <xsl:variable name="pv_adserver_targets">
    <xsl:if test="count(govtrack-login:GetMonitorsOfType('p')) &gt; 0">&amp;targets=govtrackwithlocation</xsl:if>
  </xsl:variable>

  <xsl:template match="/Page[not(httpcontext:param('ajax_element')='')]">
	<xsl:processing-instruction name="mime-type">text/html</xsl:processing-instruction>
	<xsl:processing-instruction name="charset">UTF-8</xsl:processing-instruction>
	<xsl:processing-instruction name="doc-type">&lt;!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"&gt;</xsl:processing-instruction>

	<xsl:if test="count(NoCache)=0">
	<xsl:processing-instruction name="cache">server</xsl:processing-instruction>
	<xsl:for-each select="Param">
		<xsl:processing-instruction name="cache-by-param"><xsl:value-of select="."/></xsl:processing-instruction>
	</xsl:for-each>
 	</xsl:if>
	<xsl:if test="not(count(NoCache)=0)">
		<xsl:processing-instruction name="cache">none</xsl:processing-instruction>
 	</xsl:if>
 	
 	<xsl:apply-templates select="Body-A|Body-B" mode="ajax"/>
  </xsl:template>
  <xsl:template match="*" mode="ajax">
  	<xsl:choose>
  	<xsl:when test="@id=httpcontext:param('ajax_element')">
  		<xsl:apply-templates select="."/>
  	</xsl:when>
  	<xsl:otherwise>
		<xsl:apply-templates select="@*|node()" mode="ajax"/>
	</xsl:otherwise>
	</xsl:choose>
  </xsl:template>

  <xsl:template match="/Page[httpcontext:param('ajax_element')='']" xml:space="preserve">
	<xsl:processing-instruction name="mime-type">text/html</xsl:processing-instruction>
	<xsl:processing-instruction name="charset">UTF-8</xsl:processing-instruction>
	<xsl:processing-instruction name="doc-type">&lt;!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"&gt;</xsl:processing-instruction>

	<xsl:if test="count(NoCache)=0">
	<xsl:processing-instruction name="cache">server</xsl:processing-instruction>
	<xsl:for-each select="Param">
		<xsl:processing-instruction name="cache-by-param"><xsl:value-of select="."/></xsl:processing-instruction>
	</xsl:for-each>
 	</xsl:if>
	<xsl:if test="not(count(NoCache)=0)">
		<xsl:processing-instruction name="cache">none</xsl:processing-instruction>
 	</xsl:if>
 	
	<xsl:variable name="title"><xsl:apply-templates select="Title" mode="text"/></xsl:variable>

	<html
		xmlns:v="urn:schemas-microsoft-com:vml"
		xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
		xmlns:dc="http://purl.org/dc/elements/1.1/"
		xmlns:foaf="http://xmlns.com/foaf/0.1/"
		xmlns:usbill="tag:govshare.info,2005:rdf/usbill/"
		xmlns:xsd="http://www.w3.org/2001/XMLSchema#"
		xml:lang="en" lang="en"
		>
		<head>
			<title><xsl:if test="Title/@PrependSiteTitle">GovTrack: </xsl:if> <xsl:value-of select="$title"/></title>

			<link rel="shortcut icon" type="image/png" href="/media/logo32x32.png"/>
			<link rel="stylesheet" type="text/css" href="/stylesheet.css"/>
			<xsl:if test="count(RDFA/@about) &gt; 0">
				<link rel="alternate" type="application/rdf+xml" href="{RDFA/@about}" />
			</xsl:if>

			<xsl:if test="not(httpcontext:param('page-command') = 'print')"> <!-- better for screen readers -->
			<link rel="search" type="application/opensearchdescription+xml" title="GovTrack.us" href="http://www.govtrack.us/embed/search/search.xml"/>

			<xsl:for-each select="Link">
				<link rel="{@rel}" type="{@type}"
					href="{@href}" />
			</xsl:for-each>

			<xsl:for-each select="//monitor-subscribe">
				<link rel="alternate" type="application/rss+xml" title="RSS Feed for {/Page/Title}"
					href="http://www.govtrack.us/users/events-rdf.xpd?monitors={@type}:{govtrack-login:MonitorEscape(@term)}" />
				<!--<link rel="alternate" type="application/atom+xml" title="Atom Feed for {/Page/Title}"
					href="http://www.govtrack.us/users/events-atom.xpd?monitors={@type}:{govtrack-login:MonitorEscape(@term)}" />-->

				<xsl:if test="httpcontext:param('getfeed')='rdf' or httpcontext:param('getfeed')='rss2' or httpcontext:param('getfeed')='atom'">
					<xsl:value-of select="httpcontext:redirect(concat('http://www.govtrack.us/users/events-', httpcontext:param('getfeed'), '.xpd?monitors=', @type, ':', govtrack-login:MonitorEscape(@term)))"/>
				</xsl:if>
			</xsl:for-each>
			</xsl:if>

			<meta http-equiv="P3P" content="CP='ALL CUR ADMa DEVa OUR IND ONL UNI NAV POL LOC'"/>
			<meta http-equiv="Content-Type" content="text/html;charset=utf-8" />

			<xsl:for-each select="Meta">
				<meta name="{@Name}" content="{.}"/>
			</xsl:for-each>
			
			<xsl:if test="httpcontext:param('page-command') = 'print' or httpcontext:param('page-command') = 'email'">
				<META name="ROBOTS" content="NOINDEX"/>
			</xsl:if>
			
			
			<!--
			<script type="text/javascript" src="/scripts/menus.js"/>
			<script type="text/javascript" src="/scripts/ajax.js"/>
			-->

			<script type="text/javascript" src="/scripts/govtrack.js"/>

			<xsl:apply-templates select="Head/*"/>
		</head>
		
		<body onload="{@script_onload}" onunload="{@script_onunload}"><xsl:if test="count(RDFA/@about)=1"><xsl:attribute name="about"><xsl:value-of select="RDFA/@about"/></xsl:attribute></xsl:if>
			<xsl:for-each select="RDFA/@type">
				<link rel="rdf:type" href="[{.}]"/>
			</xsl:for-each>
			
		<!-- main page table -->
		<table cellpadding="0" cellspacing="0" border="0" align="center">
		<tr valign="top">
		<td>

		<!-- main content table -->
		<table id="frame" cellpadding="0" cellspacing="0" border="0"><xsl:if test="not(@Width='Max')"><xsl:attribute name="class">width935</xsl:attribute></xsl:if>
			<tr>
				<td id="header" colspan="2">
					<h1><span class="g">g</span><span class="ov">ov</span>track<span class="dotus">.us</span></h1>
					
					<div class="menu1 screenonly">
						<!-- find box, put it up here so it doesnt push to a new line in IE7 -->
						<form id="billsearch_master" action="/congress/billsearch.xpd" method="get" style="display: inline-block; margin: 0; padding: 0">
						<table cellspacing="0" cellpadding="0" border="0">
						<tr>
						<td>
						<xsl:if test="govtrack-login:IsLoggedIn()">
							<a href="/users/account.xpd"><xsl:value-of select="govtrack-login:GetLoginEmail()"/></a> |
							<a href="/users/logout.xpd">Log out</a>
						</xsl:if>
						<xsl:if test="not(govtrack-login:IsLoggedIn())">
							<a href="javascript:DHTML_ShowHide('masterlogin')" customized="1" title="Edit Account Settings">Log In</a>
						</xsl:if>
						</td>
						<td>
						<input type="text" name="q" value="bill number or keywords" style="color: #777" onfocus="this.value='';this.style.color=''" size="20" id="billsearch_master_q"/>
						</td>
						<td>
						<input type="submit" value="" title="Search"/>
						</td>
						</tr>
						</table>
						</form>
					</div>
				
					<div class="menu2 screenonly">
						<a href="/" title="GovTrack.us">Home</a>
						<a href="/congress" title="Research Legislation, Members, Voting Records, and other aspects of Congress">Browse</a>
						<xsl:if test="not(govtrack-login:HasMonitors())">
							<a href="/users" title="Track Events in Congress">Trackers</a>
						</xsl:if>
						<xsl:if test="govtrack-login:HasMonitors()">
							<a href="/users/events.xpd" customized="1" title="View Tracked Events Matching Your Trackers">Trackers</a>
						</xsl:if>
						<a href="/about.xpd" title="About GovTrack.us">About</a>
						<a href="/developers" title="Build New Websites Based On Our Database">Use Our Data</a>
					</div>
				</td>
			</tr>

			<!-- blue splash -->
			<tr valign="top">
				<td id="splash" class="screenonly">
					<xsl:apply-templates select="/Page/Breadcrumbs"/>
					
					<div class="menubar robots-nocontent screenonly">
					<div id="masterlogin" style="display: none; position: absolute; width: 27em; border: 2px solid black; background-color: #FAFAFF; font-size: 11pt; padding: 1em; z-index: 1000;">
						<div style="float: right;"><a href="javascript:DHTML_ShowHide('masterlogin')">close</a></div>
						<h2 style="margin-top: 0px">Log In</h2>
						<p>If you already have registered an account on GovTrack, access your saved settings by logging in below.</p>
						<xsl:call-template name="LoginForm">
							<xsl:with-param name="redirect" select="'/users/account.xpd'"/>
							<xsl:with-param name="formid" select="'master'"/>
						</xsl:call-template>
						<p style="margin-bottom: 0px">Registering on the site lets you save your tracker settings and gives you the option for receiving email updates for congressional activity that matches the trackers you choose.</p>
					</div>
					<xsl:if test="httpcontext:param('PostFormID') = 'masterloginform' and not(''=httpcontext:getformreturn('masterloginform'))">
						<script>DHTML_ShowHide('masterlogin', true)</script>
					</xsl:if>
					</div>
				</td>
			</tr>

			<xsl:if test="Body-A">
			<tr valign="top">
				<td>
					<div id="titlebox">
					<div class="innerbox">
						<xsl:apply-templates select="Body-A/node()"/>
					</div>
					<div class="share">
						<xsl:if test="count(@HideAds | HideAds)=0">
						<div class="screenonly">
							<fb:like xmlns:fb="notimportant" href="{httpcontext:requesturl()}" layout="button_count" show_faces="true" width="150" action="recommend" colorscheme="light" />
							<xsl:apply-templates select="Share"/>
						</div>
						</xsl:if>
					</div>
					</div>
				</td>
				
				<xsl:if test="count(@HideAds | HideAds)=0">
				<td id="titleadbox">
					<script type="text/javascript"><xsl:comment>
					google_ad_client = "ca-pub-3418906291605762";
					/* GT title square */
					google_ad_slot = "6503428641";
					google_ad_width = 300;
					google_ad_height = 250;
					//</xsl:comment>
					</script>
					<script type="text/javascript"
					src="http://pagead2.googlesyndication.com/pagead/show_ads.js">
					</script>
					<div class="ad_explainer">(<a href="/ads.xpd">About Ads | Advertise Here</a>)</div>
				</td>
				</xsl:if>
			</tr>
			</xsl:if>

			<!-- main content area -->
			<tr>
				<td class="main {/Page/@BodyClass}" style="text-align: left" colspan="2">

		<xsl:if test="not(httpcontext:param('page-command') = 'print') and count(AllowEmail) and not(httpcontext:param('page-command') = 'email')">
			<table class="pageicons screenonly" cellpadding="0" cellspacing="0" border="0">
			<tr>
			<td>
			</td>
			<td style="font-size: 8pt; color: #999999; text-align: center; padding: 2px 8px 3px 0px">
			</td>
			<xsl:for-each select="Download">
				<td style="font-size: 6pt; color: #999999; text-align: center">
						<a href="{.}" title="Download {@Type} File"><img src="/media/save.gif" border="0" alt="Download"/></a>
						<div><xsl:value-of select="@Type"/></div>
				</td>
			</xsl:for-each>
			</tr>
			</table>
		</xsl:if>
		
		<div class="maincontent">
		
		<!-- side bar -->
		<xsl:if test="not(httpcontext:param('page-command')='print') and not(@HideSidebar) and count(Sidebar/*) &gt; 0">
		<div style="float: right; clear: right; width: 250px; margin-left: 1.5em" class="screenonly"> <!-- 236px -->
			<div class="sidebar">
				<xsl:apply-templates select="Sidebar"/> <!-- allow things like IfLoggedIn around Section tags -->
			</div>
		</div>
		</xsl:if>

		<!-- body next to the sidebar -->
						<div><xsl:if test="count(Sidebar/*) &gt; 0"><xsl:attribute name="style">width: 650px</xsl:attribute></xsl:if>
							<xsl:apply-templates select="Body-B/node()"/>
						</div>
			</div>
			
			<div style="clear: both">&#160;</div> <!-- prevent end of this region from occurring before end of sidebar float, the nbsp prevents a weird space that appears below the div -->
			
		<!-- end of main area -->

				</td>
				
			</tr>

			<xsl:if test="not(httpcontext:param('page-command') = 'print')">
			<tr>
				<td colspan="2" class="footer">
					<p>GovTrack.us is a project of <a href="http://www.civicimpulse.com">Civic Impulse, LLC</a>.
					Read <a href="/about.xpd">about GovTrack</a>.</p>
					
					<p>Feedback (but not political opining) is welcome to
					<xsl:call-template name="contactemailaddress"/>, but I can't do your research for you, nor can I
					pass on messages to Members of Congress.</p>
					
					<p>You can also find us on our <a href="http://www.facebook.com/pages/Civic-Impulse/312186525430">Facebook Page</a> and <a href="http://twitter.com/#!/govtrack">@govtrack</a> on Twitter.</p>
					
					<p>You are encouraged to reuse any material on this site. GovTrack is open source and supports open knowledge; see the <a href="/developers">developers</a> page.</p>
				</td>
			</tr>
			</xsl:if>

		</table> <!-- end of main content table -->

		</td>
		</tr>
		</table> <!-- end of main page table -->

			<xsl:apply-templates select="Scripts/*"/>

<!-- UserVoice -->
<!--
<script type="text/javascript">
 var uservoiceJsHost = ("https:" == document.location.protocol) ? "https://uservoice.com" : "http://cdn.uservoice.com";
 document.write(unescape("%3Cscript src='" + uservoiceJsHost + "/javascripts/widgets/tab.js' type='text/javascript'%3E%3C/script%3E"))
</script>
<script type="text/javascript">
 UserVoice.Tab.show({ 
    key: 'govtrack',
    host: 'govtrack.uservoice.com', 
    forum: 'feedback', 
    lang: 'en'
  });
</script>
-->

<!-- Google Analytics -->
<script type="text/javascript">
var gaJsHost = (("https:" == document.location.protocol) ? "https://ssl." : "http://www.");
document.write(unescape("%3Cscript src='" + gaJsHost + "google-analytics.com/ga.js' type='text/javascript'%3E%3C/script%3E"));
</script>
<script type="text/javascript">
try { var pageTracker = _gat._getTracker("UA-1190841-1"); pageTracker._trackPageview(); }
catch(err) {}
</script>

<!-- RPXNow -->
<script src="https://rpxnow.com/openid/v2/widget" type="text/javascript"></script>
<script type="text/javascript">
RPXNOW.token_url = "http://" + document.location.host + "/users/login_rpxnow.xpd?redirect=" + encodeURI(document.location);
RPXNOW.realm = "govtrack-us";
RPXNOW.overlay = true;
RPXNOW.language_preference = 'en';
</script>

<!-- Facebook -->
<div id="fb-root"></div>
<script>
  window.fbAsyncInit = function() {
   FB.init({appId: '119329904748946', status: true, cookie: true, xfbml: true});
  };
  (function() {
    var e = document.createElement('script'); e.async = true;
    e.src = document.location.protocol + '//connect.facebook.net/en_US/all.js';
   document.getElementById('fb-root').appendChild(e);
     }());
</script>

<!--<script src="http://www.surveymonkey.com/jsPop.aspx?sm=3SWUi_2bOO3iAvZkafEwNtCg_3d_3d"> </script>-->
		</body>
	</html>  
  </xsl:template>

	<xsl:template match="Sidebar//Section">
		<xsl:variable name="ctx">
			<Box><xsl:if test="count(@Color)=0"><xsl:attribute name="Color">yellow</xsl:attribute></xsl:if><xsl:if test="not(count(@Color)=0)"><xsl:attribute name="Color"><xsl:value-of select="@Color"/></xsl:attribute></xsl:if>
				<xsl:if test="count(@Icon)=1">
					<img src="{@Icon}" style="float: right; position: relative; top: 0px; right: 2px" alt=""/>
				</xsl:if>
				<xsl:if test="not(@Name='')"><div class="sectionhead"><xsl:value-of select="@Name"/></div></xsl:if>
				<xsl:apply-templates/>
			</Box>
		</xsl:variable>

		<xsl:for-each select="$ctx">
			<div style="margin-bottom: 1em">
			<xsl:call-template name="Box"/>
			</div>
		</xsl:for-each>
	</xsl:template>

	<xsl:template match="IfLoggedIn">
		<xsl:if test="govtrack-login:IsLoggedIn()">
			<xsl:apply-templates/>
		</xsl:if>
	</xsl:template>
	<xsl:template match="IfNotLoggedIn">
		<xsl:if test="not(govtrack-login:IsLoggedIn())">
			<xsl:apply-templates/>
		</xsl:if>
	</xsl:template>

	<xsl:template match="IfHasMonitors">
		<xsl:if test="govtrack-login:HasMonitors()">
			<xsl:apply-templates/>
		</xsl:if>
	</xsl:template>

	<xsl:template match="IfParam">
		<xsl:if test="httpcontext:param(@name) = @value">
			<xsl:apply-templates/>
		</xsl:if>
	</xsl:template>
	<xsl:template match="IfNotParam">
		<xsl:if test="not(httpcontext:param(@name) = @value)">
			<xsl:apply-templates/>
		</xsl:if>
	</xsl:template>
	
	<xsl:template match="LoginEmail">
		<xsl:value-of select="govtrack-login:GetLoginEmail()"/>
	</xsl:template>

	<xsl:template match="param[not(name(parent::*)='applet')]">
		<xsl:if test="count(@escape)=0"><xsl:value-of select="httpcontext:param(@name)"/></xsl:if>
		<xsl:if test="count(@escape)=1"><xsl:value-of select="govtrack-util:JSEncode(httpcontext:param(@name))"/></xsl:if>
	</xsl:template>		
	<xsl:template match="hostname">
		<xsl:value-of select="httpcontext:requesthost()"/>
	</xsl:template>
	
	<xsl:template match="include">
		<xsl:apply-templates select="HttpContext:document(string(@source))"/>
	</xsl:template>

	<xsl:template match="LoginForm" xml:space="preserve">
		<xsl:call-template name="LoginForm">
			<xsl:with-param name="redirect" select="@redirect"/>
		</xsl:call-template>
	</xsl:template>
	
	<xsl:template name="LoginForm" xml:space="preserve">
		<xsl:param name="redirect" select="''"/>
		<xsl:param name="formid" select="''"/>
		<p>- Sign in with a GovTrack <a href="javascript:DHTML_ShowHide('{$formid}loginwrapper');">username/password</a> for existing accounts.</p>
		<xspforms:form xmlns:xspforms="xspforms" id="{$formid}loginform" code="GovTrack.Web.Login, GovTrackWeb:DoLogin">
		<xsl:if test="not($redirect = '')"><xspforms:hidden name="redirect" value="{$redirect}"/></xsl:if>
		<xsl:if test="$redirect='' and not(httpcontext:param('redirect')='')"><xspforms:hidden name="redirect" value="{httpcontext:param('redirect')}"/></xsl:if>
		<xspforms:formreturn class="formerror"/>
		<div id="{$formid}loginwrapper" style="display: none">
		<div style="margin-top: .5em">Email:</div> <div><xspforms:text name="email"/> </div>
		<div style="margin-top: .5em">Password:</div> <div><xspforms:password name="password"/> </div>
		<div style="margin-top: .5em; margin-bottom: .5em;" xml:space="preserve">
			<div><a href="/users/forgottenpassword.xpd">Forgot your password?</a></div>
			</div>
		<xspforms:submit text="Log In"/>
		</div>
		</xspforms:form>
		<p>- Sign in with a <a class="rpxnow" onclick="return false;" href="https://govtrack-us.rpxnow.com/openid/v2/signin?token_url=http://www.govtrack.us/users/login_rpxnow.xpd">third-party account or OpenID</a> (recommended for new users).</p>
		<p>- Register a <a href="/users/register.xpd">new GovTrack username/password</a>.</p>
	</xsl:template>
	
	<xsl:template match="floatrightbox">
		<div class="box" style="float: right; width: 250px; clear: right; margin-right: .5em; margin-left: .5em">
		<div class="boxtitle"><xsl:value-of select="@title"/></div>
		<xsl:apply-templates/>
		</div>
	</xsl:template>	

	<xsl:template match="MonitorButton">
		<xsl:variable name="id">
			<xsl:if test="count(@id)=0"><xsl:value-of select="concat('monbtn_', httpcontext:counter())"/></xsl:if>
			<xsl:if test="count(@id)=1"><xsl:value-of select="@id"/></xsl:if>
		</xsl:variable>
		<span id="{$id}">
			<xsl:choose>
			<xsl:when test="not(govtrack-login:HasMonitor(@monitor))">
				<a href="javascript:SetInnerHtml('{$id}', '&lt;img src=&quo;/media/tracker_remove.gif&quo; width=&quo;16&quo; height=&quo;16&quo;/&gt;');SetInnerHTMLFromAjaxResponse('/users/ajax_addmonitor_icon.xpd?monitor={govtrack-util:UrlEncode(@monitor)}&amp;action=add&amp;elemid={$id}', '{$id}')">
					<img src="/media/tracker_add.gif" title="Track {@name}" border="0" width="16" height="16"/>
				</a>
			</xsl:when>
			<xsl:otherwise>
				<a href="javascript:SetInnerHtml('{$id}', '&lt;img src=&quo;/media/tracker_add.gif&quo; width=&quo;16&quo; height=&quo;16&quo;/&gt;');SetInnerHTMLFromAjaxResponse('/users/ajax_addmonitor_icon.xpd?monitor={govtrack-util:UrlEncode(@monitor)}&amp;action=remove&amp;elemid={$id}', '{$id}')">
					<img src="/media/tracker_remove.gif" title="Stop Tracking {@name}" border="0" width="16" height="16"/>
				</a>
			</xsl:otherwise>
			</xsl:choose>
		</span>
	</xsl:template>

	<xsl:template match="monitor-subscribe">
			<xsl:variable name="cx" select="httpcontext:counter()"/>
			<div>
				<!--<div class="sidebarsectionicon">
					<a href="/users/events.xpd?monitors={@type}:{govtrack-login:MonitorEscape(@term)}" rel="nofollow">
						<img src="/media/feed-icon-20x20.gif" border="0" title="Click for Feed Options" alt="Feed Options"/>
					</a>
				</div>-->
				<xsl:if test="count(events) = 1">
					<div style="margin-top: 4px;">This feed includes <xsl:apply-templates select="events/node()"/>.</div>
				</xsl:if>
				<p style="text-align: center"><a href="/users/events.xpd?monitors={@type}:{govtrack-login:MonitorEscape(@term)}" rel="nofollow">Preview Feed &gt;</a></p>

			<hr size="1"/>

			<xsl:choose>
			<xsl:when test="govtrack-login:HasMonitor(concat(@type, ':', @term))=1">
				<div id="monitorbutton{$cx}">
				You are tracking this <xsl:value-of select="@desc"/>.
				<div style="text-align: center; padding: .5em">
					<!--<xspforms:submit text="Stop Tracking"/>-->
					<input type="button" value="Stop Tracking" onclick="GTMonitor('monitorbutton{$cx}', false, '{govtrack-util:JSEncode(govtrack-util:UrlEncode(concat(@type, ':', @term)))}')"/>
				</div>
				<div>Events related to this <xsl:value-of select="@desc"/> will
				appear on your customized <a href="/users/events.xpd">Tracked
				Events</a> page
				<xsl:if test="govtrack-login:IsLoggedIn()">
					and in <a href="/users/yourmonitors.xpd">email updates</a>
				</xsl:if>
				</div>
				</div>
				<xsl:apply-templates select="when-monitoring"/>
			</xsl:when>
			<xsl:otherwise>
				<div id="monitorbutton{$cx}">
				<div>Personalize your <a href="/users/events.xpd">Tracked
				Events</a> page
				<xsl:if test="govtrack-login:IsLoggedIn()">
					and <a href="/users/yourmonitors.xpd">email updates</a>
				</xsl:if>
				by selecting trackers.</div>
				<xsl:apply-templates select="when-not-monitoring"/>
				<div style="text-align: center; margin: 8px"><input type="button" value="Add Tracker" onclick="GTMonitor('monitorbutton{$cx}', true, '{govtrack-util:JSEncode(govtrack-util:UrlEncode(concat(@type, ':', @term)))}')"/></div>
				<xsl:if test="count(monitor-info)=1">
					<div>This tracker adds the feed above and
					<xsl:apply-templates select="monitor-info/node()"/>.</div>
				</xsl:if>
				</div>
			</xsl:otherwise>
			</xsl:choose>

			<!--<xsl:if test="govtrack-login:IsLoggedIn()">
				<div style="margin-top: .5em;">
					You are logged in as <tt><xsl:value-of select="govtrack-login:GetLoginEmail()"/></tt>.
					<a href="/users/logout.xpd">Log out?</a>
				</div>
			</xsl:if>-->

			<xsl:if test="not(govtrack-login:IsLoggedIn())">
				<div style="margin-top: .5em;">
					You are not logged in to an account.
					<a href="javascript:DHTML_ShowHide('notloggedin1{$cx}');">Why sign up?</a>
				</div>
				<div id="notloggedin1{$cx}" style="display: none; margin: .5em 0em .5em 0em; color: #009">
					When you sign up, your trackers are stored permanently and you can
					access them from any computer. Otherwise they are stored in a "cookie"
					on your computer and could get erased. When you are signed in, your
					personal tracked events RSS feed will update with your tracker settings,
					and you can get email updates on tracked events sent to you automatically.
				</div>
				<div style="text-align: center; margin-top: .5em;">
					<a href="/users/login.xpd?redirect={httpcontext:fullrequest()}">Log In</a>
					|
					<a href="/users/register.xpd">Sign Up</a> (for free)
				</div>
			</xsl:if>

			<hr size="1"/>
			
			Make a <a href="/embed/sample-events.xpd?monitors={@type}:{govtrack-login:MonitorEscape(@term)}">widget for this tracker</a> to display on your web page.
			
			</div>
	</xsl:template>
	
	<xsl:template match="monitor-subscribe-archival">
			<div>
			<xsl:choose>
			<xsl:when test="govtrack-login:HasMonitor(concat(@type, ':', @term))=1">
				<div id="monitorbutton">
				This <xsl:value-of select="@desc"/> is listed in your
				<a href="/users/yourmonitors.xpd">Trackers</a> page so
				you can find it again later. It no longer generates
				events.
				<div style="text-align: center; padding: .5em">
					<input type="button" value="Remove Bookmark" onclick="GTMonitor('monitorbutton', false, '{govtrack-util:JSEncode(govtrack-util:UrlEncode(concat(@type, ':', @term)))}')"/>
				</div>
				</div>
			</xsl:when>
			<xsl:otherwise>
				<div id="monitorbutton">
				Bookmark this <xsl:value-of select="@desc"/> in your
				<a href="/users/yourmonitors.xpd">Trackers</a> page so
				you can find it again later. This <xsl:value-of select="@desc"/>
				no longer generates events.
				<div style="text-align: center; margin: 8px"><input type="button" value="Add Bookmark" onclick="GTMonitor('monitorbutton', true, '{govtrack-util:JSEncode(govtrack-util:UrlEncode(concat(@type, ':', @term)))}')"/></div>
				</div>
			</xsl:otherwise>
			</xsl:choose>

			<xsl:if test="govtrack-login:IsLoggedIn()">
				<div style="margin-top: .5em;">
					You are logged in as <tt><xsl:value-of select="govtrack-login:GetLoginEmail()"/></tt>.
					<a href="/users/logout.xpd">Log out?</a>
				</div>
			</xsl:if>

			<xsl:if test="not(govtrack-login:IsLoggedIn())">
				<div style="margin-top: .5em;">
					You are not logged in to an account.
				</div>
				<div style="margin-top: .5em; text-align: center">
					<a href="/users/login.xpd?redirect={httpcontext:fullrequest()}">Log In</a>
					|
					<a href="/users/register.xpd">Sign Up</a> (for free)
				</div>
				<div id="notloggedin0" style="margin-top: .5em; text-align: center">
					<a href="javascript:DHTML_ShowHide('notloggedin1',1);DHTML_ShowHide('notloggedin0',0)">Why sign up?</a>
				</div>
				<div id="notloggedin1" style="display: none; margin: .5em">
					When you sign up, your trackers are stored permanently and you can
					access them from any computer. Otherwise they are stored in a "cookie"
					on your computer and could get erased. When you are signed in, your
					personal tracked events RSS feed will update with your tracker settings,
					and you can get email updates on tracked events sent to you automatically.
				</div>
			</xsl:if>
			
			</div>
	</xsl:template>
	
	<xsl:template match="popup_new">
		<a href="{@href}" target="_blank">
			<xsl:apply-templates/>
		</a>
	</xsl:template>

	<!--<xsl:template match="bill">
		<xsl:variable name="b" select="govtrack-bills:LoadBill3(@id)"/>
		<a href="{govtrack-bills:BillLink($b)}">
			<xsl:value-of select="govtrack-bills:DisplayString($b, 75)"/>
		</a>
	</xsl:template>

	<xsl:template match="vote">
		<xsl:if test="not(@notext='1')">
			<xsl:variable name="v" select="govtrack-bills:LoadRollParse(@id)"/>
			<xsl:text xml:space="preserve"> </xsl:text>
			<xsl:value-of select="$v/@aye"/> to <xsl:value-of select="$v/@nay"/>
			<xsl:if test="not($v/required='1/2')"> (<xsl:value-of select="$v/required"/> needed)</xsl:if>
		</xsl:if>
		[<a href="/congress/vote.xpd?vote={@id}">vote details</a><xsl:text>]</xsl:text>
	</xsl:template>-->

	<xsl:template match="linkbullet">
		<div>
		<xsl:if test="not(httpcontext:param('tab')=@tab)">
			<xsl:variable name="tab">
				<xsl:if test="count(@tab)=1 and not(@tab='')">&amp;tab=<xsl:value-of select="@tab"/></xsl:if>
			</xsl:variable>
			- <a href="{@href}{$tab}"><xsl:apply-templates/></a>
		</xsl:if>
		<xsl:if test="httpcontext:param('tab')=@tab">
			<xsl:if test="not(@style='flat')">&gt; </xsl:if>
			<xsl:if test="@style='flat'">- </xsl:if>
			<span style="color: #777777"><xsl:apply-templates/></span>
		</xsl:if>
		</div>
	</xsl:template>
	
	<xsl:template match="CommunityQuestions">
		<xsl:variable name="topic" select="@topic"/>
		<xsl:variable name="mode" select="@mode"/>
		<xsl:variable name="qs" select="govtrack-comments:GetQuestions($topic)"/>
		<xsl:variable name="qlimit"><xsl:if test="not($mode='expanded')">2</xsl:if><xsl:if test="($mode='expanded')">8</xsl:if></xsl:variable>
		
		<xsl:variable name="ctx2">
			<div><xsl:if test="not(@mode='expanded')"><xsl:attribute name="style">padding: .7em</xsl:attribute></xsl:if>
			<div>
				<xsl:if test="count($qs)=0">
					Have a question about this <xsl:value-of select="@type"/>?
					<a href="javascript:DHTML_ShowHide('questionsubmit', null); getObj('questionsubmitq').focus()">Submit a short fact-oriented question</a> and see if it will be answered by
					other visitors.
				</xsl:if>
				<xsl:if test="not(count($qs)=0)">
					Can you answer any of these questions posed by other users? Think of it as a civic good deed.
					<xsl:if test="count($qs) &lt;= $qlimit">
						You can <a href="javascript:DHTML_ShowHide('questionsubmit', null); getObj('questionsubmitq').focus()">submit a short question</a> too.
					</xsl:if>
					<xsl:if test="count($qs) &gt; $qlimit">
						See <xsl:value-of select="count($qs)-$qlimit"/> more question<xsl:if test="count($qs)-$qlimit&gt;1">s</xsl:if> posed on this topic or submit your own question on <a href="/users/questions.xpd?topic={$topic}">the Q&amp;A page</a>.
					</xsl:if>
				</xsl:if>
				<!--(If you have a general question about how Congress works,
				see <a href="/askme.xpd">this page instead</a>.)-->
			</div>
			<div id="questionsubmit" style="display: none; padding: .5em 0em .5em 0em; clear: both">
				<div><b>Enter your question. </b>
				<i>Tips:</i> Be clear and precise. No abbreviations. Don't ask about the status of this bill or when it will be voted on (other users are not likely to know).
				Don't ask a loaded question or for individual advice: Your question will be rejected.</div>
				<div><input id="questionsubmitq" size="50"/>
				<input type="button" value="Submit Question" onclick="q=getObj('questionsubmitq'); if (q.value!='') {{ DHTML_ShowHide('questionsubmit', false); AjaxElement('questionsubmitresponse', 'Submitting question for approval...', '/users/ajax_submit_question.xpd?topic=' + '{$topic}' + '&amp;question=' + escape(q.value))}}"/></div>
				<div>After submitting your question it will be reviewed, and if approved will appear here.</div>
			</div>
			<div id="questionsubmitresponse" style="font-style: italic"></div>
			</div>
			<xsl:for-each select="$qs[position() &gt; last()-$qlimit]">
				<xsl:variable name="as" select="govtrack-comments:GetAnswers(id)"/>
				
				<xsl:if test='position()=1'>
					<hr size="1"/>
				</xsl:if>

				<div><xsl:attribute name="style"><xsl:if test="not($mode='expanded')">margin-bottom: .5em</xsl:if><xsl:if test="($mode='expanded')">margin: 1em</xsl:if></xsl:attribute>
					<xsl:value-of select="date"/> - <xsl:value-of select="text"/> -
					<xsl:if test="count($as) &gt; 0"><a href="javascript:DHTML_ShowHide('question{position()}answers', null);">Read Answers</a></xsl:if>
					<xsl:if test="count($as) = 0"><a href="javascript:DHTML_ShowHide('questionanswer{position()}', null); getObj('questionanswer{position()}q').focus()">Answer it!</a></xsl:if>
				</div>
				
				<div id="question{position()}answers" style="display: none">
					<xsl:for-each select="$as[position() &gt; last()-2]">
						<div style="margin: .25em 0em .25em 2em; padding-left: 1em; border-left: 1px dotted black">
							<u>Answered by a visitor on <xsl:value-of select="date"/></u> -
							<xsl:for-each select="govtrack-util:Split(text, '&#x0A;')">
								<xsl:if test="position()=1"><xsl:value-of select="."/></xsl:if>
								<xsl:if test="position() &gt; 1"><p style="margin-left: 2em"><xsl:value-of select="."/></p></xsl:if>
							</xsl:for-each>
						</div>
					</xsl:for-each>
					<div style="margin: .25em 0em .5em 5em; padding-left: 1em; font-weight: bold">
						<xsl:if test="count($as) &gt; 3">
							Read <xsl:value-of select="count($as)-3"/> more answer<xsl:if test="count($as)-3&gt;1">s</xsl:if> on <a href="/users/questions.xpd?topic={$topic}">the Q&amp;A page</a>.
						</xsl:if>
						<a href="javascript:DHTML_ShowHide('questionanswer{position()}', null); getObj('questionanswer{position()}q').focus()">Add another answer.</a>
					</div>
				</div>

				<div id="questionanswer{position()}" style="display: none; padding-top: 1em; margin-left: 5em">
					<b>Answer This Question</b>
					<table>
					<tr valign="top"><td><textarea id="questionanswer{position()}q" rows="4" cols="35" wrap="virtual"/></td>
					<td style="padding-left: 1em" width="300"><div><input type="button" value="Submit Answer" onclick="q=getObj('questionanswer{position()}q'); if (q.value!='') {{ DHTML_ShowHide('questionanswer{position()}', false); AjaxElement('questionsubmitresponse{position()}', 'Submitting answer for approval...', '/users/ajax_submit_answer.xpd?question={id}&amp;answer=' + escape(q.value))}}"/></div>
					<p style="font-size: 80%">Tips: Reference the text of the bill when possible --- answers with unsourced facts may be rejected. Don't be inflammatory --- it will be edited out! Don't use abbreviations, but stay concise and on-point.</p>
					</td></tr></table>
				</div>
				<div id="questionsubmitresponse{position()}" style="font-style: italic"></div>
			</xsl:for-each>
			<xsl:if test="count($qs) &gt; $qlimit">
				<div><xsl:attribute name="style"><xsl:if test="not($mode='expanded')">margin-left: 4em</xsl:if><xsl:if test="($mode='expanded')">margin-left: 1em</xsl:if></xsl:attribute>
				and <a href="/users/questions.xpd?topic={$topic}"><xsl:value-of select="count($qs)-$qlimit"/> more question<xsl:if test="count($qs)-$qlimit&gt;1">s</xsl:if></a>.
				</div>
			</xsl:if>
		</xsl:variable>
		
		<xsl:if test="(@mode='expanded')">
			<xsl:copy-of select="$ctx2"/>
		</xsl:if>

		<xsl:if test="not(@mode='expanded')">
			<xsl:variable name="ctx">
			<Box Color="blue">
				<xsl:if test="not(@mode='expanded')"><div><b>Question &amp; Answer</b></div>
				<img src="/media/questions.gif" style="margin: 0px 1em 2px 0px; float: left;" alt=""/></xsl:if>
				<xsl:copy-of select="$ctx2"/>
			</Box>
			</xsl:variable>
			
			<xsl:for-each select="$ctx">
				<xsl:call-template name="Box"/>
			</xsl:for-each>
		</xsl:if>
	</xsl:template>
	
	<xsl:template match="Box">
		<xsl:call-template name="Box"/>
	</xsl:template>
	
	<xsl:template name="Box">
		<xsl:if test="count(@Icon) &gt; 0">
			<div style="height: 96px; margin-left: 30px; background-image: url(/media/box-{@Icon}-a.gif); background-repeat: no-repeat;">
				<div class="PageTitle" style="padding-left: 130px; padding-top: 1em">
					<xsl:apply-templates select="title/node()"/>
				</div>
			</div>
		</xsl:if>
		<xsl:variable name="bgcolor">
			<xsl:if test="@Color='blue'">d7e7fe</xsl:if>
			<xsl:if test="@Color='yellow'">fffcdf</xsl:if>
		</xsl:variable>
		<table cellspacing="0" cellpadding="0" border="0"><tr><td> <!-- so we don't intersect the sidebar -->
		<div style="background-color: #{$bgcolor};">
			<div style="background-image: url(/media/box-t-{@Color}.gif); background-repeat: repeat-x;">
			<div style="background-image: url(/media/box-l-{@Color}.gif); background-repeat: repeat-y;">
			<div style="background-image: url(/media/box-r-{@Color}.gif); background-repeat: repeat-y; background-position: right">
			<div style="background-image: url(/media/box-tl-{@Color}.gif); background-repeat: no-repeat">
			<div style="background-image: url(/media/box-tr-{@Color}.gif); background-repeat: no-repeat; background-position: top right;">
			<div style="background-image: url(/media/box-b-{@Color}.gif); background-repeat: repeat-x; background-position: bottom;  position: relative"> <!-- position relative fixes an image-disappearing problem in IE -->
			<div style="background-image: url(/media/box-br-{@Color}.gif); background-repeat: no-repeat; background-position: bottom right; position: relative">
			<div style="background-image: url(/media/box-bl-{@Color}.gif); background-repeat: no-repeat; background-position: bottom left;  position: relative">

			<xsl:if test="count(@Icon) &gt; 0">
				<img src="/media/box-{@Icon}-b.gif" style="margin-left: 30px" alt=""/>
			</xsl:if>

			<div><xsl:if test="count(@Icon) = 0"><xsl:attribute name="style">padding-top: 1em</xsl:attribute></xsl:if>
			<div style="margin: 0px 2em 0px 2em;" class="box_{@Color}"> <!-- padding causes rendering bug in IE, and bottom margin is ignored -->
				<xsl:apply-templates select="*[not(name)='title']"/>
			</div>
			<div style="height: 1em"></div> <!-- this is how to safely pad the bottom -->
			</div>

			</div>
			</div>
			</div>
			</div>
			</div>
			</div>
			</div>
			</div>
		</div>
		</td></tr></table>
	</xsl:template>

	<xsl:template match="PopupHelp">
		<xsl:variable name="ctx">
		<Box Color="blue">
			<xsl:apply-templates/>
		</Box>
		</xsl:variable>
		
		<xsl:variable name="id" select="httpcontext:counter()"/>
		<span style="position: absolute" onmouseover="DHTML_ShowHide('ph{$id}', 1)" onmouseout="DHTML_ShowHide('ph{$id}', 0)" class="screenonly">
			<img src="/media/help2.gif" width="16" height="16" style="float: left; margin-left: 5px" alt=""/>
			<div id="ph{$id}" class="popuphelp">
				<xsl:for-each select="$ctx">
					<xsl:call-template name="Box"/>
				</xsl:for-each>
			</div>
		</span>
	</xsl:template>
	
	<xsl:template match="Box2">
		<!-- cellpadding prevents a FF cell height bug, means we subtract 2 from the side column widths -->
		<!-- doesn't work at all in IE -->
		<table border="0" cellpadding="1" cellspacing="0" style="{@style}">
		<xsl:if test="count(@Icon) &gt; 0">
		<tr height="96">
			<td/>
			<td style="background-image: url(/media/box-{@Icon}-a.gif); background-repeat: no-repeat; padding-left: 130px; padding-top: 1em">
				<div class="PageTitle"><xsl:apply-templates select="title/node()"/></div>
			</td>
			<td/>
		</tr>
		</xsl:if>
		<tr valign="top" height="107">
			<td width="22" height="107" style="background-image: url(/media/box-tl.gif);"/>
			<td rowspan="2" style="background-color: #d7e7fe; background-image: url(/media/box-t.gif); background-repeat: repeat-x;">
				<xsl:if test="count(@Icon) &gt; 0">
					<img src="/media/box-{@Icon}-b.gif" style="margin-top: -1px" alt=""/>
					<xsl:apply-templates select="node()[not(name)='title']"/>
				</xsl:if>
				<xsl:if test="count(@Icon) = 0">
					<div style="padding-top: 1.5em">
						<xsl:apply-templates select="node()[not(name)='title']"/>
					</div>
				</xsl:if>
			</td>
			<td width="22" height="107" style="background-image: url(/media/box-tr.gif);"/>
		</tr>
		<tr valign="top">
			<td style="background-image: url(/media/box-l.gif); background-repeat: repeat-y"></td>
			<td style="background-image: url(/media/box-r.gif); background-repeat: repeat-y"></td>
		</tr>
		<tr height="18">
			<td style="background-image: url(/media/box-bl.gif); background-repeat: no-repeat"></td>
			<td style="background-image: url(/media/box-b.gif); background-repeat: repeat-x"></td>
			<td style="background-image: url(/media/box-br.gif); background-repeat: no-repeat"></td>
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
	
	<xsl:template name="contactemailaddress" match="contactemailaddress">
		<noscript>[sorry, address shown only in JavaScript-enabled browsers]</noscript>
		<script type="text/javascript">
var addy = "snoitarepo".split("").reverse().join("") + String.fromCharCode(2*0x20) + "kcartvog".split("").reverse().join("") + "." + "su".split("").reverse().join("");
document.write(unescape('%3c') + "a href='mailto:" + addy + "'" + unescape('%3e') + addy + unescape('%3c') + "/a" + unescape('%3e'));
		</script>
	</xsl:template>
	
	<xsl:template match="cite">
		<xsl:variable name="name" select="@ref"/>
		<cite title="{/Page//References/ref[@name=$name]}"> [<a href="{/Page//References/ref[@name=$name]/@href}"><xsl:value-of select="@ref"/></a>]</cite>
	</xsl:template>
	
	<xsl:template match="References">
		<h3>References</h3>
		<ol>
		<xsl:for-each select="ref">
			<li><a href="{@href}"><xsl:value-of select="@name"/></a>: <xsl:apply-templates/></li>
		</xsl:for-each>
		</ol>
	</xsl:template>
	
	<xsl:template match="CurrentMembersSelectOptions">
		<xsl:variable name="selected" select="@selected"/>
		<xsl:variable name="type" select="@type"/>
		<xsl:variable name="reps">
			<xsl:choose>
			<xsl:when test="not(count(@year)=0) and not(@year='')">
				<xsl:copy-of select="govtrack-reps:FindByYear(@year)"/>
			</xsl:when>
			<xsl:otherwise>
				<xsl:copy-of select="govtrack-reps:FindAll()"/>
			</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>
		<xsl:variable name="reps-selected">
			<xsl:for-each select="$reps">
				<xsl:if test="id=$selected">
					<xsl:copy-of select="."/>
				</xsl:if>
			</xsl:for-each>
		</xsl:variable>
		<xsl:variable name="baddefault" select="not(@selected='') and count($reps-selected)=0"/>
		<xsl:if test="$baddefault">
			<option value="{$selected}" selected="1"><xsl:value-of select="govtrack-reps:FormatPersonName(@selected, 'now', '')"/></option>
		</xsl:if>
		<xsl:for-each select="$reps">
			<xsl:sort select="sortname"/>
			<xsl:if test="count($type)=0 or $type='' or (type='rep' and $type='House') or (type='sen' and $type='Senate')">
				<option value="{id}"><xsl:if test="id=$selected"><xsl:attribute name="selected">1</xsl:attribute></xsl:if><xsl:value-of select="name"/></option>
			</xsl:if>
		</xsl:for-each>
	</xsl:template>
	
	<xsl:template match="SocialAction">
		<xsl:variable name="ctx">
		<Box Color="blue">
			<div style="float: right; font-size: 80%; margin-left: 1em; margin-bottom: 1em">
				<div>Powered by...</div>
				<div style="background-color: white; border: 1px solid black"><a href="http://socialactions.com/"><img src="http://www.socialactions.com/related-ways-to-take-action/new_sa_logo_small.png" width="143" height="38" alt="SocialAction" border="0"/></a></div>
			</div>
			<div style="width: 500px; font-weight: bold">Ways to take action...</div>
			<div style="font-size: 90%">
			<p>Here are some ways around the web that citizens are taking action on this issue. We don’t endorse any of these websites but we think you might be interested in them.</p>
			<div id="socialactioncontent"/>
			</div>
		</Box>
		</xsl:variable>
		
		<div id="socialaction" style="display: none; margin-top: 1em">
			<xsl:for-each select="$ctx">
				<xsl:call-template name="Box"/>
			</xsl:for-each>
		</div>

		<xsl:variable name="json" select="govtrack-util:CallAPI(concat('http://search.socialactions.com/actions.json?action_types=2,3,4,5,6,8&amp;limit=4&amp;q=', '&quot;', @search, '&quot;'), 'text', 60*8)"/>
		<script type="text/javascript">
			function tag(s) {
				return unescape("%3C") + s + unescape("%3E");
			}
			sa = <xsl:value-of select="$json"/>;
			if (sa.length != 0) {
				DHTML_ShowHide('socialaction', 1);
				var html = tag("ul");
				for (var i = 0; i != sa.length; i++) {
					html += tag("li") + tag("a href=\"" + sa[i].url + "\"") + sa[i].title + tag("/a") + " at " + sa[i].site.name + tag("/li");
				}
				html += tag("/ul");
				document.getElementById('socialactioncontent').innerHTML = html;
			}
		</script>
	</xsl:template>
	
	<xsl:template match="multicol">
		<xsl:variable name="rows" select="@rows"/>
		<xsl:variable name="items" select="div"/>
		<div style="overflow: auto; width: {@width}">
			<table border="0" cellspacing="0" cellpadding="0">
			<tr valign="top">
			<xsl:for-each select="httpcontext:range(ceiling(count($items) div $rows))">
				<xsl:variable name="p" select="."/>
				<td><xsl:if test="$p &gt; 0"><xsl:attribute name="style">padding-left: 2em</xsl:attribute></xsl:if>
				<xsl:for-each select="$items[floor((position()-1) div $rows) = $p]">
					<xsl:apply-templates select="."/>
				</xsl:for-each>
				</td>
			</xsl:for-each>
			</tr>
			</table>
		</div>
	</xsl:template>
	
	<xsl:template match="select[not(httpcontext:param(@name)='')]/option[@value=httpcontext:param(parent::select/@name)]">
	  <xsl:copy>
	  	<xsl:attribute name="selected">1</xsl:attribute>
		<xsl:apply-templates select="@*|node()"/>
	  </xsl:copy>
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
