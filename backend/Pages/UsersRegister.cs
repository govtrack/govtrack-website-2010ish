using System;
using System.Collections;
using System.Collections.Specialized;
using System.Text;
using System.Xml.XPath;
using System.Web;

using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

using XPD;

namespace GovTrack.Web.Pages.Users {

	public class Register {

		public static bool DoRegistration() {
			string email = HttpContext.Current.Request["email"];
			string password = HttpContext.Current.Request["password"];
			string openidurl = HttpContext.Current.Request["openid_url"];
			string passmode = HttpContext.Current.Request["logintype"];

			// Valid fields

			if (passmode == null || passmode == "regular") {
				if (email == null || password == null || email == "" || password == "")
					throw new UserException("Enter your email address and your desired GovTrack account password.");
			
				Account.ValidateEmail(email, true);
				Account.ValidatePassword(password);
				openidurl = null;
			} else {
				throw new Exception();
			}

			//Captcha.Check();
			
			// Setup the database record
			
			Hashtable account = new Hashtable();
			account["email"] = email;
			account["password"] = password;
			account["openidurl"] = openidurl;
			account["created"] = DateTime.Now;
			account["last_login"] = account["created"];
			
			ArrayList monitors = new ArrayList();
			monitors.AddRange(Login.GetMonitors());
			
			/*if (address != null && address != "") {
				// If an address was given, monitor
				// the appropriate reps.
				ArrayList reps = (ArrayList)Reps.FindByAddress(address);
				foreach (Hashtable h in reps) {
					monitors.Add("p:" + h["id"]);
				}
			}*/

			account["monitors"] = Login.EncodeMonitors(monitors);
			
			// Add the database record and log in the user.
			
			Util.Database.DBInsert("users", account);			
			Login.DoLogin2(email, password);
			
			//Email.Send("tauberer@for.net", "GovTrack.us - New User", 
			//	"New User:\n\n" + email + "\n\n", System.Web.Mail.MailFormat.Text);
			
			// Set the user along to the next page.
			
			HttpContext.Current.Response.Redirect("/users/nowwhat.xpd");

			return true;
		}
		
		public static bool SendForgottenPassword() {
			string email = HttpContext.Current.Request.Form["email"];

			if (email == null || email == "")
				return false;
				
			//Captcha.Check();
			Account.ValidateEmail(email, false);			

			TableRow user = Util.Database.DBSelectFirst(
				"users", "password", new Database.SpecEQ("email", email));
				
			if (user == null)
				throw new UserException("There is no user in our system with that email address.");
				
			string password = (string)user["password"];
				
			StringBuilder message = new StringBuilder();
			message.Append("This is an automated message from www.GovTrack.us.  Someone requested that the password for the GovTrack account for " + email + " be sent to that address by email.\n");
			message.Append("\n");
			message.Append("The password for that address is: " + password + "\n");
			message.Append("\n");
			message.Append("If you use an OpenID URL to log into the website, you can use the email address and password indicated above to log in, rather than using your OpenID URL.\n");
			message.Append("\n");
			message.Append("If you did not request your password, please disregard this message.\n");
			message.Append("\n");
			message.Append("The request was generated from a computer with the following address: " + HttpContext.Current.Request.UserHostName + ".\n");
			message.Append("\n");
			message.Append("Thank you for using GovTrack.us!\n");
			
			Email.Send(email, "GovTrack.us - Password Enclosed", message.ToString(), System.Web.Mail.MailFormat.Text);
			
			throw new UserException("Your password has been sent in an email to " + email + ".");
		}
	
	}

}
