using System;
using System.Collections;
using System.Collections.Specialized;
using System.Net;
using System.Xml.XPath;
using System.Web;

using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

using XPD;

namespace GovTrack.Web.Pages.Users {

	public class RedirectIfNotLoggedIn : XPD.FormHandler {
		public bool Process(HttpContext context) {
			if (!Login.IsLoggedIn()) {
				context.Response.Redirect("/users/login.xpd");
				return true;
			}
			return false;
		}
	}

	public class RedirectIfLoggedIn : XPD.FormHandler {
		public bool Process(HttpContext context) {
			if (Login.IsLoggedIn()) {
				if (context.Request.QueryString["redirect"] != null)
					context.Response.Redirect(context.Request.QueryString["redirect"]);
				else
					context.Response.Redirect("/users/account.xpd");
				return true;
			}

			if (context.Request.QueryString["redirect"] != null)
				context.Items["govtrack-redirect-on-login"] = "/users/account.xpd";
			return false;
		}
	}

	public class AddRemoveMonitor : XPD.FormHandler {
		public bool Process(HttpContext context) {
			string monitor = context.Request["remove"];
			if (monitor != null) {
				Login.RemoveMonitor(monitor);
				context.Response.Redirect("/users/yourmonitors.xpd");
				return true;
			}

			monitor = context.Request["add"];
			if (monitor != null) {
				bool isValid;
				Monitor monobj = Monitor.FromString(monitor, out isValid);
				if (monobj == null && !isValid) return false;
				Login.AddMonitor(monitor);
				context.Response.Redirect("/users/yourmonitors.xpd");
				return true;
			}
			return false;
		}
	}

	public class Account {
		public static void ValidateEmail(string email, bool dupcheck) {
			string text = "You did not give a valid email address. ";
		
			if (email.Length < 5 || email.Length > 60)
				throw new UserException(text + "Email addresses must be between 5 and 60 characters long.");
			
			int at = -1;
			for (int i = 0; i < email.Length; i++) {
				char c = email[i];				
				if (c == '@') {
					if (at != -1) throw new UserException(text);
					at = i;
					continue;
				}
				if (c == '.' || c == '_' || c == '-' || c == '+') continue;
				if (char.IsLetterOrDigit(c)) continue;
				throw new UserException(text + "Invalid character: " + c + ".  Email addresses may contain only letters, numbers, @, underscore, periods and dashes.");
			}
			if (at == -1) throw new UserException(text + "An email address can only have one @ symbol.");
			
			if (dupcheck) {
				TableRow row = Util.Database.DBSelectFirst("users", "id", new Database.SpecEQ("email", email));
				if (row != null)
					throw new UserException("A user is already registered with that email address.  If that user is you, go back and try to log in instead.");
			}
		}
	
		public static void ValidatePassword(string password) {
			if (password.Length < 4 || password.Length > 18)
				throw new UserException("Passwords must be between 4 and 18 characters.");
			foreach (char c in password)			
				if (!char.IsLetterOrDigit(c))
					throw new UserException("Passwords must contain only letters and digits.");
		}
		
		public static void ValidateNick(string nick) {
			if (nick.Length < 2 || nick.Length > 20)
				throw new UserException("Nicks must be between 2 and 20 characters.");
			foreach (char c in nick)			
				if (!char.IsLetterOrDigit(c)
					&& c != ' '
					&& c != '\''
					&& c != '"'
					&& c != '-'
					&& c != '_'
					&& c != '.'
					&& c != ','
					&& c != '&')
					throw new UserException("Nicks must contain only letters, digits, spaces, and some punctuation marks.");			
		}

		public static bool EditAccount() {
			int userid = Login.CheckLogin();
			if (userid == 0)
				throw new UserException("You cannot change your account settings when you are not logged in.");
		
			string email = HttpContext.Current.Request.Form["email"];
			if (email != null && email.Trim() == "")
				email = null;
			if (email == null)
				email = Login.GetLoginEmail();
			
			string password = HttpContext.Current.Request.Form["password"];
			if (password != null && password.Trim() == "")
				password = null;

			string openidurl = HttpContext.Current.Request.Form["openidurl"];
			if (openidurl != null && openidurl.Trim() == "")
				openidurl = null;
			
			if (email != Login.GetLoginEmail()) ValidateEmail(email, true);
			if (password != null) ValidatePassword(password);

			if (HttpContext.Current.Request.Form["clearopenid"] != null && password == null)
				throw new UserException("Provide a password.");
			
			if (email == Login.GetLoginEmail() && password == null && openidurl == null)
				return false; // nothing to change
			
			Hashtable changes = new Hashtable();
			changes["email"] = email;
			if (password != null)
				changes["password"] = password;
			if (openidurl != null || HttpContext.Current.Request.Form["clearopenid"] != null)
				changes["openidurl"] = openidurl;
		
			Util.Database.DBUpdateID("users", userid, changes);
			
			if (password == null)
				password = Login.GetLoginField("password");
			
			Login.DoLogin2(email, password);
			Login.ClearLogin();
			
			HttpContext.Current.Response.Redirect("/users/account.xpd");
			
			return false;
		}
		
		public static bool EditNick() {
			int userid = Login.CheckLogin();
			if (userid == 0)
				throw new UserException("You cannot change your account settings when you are not logged in.");
				
			string nick = HttpContext.Current.Request.Form["nick"];
			if (nick == null) nick = "";
			
			if (nick != "")
				ValidateNick(nick);

			Hashtable changes = new Hashtable();
			changes["nick"] = nick;
			Util.Database.DBUpdateID("users", userid, changes);

			Login.ClearLogin();

			return false;
		}
		
		public static bool EditWebsite() {
			int userid = Login.CheckLogin();
			if (userid == 0)
				throw new UserException("You cannot change your account settings when you are not logged in.");
		
			string website = HttpContext.Current.Request.Form["website"];
			
			Hashtable changes = new Hashtable();
			if (website == null || website == "") {
				changes["website"] = null;
				Util.Database.DBUpdateID("users", userid, changes);
				Login.ClearLogin();
				return false;
			}
			
			//Captcha.Check();
			
			// Scan the website for the identity string.
			string scan = "[GovTrack-I-Am-" + Login.GetLoginEmail() + "]";
			
			if (!website.StartsWith("http://"))
				website = "http://" + website;
			
			bool found;
			try {
				found = ScanUri(website, scan);
			} catch (Exception e) {
				throw new UserException("The following error occured while trying to access your website: " + e.Message);
			}
			
			if (!found)
				throw new UserException("The special code below was not found at the top of your website.");
				
			if (website.StartsWith("http://"))
				website = website.Substring(7);
			
			if (website.StartsWith("www."))
				website = website.Substring(4);

			if (website.EndsWith("/"))
				website = website.Substring(0, website.Length-1);
				
			changes["website"] = website;
			Util.Database.DBUpdateID("users", userid, changes);
			Login.ClearLogin();
			
			throw new UserException("Website successfully updated.");
		}
		
		public static bool ScanUri(string uri, string scan) {
			WebRequest req = WebRequest.Create(uri);
			using (WebResponse resp = req.GetResponse()) {
				using (System.IO.Stream str = resp.GetResponseStream()) {
					System.Text.Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
					System.IO.StreamReader readStream = new System.IO.StreamReader(str, encode);
					char[] read = new char[2048];
					int count = readStream.Read(read, 0, read.Length);
					String stri = new String(read, 0, count);
					if (stri.IndexOf(scan) >= 0) return true;
				}
			}
			return false;
		}

		public static bool EditUpdates() {
			int userid = Login.CheckLogin();
			if (userid == 0)
				throw new UserException("You cannot change your account settings when you are not logged in.");
				
			string frequency = HttpContext.Current.Request.Form["frequency"];
			if (frequency == null) 
				throw new UserException("You did not specify an update frequency.");
			
			if (frequency != "0" && frequency != "1" && frequency != "2")
				throw new UserException("You did not specify a valid frequency.");

			string massemail = HttpContext.Current.Request.Form["allowmassemail"];
			if (massemail == null || massemail == "")  massemail = "0";
			else if (massemail == "ON") massemail = "1";
			else
				throw new UserException("Invalid form value.");
			
			Hashtable changes = new Hashtable();
			changes["emailupdates"] = frequency;
			if (Login.GetLoginField("emailupdates") == "0")
				changes["emailupdates_last"] = Util.DateTimeToUnixDate(DateTime.Now);
			changes["massemail"] = massemail;
			Util.Database.DBUpdateID("users", userid, changes);

			Login.ClearLogin();

			return false;
		}

		public static bool EditBlog() {
			int userid = Login.CheckLogin();
			if (userid == 0)
				throw new UserException("You cannot change your account settings when you are not logged in.");
		
			string blog = HttpContext.Current.Request.Form["blog"];
			
			Hashtable changes = new Hashtable();
			if (blog == null || blog == "") {
				changes["blog"] = null;
				Util.Database.DBUpdateID("users", userid, changes);
				Login.ClearLogin();
				return false;
			}
			
			string scan = "[GovTrack-I-Am-" + Login.GetLoginEmail() + "]";
			
			if (!blog.StartsWith("http://"))
				blog = "http://" + blog;
				
			string website = Login.GetLoginField("website");
			if (website == null)
				throw new UserException("You must provide a website address on the Identity tab.");
			
			if (!blog.StartsWith("http://" + website)
				&& !blog.StartsWith("http://www." + website)
				)
				throw new UserException("Your blog address does not begin with your website address: " + website);
				
			changes["blog"] = blog;
			changes["blog_error"] = "Your blog has not been scanned yet since you entered your blog address.  Blogs are scanned daily.";
			Util.Database.DBUpdateID("users", userid, changes);
			Login.ClearLogin();
			
			throw new UserException("Blog successfully updated.");
		}		
	}
}
