using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;

using System.Net.Mail;
//using DotNetOpenMail;

using XPD;

namespace GovTrack.Web {

	class EmailUpdates {
	
		static int CurrentUserId;
		static bool HadEvents;
		static DateTime EndDate;
		static object users_events;
		static string DateRange;

		static int NumEmails = 0;

		public static void Main(string[] args) {
			if (args.Length == 0) {
				Console.Error.WriteLine("Specify 'daily' or 'weekly' or 'test' as the command-line argument.");
				return;
			}

			DateTime startTime = DateTime.Now;

			int emailupdatesfreq = 0;
			bool testing = false;
			if (args[0] == "daily")
				emailupdatesfreq = 1;
			else if (args[0] == "weekly")
				emailupdatesfreq = 2;
			else if (args[0] == "test")
				testing = true;
			else {
				Console.Error.WriteLine("Specify 'daily' or 'weekly' as the command-line argument.");
				return;
			}

			AppModule.InitData(false);
			XPD.Database.SingleThreadedConnection = true;
		
			// Load watchevents stylesheet
			
			XmlDocument stylesheet = new XmlDocument();
			stylesheet.Load("/home/govtrack/website/users/watchevents.xsl");
			
			// Add a namespace node to load our extension object
			stylesheet.DocumentElement.SetAttribute("xmlns:govtrack-emailupdates", "assembly://GovTrackEmailUpdates/GovTrack.Web.EmailUpdates,GovTrackEmailUpdates");
			
			// Add an xsl:variable node to load in the events
			XmlElement var = stylesheet.CreateElement("xsl", "variable", "http://www.w3.org/1999/XSL/Transform");
			stylesheet.DocumentElement.AppendChild(var);
			var.SetAttribute("name", "events");
			var.SetAttribute("select", "govtrack-emailupdates:GetEvents()");

			// Add an xsl:variable node to load in the monitors
			var = stylesheet.CreateElement("xsl", "variable", "http://www.w3.org/1999/XSL/Transform");
			stylesheet.DocumentElement.AppendChild(var);
			var.SetAttribute("name", "monitors");
			var.SetAttribute("select", "govtrack-emailupdates:GetMonitors()");
			
			// Add an xsl:output node
			XmlElement output = stylesheet.CreateElement("xsl", "output", "http://www.w3.org/1999/XSL/Transform");
			stylesheet.DocumentElement.AppendChild(output);
			output.SetAttribute("method", "html");
			output.SetAttribute("encoding", "utf-8");
			
			// Load in extension objects
			XsltArgumentList xsltargs = new XsltArgumentList();
			foreach (DictionaryEntry ext in XPD.XSLTPipeline.LoadExtensions(stylesheet)) {
				xsltargs.AddExtensionObject((string)ext.Key, ext.Value);		
			}
			
			// Load the transform and the template
			XslTransform transform = new XslTransform();
			transform.Load(stylesheet, null, null);			

			string announcetext = "";
			string announcefile = "/home/govtrack/emailupdates-announce.html";
			if (File.Exists(announcefile)) {
				using (StreamReader reader = new StreamReader(announcefile))
					announcetext = reader.ReadToEnd();
			}
				
			List<int> users = new List<int>();
			if (testing)
				users.AddRange( new int[] { 9 } );
			else
				foreach (int uid in Util.Database.DBSelectVector("users", "id", new Database.SpecEQ("emailupdates", emailupdatesfreq)))
					users.Add(uid);
			users.Sort();
			
			int consecfailures = 0;
			
			foreach (int userid in users) {
				//if (userid <= 52466) continue;

				CurrentUserId = userid;
				TableRow user = Util.Database.DBSelectID("users", CurrentUserId, "email, monitors, emailupdates_last, CONVERT(md5(email) USING latin1) as emailhash");

				Console.WriteLine(userid + "\t" + user["email"]);
			
				HadEvents = false;
				GetEventsInternal(testing);
				if (!HadEvents) {
					if (!testing)
						continue;
					else
						Console.WriteLine("\tThere are no events to send but since we're testing we'll send anyway.");
				}

				Console.WriteLine(userid + "\t" + user["email"]);
	
				string textpart = null, htmlpart = null;

				foreach (string format in new string[] { "text", "html" }) {
				
				StringWriter writer = new StringWriter();
				if (format == "html") {
					writer.WriteLine("<html>");
					writer.WriteLine("<head>");
					writer.WriteLine("<style>");
					writer.WriteLine("\tbody { font-family: sans-serif; }");
					writer.WriteLine("\t.date { font-size: 90%; color: #AA9999; font-weight: bold; }");
					writer.WriteLine("\t.welcome { font-family: Verdana; font-size: 90%; text-align: justify; line-height: 155% } ");
					writer.WriteLine("\t.welcome a { font-weight: bold; }");
					writer.WriteLine("\ta.light { font-weight: normal; text-decoration: none; color: #444488; }");
					writer.WriteLine("\ta.light:hover { text-decoration: underline }");
					writer.WriteLine("</style>");
					writer.WriteLine("</head>");
					writer.WriteLine("<body>");
					writer.WriteLine("<img src=\"http://www.govtrack.us/perl/emailupdate_trackback.cgi?userid=" + userid + "&emailhash=" + user["emailhash"] + "&timestamp=" + Util.DateTimeToUnixDate(DateTime.Now) + "\" width=0 height=0/>");
				}
				
				writer.WriteLine(announcetext);

				writer.WriteLine(Tag("h2", "GovTrack.us Tracked Events Update", format));
				
				writer.WriteLine(Tag("p", "This is your email update from " 
					+ Tag("a", "href=\"http://www.govtrack.us\"", "www.GovTrack.us", format) 
					+ ". "
					+ "To change your email updates settings including to unsubscribe, go to your "
					+ (format == "html" ?
						@"<a href=""http://www.govtrack.us/users/yourmonitors.xpd"">account settings</a> page"
						: "account settings page at <http://www.govtrack.us/users/yourmonitors.xpd>"
						)
					+ "."
					, format));

				writer.WriteLine();
				
				try {
					writer.WriteLine((format == "html" ? "<p>" : "") + "You are currently monitoring: ");
					if (user["monitors"] == null) continue;
					ArrayList monitors = Login.ParseMonitors((string)user["monitors"]);
					bool firstm = true;
					foreach (string mon in monitors) {
						Monitor m = Monitor.FromString(mon);
						if (m == null) continue;
						if (!firstm) writer.Write(", "); firstm = false;
						writer.Write(m.Display());
					}
					writer.WriteLine("." + (format == "html" ? "</p>" : ""));

					writer.WriteLine();

					XmlDocument template = new XmlDocument();
					template.LoadXml("<watchevents format='" + format + "'/>");

					transform.Transform(template, xsltargs, writer, null);
				} catch (Exception e) {
					Console.WriteLine("There was an error sending email updates for user " + user["email"] + "\n" + e + "\n");
					continue;
				}

				if (format == "html") {
					writer.WriteLine("<hr>");
				}

				writer.WriteLine(Tag("p", @"""The right of representation in the legislature [is] a right inestimable to [the people], and formidable to tyrants only."" --" + Tag("a", @"href=""http://etext.virginia.edu/jefferson/quotations/""", "Thomas Jefferson", format) + ": Declaration of Independence, 1776.", format));
				
				if (format == "html") {
					writer.WriteLine("</body>");
					writer.WriteLine("</html>");
				}

				if (format == "text") textpart = writer.ToString();
				if (format == "html") htmlpart = writer.ToString();
				
				} // format
				
				if (textpart == null || htmlpart == null) continue;

				if (testing && userid != 9) continue; // double check!
				
				Console.WriteLine(" - content");

				/* DotNetOpenMail
				EmailMessage emailMessage = new EmailMessage();
				emailMessage.EnvelopeFromAddress = new EmailAddress("automated+uid_" + userid + "@govtrack.us");
				emailMessage.FromAddress = new EmailAddress("operations@govtrack.us", "GovTrack.us");
				emailMessage.AddToAddress(new EmailAddress((string)user["email"]));	
				emailMessage.Subject = "GovTrack.us Tracked Events for " + DateRange;
				emailMessage.TextPart = new TextAttachment(textpart);
				emailMessage.HtmlPart = new HtmlAttachment(htmlpart);
				emailMessage.AddCustomHeader("Precedence", "bulk");
				*/
				
				MailMessage emailMessage = new MailMessage();
				emailMessage.From = new MailAddress("automated+uid_" + userid + "@govtrack.us", "GovTrack.us");
				emailMessage.To.Add(new MailAddress((string)user["email"]));	
				emailMessage.Subject = "GovTrack.us Tracked Events for " + DateRange;
				emailMessage.Body = textpart;
				emailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlpart, null, "text/html"));
				emailMessage.Headers["Precedence"] = "bulk";

				// Try sending six times, breaking 3 seconds
				// between attempts.
				int numAttempts = 0;
				while (true) {
					numAttempts++;
					if (numAttempts == 6) {
						consecfailures++;
						break;
					}
					try {
						Console.WriteLine(" - send");
						//emailMessage.Send(new SmtpServer("localhost"));
						new SmtpClient("localhost").Send(emailMessage);
						consecfailures = 0;
						break;
					} catch (System.Net.Sockets.SocketException e) {
						Console.WriteLine(e);
						System.Threading.Thread.Sleep(3 * 1000);
					} catch (DotNetOpenMail.MailException e) {
						Console.WriteLine(e);
						System.Threading.Thread.Sleep(3 * 1000);
					} catch (Exception e) {
						Console.WriteLine(e);
						consecfailures++;
						break;
					}
				}
				
				if (consecfailures == 5) {
					throw new Exception("Failed to send 5 messages in a row. Aborting.");
				}

				if (!testing) {
					// Update the user's emailupdates_last field.
					Hashtable update_last_date = new Hashtable();
					update_last_date["emailupdates_last"] = Util.DateTimeToUnixDate(EndDate);
					Util.Database.DBUpdateID("users", userid, update_last_date);
				}
				
				Console.WriteLine(" - done");

				NumEmails++;
				System.Threading.Thread.Sleep(200);
			}

			Console.WriteLine("{0} emails: {1}, time to send: {2}", args[0], NumEmails, DateTime.Now-startTime);
		}

		private static string Tag(string tag, string body, string format) {
			return Tag(tag, null, body, format);
		}
		private static string Tag(string tag, string attrs, string body, string format) {
			if (format == "text") {
				if (tag == "h2" || tag == "p") body += "\n";
				if (tag == "h2") body += "=====================================\n";
				return body;
			}
			return "<" + tag + (attrs != null ? " " : "") + attrs + ">" + body + "</" + tag + ">";
		}
		
		public static object GetMonitors() {
			TableRow user = Util.Database.DBSelectID("users", CurrentUserId, "monitors");
			ArrayList monitors = Login.ParseMonitors((string)user["monitors"]);
			return monitors;
		}

		public static object GetEvents() {
			return users_events;
		}

		public static void GetEventsInternal(bool testing) {
			TableRow user = Util.Database.DBSelectID("users", CurrentUserId, "monitors, emailupdates_last");
			ArrayList monitors = Login.ParseMonitors((string)user["monitors"]);

			Monitor.Options monoptions = new Monitor.Options();
			
			Event.Options evoptions = new Event.Options();
			evoptions.AllMonitors = monitors;
			
			DateTime from = Util.UnixDateToDateTime((int)user["emailupdates_last"]);
			DateTime to = DateTime.Today; // i.e. the very start of today

			if (testing)
				from = to.AddDays(-30);

			if (from > to - TimeSpan.FromSeconds(60*60*12))
				return;

			EndDate = to;
			
			ArrayList events = Event.GetTrackedEvents(monitors, from, to, monoptions);
			if (events.Count > 0)
				HadEvents = true;
				
			DateRange = from.ToString("d");
			if (DateRange != to.AddDays(-.5).ToString("d"))
				DateRange += " - " + to.AddDays(-.5).ToString("d");
			
			//Console.Error.WriteLine("Doing email updates for " + user["email"] + ": " + events.Count + " events.");
			
			ArrayList ret = new ArrayList();
			foreach (Event e in events) {
				ret.Add( e.ToHashtable(evoptions) );
			}
				
			users_events = ret;
		}
	}

}
