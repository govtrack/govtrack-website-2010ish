using System;
using System.Collections;
using System.Xml;
using System.Xml.XPath;
using System.Web;

using GovTrack;
using GovTrack.Web;

namespace GovTrack.Web.Pages {

	public class Index {
		static string nextsession;
		static Hashtable PopularMonitors;
		static ArrayList PopularBills;

		public static void Init(bool allowDelayedLoad) {
			//nextsession = LoadNextSession();

			AppModule.Run(allowDelayedLoad, delegate(object state) {
				LoadPopularMonitors();
				LoadPopularBills(); // after LoadPopularMonitors
			});
		}
	
		public XPathNavigator StatsSummary() {
			return Util.LoadCacheData(Util.CurrentSession, "stats.summary");
		}
		
		public static string GetNextSession() {
			return "";
		}

		/*private static string LoadNextSession() {
			DateTime now = DateTime.Now.Date;
			DateTime house = Util.UnixDateToDateTime(long.Parse(Util.LoadFileString(Util.CurrentSession, "congress.nextsession.house")));
			DateTime senate = Util.UnixDateToDateTime(long.Parse(Util.LoadFileString(Util.CurrentSession, "congress.nextsession.senate")));
			if (house < now && senate < now) return "";
			if (house == senate && house == now)
				return "Congress is in session today.";
			if (house == senate)
				return "Congress next meets on " + Util.DateTimeToString(house) + ".";
			string ret = "";
			if (house >= now && (house < senate || senate < now)) {
				if (house == now)
					ret += "The House meets today";
				else
					ret += "The House next meets " + Util.DateTimeToString(house);
				if (senate == now)
					ret += "; the Senate meets today";
				else if (senate > now)
					ret += "; the Senate next meets " + Util.DateTimeToString(senate);
			} else if (senate >= now) {
				if (senate == now)
					ret += "The Senate meets today";
				else
					ret += "The Senate next meets " + Util.DateTimeToString(senate);
				if (house == now)
					ret += "; the House meets today";
				else if (house > now)
					ret += "; the House next meets " + Util.DateTimeToString(house);
			}
			ret += ".";
			return ret;
		}*/
		
		public ArrayList GetPopularMonitors(string type) {
			if (PopularMonitors == null) {
				Console.Error.WriteLine("Access to Pages.Index information before initialization.");
				throw new UserException("This information is not available at the moment. Check back in a few seconds. Sorry for the inconvenience.");
			}
			ArrayList list = (ArrayList)PopularMonitors[type];
			if (list == null) list = new ArrayList();
			return list;
		}
		
		private static void LoadPopularMonitors() {
			Hashtable popmons = new Hashtable();
		
			XmlDocument populars = new XmlDocument();
			try {
				populars.Load("/home/govtrack/data/misc/monitors.popular.xml");
			} catch (Exception e) {
				return;
			}
			
			foreach (XmlElement m in populars.SelectNodes("monitors/monitor")) {
				try {
				Monitor mon = Monitor.FromString(Login.MonitorUnescape(m.GetAttribute("name")));
				if (mon == null) continue;
				//if (mon.Stale()) continue;

				Hashtable h = new Hashtable();
				h["monitor"] = mon.Encoded();
				h["title"] = mon.Display();
				h["link"] = mon.Link();
				h["users"] = m.GetAttribute("users");
				
				ArrayList list = (ArrayList)popmons[mon.GetType().Name];
				if (list == null) list = new ArrayList();
				list.Add(h);
				popmons[mon.GetType().Name] = list;
				if (popmons.Count > 100) break;
				} catch (Exception e) {
					continue;
				}
			}
			
			PopularMonitors = popmons;
		}

		public ArrayList GetPopularBills() {
			if (PopularBills == null) {
				Console.Error.WriteLine("Access to Pages.Index information before initialization.");
				throw new UserException("This information is not available at the moment. Check back in a few seconds. Sorry for the inconvenience.");
			}
			return PopularBills;
		}

		private static void LoadPopularBills() {
			ArrayList popularbills = new ArrayList();
			XmlDocument populars = new XmlDocument();
			try {
				Hashtable usedsubjs = new Hashtable();
				populars.Load("/home/govtrack/data/misc/popularbills.xml");
				foreach (XmlElement m in populars.SelectNodes("popular-bills/bill")) {
					BillMonitor mon = (BillMonitor)Monitor.FromString("bill:" + m.GetAttribute("id"));
					
					string billtitle = mon.Display().ToLower();
					string terms = "";
					foreach (XmlElement t in m.SelectNodes("search-string")) {
						if (billtitle.IndexOf(t.InnerText) >= 0) continue;
						if (terms != "") terms += ", ";
						terms += '"' + t.InnerText + '"';
					}
					
					Hashtable h = new Hashtable();
					h["monitor"] = mon.Encoded();
					h["title"] = mon.Display();
					h["link"] = mon.Link();
					h["terms"] = terms;
					popularbills.Add(h);
					if (popularbills.Count > 30) break;
				}
			} catch {
			}
			PopularBills = popularbills;
		}
		
	}
}
