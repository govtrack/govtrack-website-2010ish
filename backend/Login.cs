using System;
using System.Collections;
using System.Collections.Specialized;
using System.Xml;
using System.Xml.XPath;
using System.Web;

using XPD;

namespace GovTrack.Web {

	public class Login {

		static readonly string login_db_fields = "id, email, password, monitors, emailupdates, openidurl, massemail, autostatus";

		public static void ClearLogin() {
			HttpContext.Current.Items["govtrack-govtrack-login"] = null;
		}
	
		public static int CheckLogin() {
			TableRow login = (TableRow)HttpContext.Current.Items["govtrack-govtrack-login"];
			if (login != null)
				return (int)login["id"];
					
			HttpCookie cookie = HttpContext.Current.Request.Cookies["govtrack-login"];
			if (cookie == null || cookie.Value == null || cookie.Value == "") return 0;
			
			string[] args = cookie.Value.Split(':');
			if (args.Length != 3) return 0;
			
			string cookieversion = args[0];
			string idstring = args[1];
			string hash = args[2];
			
			if (cookieversion != "0") return 0;
			
			int id;
			try {
				id = int.Parse(idstring);
			} catch (Exception e) {
				return 0;
			}
			
			TableRow user = null;
			try {
				user = Util.Database.DBSelectID("users", id, login_db_fields);
			} catch { }
			if (user == null) return 0;
			
			string dbhash = ComputeHash((string)user["email"], (string)user["password"]);
			if (dbhash != hash) return 0;

			HttpContext.Current.Items["govtrack-govtrack-login"] = user;
			
			Hashtable last_login = new Hashtable();
			last_login["last_login"] = DateTime.Now;
			Util.Database.DBUpdateID("users", id, last_login);

			return id;
		}
		
		public static TableRow GetLogin() {
			if (CheckLogin() == 0) throw new InvalidOperationException("Not logged in.");
			return (TableRow)HttpContext.Current.Items["govtrack-govtrack-login"];
		}
		
		public static string GetLoginId() {
			return CheckLogin().ToString();
		}
		
		public static string GetLoginEmail() {
			return (string)GetLogin()["email"];
		}

		public static string GetLoginField(string field) {
			object ret = GetLogin()[field];
			if (ret == null) return "";
			return ret.ToString();
		}
		
		public static bool IsLoggedIn() {
			return CheckLogin() != 0;
		}
		
		public static ArrayList GetMonitors() {
			ArrayList monitors = (ArrayList)HttpContext.Current.Items["govtrack-govtrack-monitors"];
			if (monitors != null) return monitors;
		
			int id = CheckLogin();
			if (id == 0) {
				HttpCookie cookie = HttpContext.Current.Request.Cookies["govtrack-monitors"];
				if (cookie != null && cookie.Value != null)
					monitors = ParseMonitors(cookie.Value);
				else
					monitors = new ArrayList();
			} else {
				string m = (string)GetLogin()["monitors"];
				if (m == null) m = "";
				monitors = ParseMonitors(m);
			}
			
			HttpContext.Current.Items["govtrack-govtrack-monitors"] = monitors;
			
			return monitors;
		}

		public static bool HasMonitors() {
			return GetMonitors().Count > 0;
		}
		
		public static void AddMonitor(string monitor) {
			ArrayList monitors = GetMonitors();
			if (monitors.Contains(monitor)) return;
			monitors.Add(monitor);
			CommitMonitors(monitors);
		}
		
		public static void RemoveMonitor(string monitor) {
			ArrayList monitors = GetMonitors();
			if (!monitors.Contains(monitor)) return;
			monitors.Remove(monitor);
			CommitMonitors(monitors);
		}
		
		private static void CommitMonitors(ArrayList monitors) {
			string list = EncodeMonitors(monitors);
			
			int id = CheckLogin();
			if (id == 0) {
				HttpCookie cookie = new HttpCookie("govtrack-monitors", list);
				cookie.Expires = DateTime.Now.AddDays(60);
				HttpContext.Current.Response.Cookies.Remove("govtrack-monitors");
				HttpContext.Current.Response.Cookies.Add(cookie);
			} else {
				Hashtable updates = new Hashtable();
				updates["monitors"] = list;
				Util.Database.DBUpdateID("users", id, updates);
			}

			HttpContext.Current.Items["govtrack-govtrack-monitors"] = monitors;
		}
		
		public static string EncodeMonitors(ArrayList monitors) {
			string[] values = new string[monitors.Count];
			for (int i = 0; i < monitors.Count; i++)
				values[i] = MonitorEscape((string)monitors[i]);
			string list = String.Join(",", values);
			return list;
		}
		
		public static ArrayList ParseMonitors(string monitors) {
			ArrayList ret = new ArrayList();
			foreach (string monitor in monitors.Split(','))
				if (monitor != "")
					ret.Add( MonitorUnescape(monitor) );
			return ret;
		}
		
		public static string MonitorEscape(string monitor) {
			monitor = monitor.Replace("$", "$$");
			monitor = monitor.Replace("\\", "$/");
			monitor = monitor.Replace(",", "$C");
			return monitor;
		}
		public static string MonitorUnescape(string monitor) {
			monitor = monitor.Replace("%COMMA%", ",");
			monitor = monitor.Replace("\\;", ",");
			monitor = monitor.Replace("\\\\", "\\");
			monitor = monitor.Replace("$/", "\\");
			monitor = monitor.Replace("$;", ",");
			monitor = monitor.Replace("$C", ",");
			monitor = monitor.Replace("$$", "$");
			return monitor;
		}
		
		public static bool HasMonitor(string monitor) {
			return GetMonitors().Contains(monitor);
		}
		
		public object GetMonitorsOfType(string type) {
			ArrayList ret = new ArrayList();
			foreach (string monitor in GetMonitors())
				if (monitor.StartsWith(type + ":"))
					ret.Add(monitor);
			return ret;
		}
		
		public object MonitorLink(string monitor) {
			Hashtable ret = new Hashtable();
			
			Monitor m = Monitor.FromString(monitor);
			if (m == null)
				throw new UserException("Invalid tracker: " + monitor);
			ret["href"] = m.Link();
			ret["title"] = m.Display();
			ret["stale"] = m.Stale();
		
			return ret;
		}
		
		public XPathNavigator MonitorMatch(string prefix, XPathNodeIterator items) {
			ArrayList monitors = GetMonitors();
		
			XmlDocument d = new XmlDocument();
			d.LoadXml("<root/>");
			while (items.MoveNext()) {
				string m = prefix + ":" + items.Current.Value;
				if (monitors.Contains(m)) {
					XmlElement e = d.CreateElement("monitor");
					e.InnerText = m;
					d.DocumentElement.AppendChild(e);
				}
			}
			return d.DocumentElement.CreateNavigator();
		}
		
		public static string DoLogout() {
			if (IsLoggedIn()) {
				HttpContext.Current.Response.Cookies.Add(
					new HttpCookie("govtrack-login", ""));
				ClearLogin();
				HttpContext.Current.Response.Redirect("/users/logout.xpd");
			}
			return "";
		}
		
		public static bool DoLogin() {
			string email = HttpContext.Current.Request["email"];
			string password = HttpContext.Current.Request["password"];

			if (email == null || email.Trim() == "")
				throw new UserException("Enter your email address or your OpenID identity URL to log in.");

			string redirect = HttpContext.Current.Items["govtrack-redirect-on-login"] as string;
			if (redirect == null)
				redirect = HttpContext.Current.Request["redirect"];

			if (password == null || password.Trim() == "") {
				if (email.IndexOf('@') != -1)
					throw new UserException("You must enter your GovTrack login password. If you are trying to log in with an OpenID URL, you have not entered a valid OpenID URL. OpenID URLs are not email addresses and do not have @ signs.");
				else
					throw new UserException("I don't recognize that email address or OpenID URL.");
			}

			DoLogin2(email, password);
			
			if (redirect != null) {
				HttpContext.Current.Response.Redirect(redirect);
				return true;
			}
			
			return false;
		}
		
		public static bool DoLogin2(string email, string password) {
			GovTrack.Web.Pages.Users.Account.ValidateEmail(email, false);
			GovTrack.Web.Pages.Users.Account.ValidatePassword(password);
			
			TableRow user = Util.Database.DBSelectFirst(
				"users", login_db_fields, new Database.SpecEQ("email", email));
					
			if (user == null || (string)user["password"] != password)
				throw new UserException("No account exists with that email address/password pair.");
			
			HttpCookie cookie = new HttpCookie("govtrack-login",
					"0:" + user["id"] + ":" + ComputeHash(email, password));
			cookie.Expires = DateTime.Now.AddDays(60);
 			HttpContext.Current.Response.Cookies.Add(cookie);
			HttpContext.Current.Items["govtrack-govtrack-login"] = user;
			
			return false;
		}
		
		private static string ComputeHash(string email, string password) {
			string hash = email.ToLower() + "|" + password;

			byte[] bytes = System.Text.Encoding.Unicode.GetBytes(hash);

			System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
			bytes = md5.ComputeHash(bytes);
			
			System.Text.StringBuilder builder = new System.Text.StringBuilder();
			foreach (byte b in bytes) {
				builder.Append( (char)('A' + (b % 16)) );
				builder.Append( (char)('A' + (b / 16)) );
			}
			
			return builder.ToString();			
		}
		
		public static string DoRpxLogin() {
			string redirect = HttpContext.Current.Request["redirect"];

			string token = HttpContext.Current.Request["token"];
			XmlElement rpx = new Rpx("26e68488dd7f567b1b0934bb6e2851d721f8473f", "https://rpxnow.com").AuthInfo(token);
			if (rpx == null)
				return "An unexpected error occurred. Please try again.";
				
			XmlElement id = (XmlElement)rpx.SelectSingleNode("profile/identifier");
			if (id == null || id.InnerText.Trim() == "")
				return "An unexpected error occurred. Please try again. (RPX did not return an identifier.)";
				
			// If we have this identifier in our database already, log
			// the user in.
			
			TableRow user_openid = Util.Database.DBSelectFirst(
				"users", "email, password", new Database.SpecEQ("openidurl", id.InnerText));
			if (user_openid != null) {

				DoLogin2((string)user_openid["email"], (string)user_openid["password"]);
				
				if (redirect == null || redirect.Trim() == "")
					redirect = "/users/account.xpd";

			} else {
				// Otherwise, create an account if the email validates.
				
				//Console.Error.WriteLine(rpx.OuterXml);
				
				XmlElement email = (XmlElement)rpx.SelectSingleNode("profile/verifiedEmail");
				if (email == null)
					email = (XmlElement)rpx.SelectSingleNode("profile/email");
				if (email == null)
					return "Your third-party account did not provide your email address to GovTrack, which you need in order to create an account. Change your third-party account settings to include an email address or use a different third-party account. You can also create a GovTrack username/password to register.";
				try {
					GovTrack.Web.Pages.Users.Account.ValidateEmail(email.InnerText, true);
				} catch (UserException ex) {
					return "Your third-party account provided an invalid email address: " + ex.Message + " (Identity/OpenID URL: " + id.InnerText + ")";
				}

				// Create a random password for the account to allow
				// for logins the old-fashioned way.

				string password = "";
				Random random = new Random();
				for (int i = 0; i < 10; i++)
					password += (char)((int)'A' + random.Next(26));

				// Setup the database record
				
				Hashtable account = new Hashtable();
				account["email"] = email.InnerText;
				account["password"] = password;
				account["openidurl"] = id.InnerText;
				account["created"] = DateTime.Now;
				account["last_login"] = account["created"];
			
				ArrayList monitors = new ArrayList();
				monitors.AddRange(Login.GetMonitors());
			
				account["monitors"] = Login.EncodeMonitors(monitors);
				
				// Add the database record and log in the user.
			
				Util.Database.DBInsert("users", account);			
				Login.DoLogin2(email.InnerText, password);

				if (redirect == null || redirect.Trim() == "")
					redirect = "/users/nowwhat.xpd";
			}
				
			// Send the user on his way.
			
			HttpContext.Current.Response.Redirect(redirect);

			return "";
		}
		
		public static bool AddRemoveMonitor() {
			string action = HttpContext.Current.Request.Form["action"];
			string type = HttpContext.Current.Request.Form["type"];
			string term = HttpContext.Current.Request.Form["term"];
			
			try {
				Monitor m = Monitor.FromString(type + ":" + term);
				if (m == null)
					throw new UserException("Invalid request.");
			} catch (ArgumentException argexc) {
				throw new UserException("Invalid request.");
			}			
			
			if (action == "monitor") {
				AddMonitor(type + ":" + term);
			} else if (action == "unmonitor") {
				RemoveMonitor(type + ":" + term);
			} else {
				throw new UserException("Invalid request.");
			}

			string redirect = HttpContext.Current.Request.Url.ToString() + "?";
			foreach (string k in HttpContext.Current.Request.Form.Keys)
				if (k.StartsWith("pass."))
					redirect += "&" + k.Substring(5) + "=" + HttpUtility.UrlEncode(HttpContext.Current.Request.Form[k]);

			HttpContext.Current.Response.Redirect(redirect);
			
			return false;
		}
		
		public static bool Throttle(int hours, int max) {
			Util.Database.DBDelete(
				"accesses",
				new Database.UserSpec("(NOW()-time) > " + (60*60*24)));

			string[] ipaddr = HttpContext.Current.Request.UserHostAddress.Split('.');
			long ip = 0;
			for (int i = 0; i < 4; i++)
				ip += int.Parse(ipaddr[i]) << (8*i);
		
			TableRow row = Util.Database.DBSelectFirst(
				"accesses", "time, count",
				new Database.SpecEQ("ipaddr", ip),
				new Database.SpecEQ("request", HttpContext.Current.Request.RawUrl));

			int count = 0;
			if (row != null) {
				DateTime time = (DateTime)row["time"];
				if ((DateTime.Now-time).Hours < hours)
					count = (int)row["count"];
			}
			count++;
			
			bool ok = count < max;
			
			if (row != null) {
				row["count"] = count;
				if (count == 1)
					row["time"] = DateTime.Now;
				Util.Database.DBUpdate("accesses", row, "ipaddr", "request");
			} else {
				Hashtable update = new Hashtable();
				update["ipaddr"] = ip;
				update["request"] = HttpContext.Current.Request.RawUrl;
				update["count"] = count;
				update["time"] = DateTime.Now;
				update["remotehost"] = HttpContext.Current.Request.UserHostName;
				update["useragent"] = HttpContext.Current.Request.UserAgent;
				Util.Database.DBInsert("accesses", update);
			}
			
			return ok;
		}
	}

}
