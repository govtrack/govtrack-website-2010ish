// created on 7/25/2004 at 4:53 PM

using System;
using System.IO;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;
using System.Web;
using System.Web.Mail;

namespace GovTrack.Web {

	public class Email {

		public static void SendToWebmaster(string subject, string body) {
			body += "\n\n==============================\n\n";
			body += HttpContext.Current.Request.UserHostName + "\n";
			if (Login.IsLoggedIn())
				body += Login.GetLoginEmail() + "\n";
		
			Send("operations@govtrack.us", subject, body, MailFormat.Text);
		}
		
		public static void Send(string to, string subject, string body, MailFormat format) {
			MailMessage message = new MailMessage();
			
			message.From = "\"GovTrack.us\" <automated.no.replies@govtrack.us>";
			message.Subject = subject;
			message.To = to;
			message.Body = body;
			message.BodyFormat = format;
		
			SmtpMail.Send(message);
		}
		
		public static bool EmailThisPage1() {
			string recips = HttpContext.Current.Request.Form["recipients"];
			if (recips == null || recips == "")
				throw new UserException("Enter a list of recipients, separated by commas.");
				
			string sender = HttpContext.Current.Request.Form["sender"];
			if (sender == null || sender == "")
				throw new UserException("Enter your name so that the recipient(s) know who sent the email.");
				
			if (recips.Length > 100 || recips.Split(',').Length > 8)
				throw new UserException("You have entered too many email addresses.");

			//Captcha.Check();
			
			HttpContext.Current.Items["govtrack-page-action"] = "email";
			
			return false;
		}
		
		public static string EmailThisPage2(XPathNavigator body, string title, XPathNodeIterator param) {
			if (HttpContext.Current.Items["govtrack-page-action"] == null
				|| (string)HttpContext.Current.Items["govtrack-page-action"] != "email")
				return "";

			string recips = HttpContext.Current.Request.Form["recipients"];
			string sender = HttpContext.Current.Request.Form["sender"];
			string message = HttpContext.Current.Request.Form["message"];
				
			sender = sender.Replace("&", "&amp;");
			sender = sender.Replace("<", "&lt;");
			sender = sender.Replace(">", "&gt;");

			if (message == null || message.Trim() == "") message = "No message was provided.";
			message = message.Replace("&", "&amp;");
			message = message.Replace("<", "&lt;");
			message = message.Replace(">", "&gt;");
			message = " <p style=\"margin: .5em; padding: .5em; border: thin solid #DDDDDD;\">" + message + "</p>";
			
			// Transform the page to text
			
			string basehref = HttpContext.Current.Request.FilePath;
			string url = HttpContext.Current.Request.Url.ToString();
			url = url + "?";
			while (param.MoveNext())
				url = url + "&" + param.Current.Value + "=" + HttpContext.Current.Request.Form[param.Current.Value];

			XmlDocument stylesheet = new XmlDocument();
			stylesheet.LoadXml(
				  "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0'>"
				+ "<xsl:output method='html' encoding='ascii' />"
				+ "<xsl:template match='/'>"
				+ "<html>"
				+ "<head>"
				+ "<base href='http://www.govtrack.us" + basehref + "'/>"
				+ "<link rel='stylesheet' type='text/css' href='/stylesheet.css'/>"
				+ "<script type=\"text/javascript\" src=\"/scripts/menus.js\"/>"
				+ "</head>"
				+ "<body bgcolor=\"white\">"
				+ " <p>Hello! " + sender + " has sent this page to you from <a href=\"http://www.govtrack.us\">www.GovTrack.us</a> with the following message:</p>"
				+ message
				+ " <p>The page below can also be found at the following address: <a href=\"" + url.Replace("&", "&amp;") + "\">" + url.Replace("&", "&amp;") + "</a>.</p>"
				+ "<hr/>"
				+ "	<xsl:apply-templates/>"
				+ "</body>"
				+ "</html>"
				+ "</xsl:template>"
				+ "<xsl:template match='@*|node()'><xsl:copy><xsl:apply-templates select='@*|node()'/></xsl:copy></xsl:template>"
				+ "</xsl:stylesheet>"
				);
			XslTransform transform = new XslTransform();
			transform.Load(stylesheet, null, null);
			
			StringWriter stringwriter = new StringWriter();
			XmlTextWriter writer = new XmlTextWriter(stringwriter);
			
			transform.Transform(body, null, writer, null);
			
			// Send the email
			
			Send(recips, "GovTrack.us - " + title, stringwriter.ToString(), MailFormat.Html);
			//Send(..email address.., "GovTrack.us [Monitor] - " + title, stringwriter.ToString(), MailFormat.Html);
			
			// Redirect the user away from this post page
				
			HttpContext.Current.Response.Redirect(url);
		
			return "";
		}
	
	}


}
