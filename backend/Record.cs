using System;
using System.Collections;
using System.IO;
using System.Web;
using System.Xml;
using System.Xml.XPath;

using GovTrack.Enums;

namespace GovTrack.Web {

	public class Record {
		static Hashtable RecordDaysCache = new Hashtable();
		
		public static string SessionOfRecord(string id) {
			return id.Substring(0, 3);
		}
		public static string ChamberOfRecord(string id) {
			return id.Substring(4, 1);
		}
		public static string DateOfRecord(string id) {
			return id.Substring(5, 8);
		}
		public static string OrdinalOfRecord(string id) {
			return id.Substring(14);
		}
				
		public static XPathNavigator LoadRecord(string id) {
			if (id.Length < 15) throw new ArgumentException();
			int session = int.Parse(id.Substring(0, 3));
			string file = id.Substring(4);
			return Util.LoadData(session, Path.DirectorySeparatorChar + "cr" + Path.DirectorySeparatorChar + file + ".xml");
		}
	
		public static string Link(XPathNodeIterator speech) {
			if (!speech.MoveNext()) throw new ArgumentException();
			return Util.UrlBase + "/congress/record.xpd?id="
				+ speech.Current.GetAttribute("file", "").Replace("/", "-");
		}
	
		public string ChamberNameShort(string chamber) {
			if (chamber == "h") return EnumsConv.ChamberNameShort(Chamber.House);
			if (chamber == "s") return EnumsConv.ChamberNameShort(Chamber.Senate);
			return "";
		}
		public string ChamberNameLong(string chamber) {
			if (chamber == "h") return EnumsConv.ChamberNameLong(Chamber.House);
			if (chamber == "s") return EnumsConv.ChamberNameLong(Chamber.Senate);
			return "";
		}
		
		public object GetRecordDays(int year) {
			ArrayList ret;
			lock (RecordDaysCache) {
				ret = (ArrayList)RecordDaysCache[year];
				if (ret == null) {
					ret = GetRecordDays2(year);
					RecordDaysCache[year] = ret;
				}
				return ret;
			}
		}

		ArrayList GetRecordDays2(int year) {
			string[] files = new string[0]; // Directory.GetFiles(Util.DataPath + Path.DirectorySeparatorChar + Util.SessionFromYear(year) + Path.DirectorySeparatorChar + "cr");
			Hashtable dates = new Hashtable();
			foreach (string file in files) {
				string d = Path.GetFileName(file).Substring(1,8);
				DateTime dt = new DateTime(int.Parse(d.Substring(0,4)), int.Parse(d.Substring(4,2)), int.Parse(d.Substring(6,2)));
				if (dt.Year != year) continue;
				Hashtable date;
				if (dates[d] == null) {
					date = new Hashtable();
					dates[d] = date;
				} else {
					date = (Hashtable)dates[d];
				}
				date["datestr"] = Util.DateTimeToString(dt);
				date["date"] = d;
				date[Path.GetFileName(file).Substring(0,1)] = 1; // mark 's' or 'h'
			}
			
			return SortValuesOnKeys(dates);
		}
		
		private ArrayList SortValuesOnKeys(Hashtable h) {
			ArrayList s = new ArrayList(h.Keys);
			s.Sort();
			ArrayList r = new ArrayList();
			foreach (object o in s)
				r.Add(h[o]);
			return r;
		}
		
		public object GetRecordDayDocuments() {
			string date = HttpContext.Current.Request["date"];
			string where = HttpContext.Current.Request["where"];
			
			if (date == null || date == "" || where == null
				|| (where != "s" && where != "h"))
				return new ArrayList();
			int.Parse(date); // make sure it's a number
			
			int session = int.Parse(Util.SessionFromDT(date));
			
			Hashtable ret = new Hashtable();
			string[] files = Directory.GetFiles(Util.DataPath + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + "cr" + Path.DirectorySeparatorChar, where + date + "-*.xml");
			foreach (string file in files) {
				Hashtable h = new Hashtable();
				string f = Path.GetFileName(file);
				f = f.Substring(10, f.Length-4-10);

				h["session"] = session;
				h["where"] = where;
				h["date"] = date;
				h["ord"] = f;
				
				XmlDocument d = new XmlDocument();
				d.Load(file);
				h["title"] = d.DocumentElement.GetAttribute("title");
			
				ret[int.Parse(f)] = h;
			}
			
			return SortValuesOnKeys(ret);
		}
	}
	
}
	
