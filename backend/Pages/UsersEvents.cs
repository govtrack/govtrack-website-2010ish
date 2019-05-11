using System;
using System.Collections;
using System.Xml.XPath;
using System.Web;

using XPD;

using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

namespace GovTrack.Web.Pages.Users {

	public class Events {
	
		private ArrayList Monitors(bool forLinks) {
			ArrayList monitors1;
			ArrayList monitors = new ArrayList();

			string http_monitors = HttpContext.Current.Request["monitors"];
			if (http_monitors != null) {
				// Load monitors from user profile.
				string profprefix = "user-profile:";
				if (!forLinks && http_monitors.StartsWith(profprefix)) {
					int userid = int.Parse(http_monitors.Substring(profprefix.Length));
					TableRow profile = Util.Database.DBSelectID("users", userid, "monitors");
					http_monitors = (string)profile["monitors"];
					if (http_monitors == null) http_monitors = "";
				}
				monitors1 = Login.ParseMonitors(http_monitors);
			} else {
				if (forLinks && Login.IsLoggedIn()) {
					ArrayList ret = new ArrayList();
					ret.Add( "user-profile:" + Login.GetLoginId() );
					return ret;
				}	
		
				monitors1 = Login.GetMonitors();
			}
			
			foreach (string m in monitors1)
				if (Monitor.FromString(m) != null)
					monitors.Add(m);

			return monitors;
		}

		public bool ShowLinkToOwnEvents() {
			if (Login.GetMonitors().Count == 0) return false;
			string http_monitors = HttpContext.Current.Request["monitors"];
			if (http_monitors == null) return false;
			return true; // user is viewing other monitors & has his own monitors
		}
	
		public object GetMonitors() {
			ArrayList monitors = Monitors(false);
			return monitors;
		}
		
		public string GetMonitorsEncoded() {
			string ret = "monitors=" + Login.EncodeMonitors(Monitors(true));
			if (HttpContext.Current.Request["people"] != null)
				ret += "&people=" + HttpContext.Current.Request["people"];
			return ret;
		}
	
		public string GetMonitorsEncodedFromQuery() {
			string ret =
			"monitors=" +
			  Login.EncodeMonitors(Login.ParseMonitors(HttpContext.Current.Request["monitors"]))
			  .Replace(" ", "+");
			string people = HttpContext.Current.Request["people"];
			if (people != null && people != "")
				ret += "&people=" + people;
			return ret;
		}
		
		public ArrayList GetEvents(int backdays, int norelativepaths, int ical) {
			if (backdays == -1) {
				int[] t = { 1, 2, 3, 5, 7, 10, 14, 21 };
				ArrayList e = null;
				for (int i = 0; i < t.Length; i++) {
					e = GetEvents(t[i], norelativepaths, ical);
					if (e.Count > 25) break;
				}
				return e;
			}
		
			if (norelativepaths == 1)
				HttpContext.Current.Items["govtrack-UseGlobalPath"] = this; // must be non-null		

			string days = HttpContext.Current.Request["days"];
			if (days != null)
				backdays = int.Parse(days);
			DateTime starttime = DateTime.Today - TimeSpan.FromDays(backdays);
		
			string since = HttpContext.Current.Request["since"];
			if (since != null && since == "yesterday") {
				starttime = DateTime.Today - TimeSpan.FromDays(2);
			}
		
			ArrayList mon = Monitors(false);
			
			if (HttpContext.Current.Request["options"] != null)
				mon.AddRange( Login.ParseMonitors(HttpContext.Current.Request["options"]) );
			
			Monitor.Options monoptions = new Monitor.Options();
			monoptions.MeetingsByEventDate = (ical == 1);
			
			ArrayList events = Event.GetTrackedEvents(
				mon,
				starttime,
				DateTime.Today + TimeSpan.FromDays(1),
				monoptions);
			
			string people = HttpContext.Current.Request["people"];
			if (people == null) {
				foreach (string m in Login.GetMonitors())
					if (m.StartsWith("p:") && !mon.Contains(m))
						mon.Add(m);
			} else {
				foreach (string person in people.Split(',')) {
					int pid = int.Parse(person);
					string m = "p:" + pid;
					if (!mon.Contains(m))
						mon.Add("p:" + pid);
				}
			}

			Event.Options evoptions = new Event.Options();
			evoptions.AllMonitors = mon;			

			ArrayList ret = new ArrayList();
			foreach (Event e in events) {
				ret.Add( e.ToHashtable(evoptions) );
			}
				
			return ret;
		}
		
		public object GetVoteEventsPeople() {
			ArrayList ret = new ArrayList();
			string people = HttpContext.Current.Request["people"];
			if (people == null) people = "";
			ret.AddRange(people.Split(','));			
			return ret;
		}
		
		public object GetVoteEvents() {
			HttpContext.Current.Items["govtrack-UseGlobalPath"] = this; // must be non-null
		
			ArrayList mon = new ArrayList();
			mon.Add("misc:allvotes");
			
			int backdays = 14;
			string days = HttpContext.Current.Request["days"];
			if (days != null)
				backdays = int.Parse(days);
			
			ArrayList events = Event.GetTrackedEvents(
				mon,
				DateTime.Today - TimeSpan.FromDays(backdays),
				DateTime.Today + TimeSpan.FromDays(1),
				new Monitor.Options());
				
			string people = HttpContext.Current.Request["people"];
			if (people == null) people = "";
				
			foreach (string person in people.Split(',')) {
				if (person == "") continue;
				int pid = int.Parse(person);
				mon.Add("p:" + pid);
			}
			
			Event.Options evoptions = new Event.Options();
			evoptions.AllMonitors = mon;

			ArrayList ret = new ArrayList();
			foreach (Event e in events) {
				ret.Add( e.ToHashtable(evoptions) );
			}
				
			return ret;
		}

	}

}
