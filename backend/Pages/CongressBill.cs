using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Web;

using XPD;
using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

namespace GovTrack.Web.Pages.Congress {

	public class Bill {
		public static bool ParseBill(string bill, out int session, out BillType type, out int number) {
			session = 0;
			type = 0;
			number = 0;
		
			if (bill.StartsWith("hr"))
				type = BillType.HR;
			else if (bill.StartsWith("hj"))
				type = BillType.HJ;
			else if (bill.StartsWith("hc"))
				type = BillType.HC;
			else if (bill.StartsWith("h"))
				type = BillType.H;
			else if (bill.StartsWith("sr"))
				type = BillType.SR;
			else if (bill.StartsWith("sj"))
				type = BillType.SJ;
			else if (bill.StartsWith("sc"))
				type = BillType.SC;
			else if (bill.StartsWith("s"))
				type = BillType.S;
			else
				return false;
			
			bill = bill.Substring(type == BillType.S || type == BillType.H ? 1 : 2);
			string[] sn = bill.Split('-');
			if (sn.Length != 2) return false;
				
			try {
				session = int.Parse(sn[0]);
				number = int.Parse(sn[1]);
			} catch (Exception e) {
				return false;
			}

			return true;
		}

		public static bool BillExists() {
			// ought to be the first thing executed in the bill page

			string bill = HttpContext.Current.Request["bill"];
			BillType type;
			int session, number;
			
			if (bill == null) throw new UserException("I'm sorry, you have followed an invalid link to GovTrack.  A bill was not properly specified in the URL.  Please ask the source of the link to correct the problem.");
			if (!ParseBill(bill, out session, out type, out number))
				throw new UserException("An invalid bill ID was specified in the URL.");

			return Bills.BillExists(session, EnumsConv.BillTypeToString(type), number);
		}

		public static Hashtable GetBillNumber() {
			string bill = HttpContext.Current.Request["bill"];
			BillType type;
			int session, number;
			
			if (bill == null) throw new UserException("I'm sorry, you have followed an invalid link to GovTrack.  A bill was not properly specified in the URL.  Please ask the source of the link to correct the problem.");
			ParseBill(bill, out session, out type, out number);

			Hashtable ret = new Hashtable();
			ret["session"] = session;
			ret["type"] = EnumsConv.BillTypeToString(type);
			ret["number"] = number;
			return ret;
		}
	
		public static XPathNavigator LoadBill() {
			if (!BillExists())
				return new System.Xml.XmlDocument().CreateNavigator();

			string bill = HttpContext.Current.Request["bill"];
			BillType type;
			int session, number;
			
			ParseBill(bill, out session, out type, out number);
			
			Util.Database.DBExecute(
				"INSERT INTO billhits VALUES (" + session + ",'" + EnumsConv.BillTypeToString(type) + "'," + number + ",0,0) ON DUPLICATE KEY UPDATE hits1=hits1+1");
			
			return Bills.LoadBill(session, type, number);
		}

		public static void ParseBill2(out int session, out BillType type, out int number) {
			string bill = HttpContext.Current.Request["bill"];
			if (bill == null) throw new UserException("You've come to an invalid page on GovTrack. A bill was not specified in the URL.");
			if (!ParseBill(bill, out session, out type, out number))
				throw new UserException("An invalid bill ID was specified in the URL.");
		}

		public static bool BillTextExists() {
			return LoadBillAvailabeTextVersions().Count > 0;
		}

		public static object LoadBillText() {
			BillType type;
			int session, number;
			ParseBill2(out session, out type, out number);

			string format = "html";

			string version = HttpContext.Current.Request["version"];
			string compareto = HttpContext.Current.Request["compareto"];
			
			if (version == null) {
				string[][] statuses = Bills.GetBillTextStatusCodes(type);
				foreach (string[] s in statuses) {
					if (System.IO.File.Exists(Bills.GetBillTextFileName(session, type, number, format, s[0], null)))
						version = s[0];
				}
				if (version == null) {
					if (format == "html") {
						format = "txt";
						foreach (string[] s in statuses) {
							if (System.IO.File.Exists(Bills.GetBillTextFileName(session, type, number, format, s[0], null)))
								version = s[0];
						}
						if (version == null)
							return TextNotAvailable();
					}
				}
				
				compareto = null;
			} else {
				foreach (char c in version)
					if (!char.IsLetter(c) && !char.IsDigit(c))
						throw new UserException("Invalid bill version code.");

				if (compareto != null)
					foreach (char c in compareto)
						if (!char.IsLetter(c) && !char.IsDigit(c))
							throw new UserException("Invalid bill version code (compareto).");
			}

			try {
				object ret = Bills.LoadBillText(session, type, number, format, version, compareto);
				if (ret is string) {
					Hashtable x = new Hashtable();
					x["text"] = ret;
					ret = x;
				}
				return ret;
			} catch (System.IO.IOException e) {
				Console.Error.WriteLine(e);
				return TextNotAvailable();
			}
		}
		
		private static object TextNotAvailable() {
			System.Xml.XmlDocument d = new System.Xml.XmlDocument();
			d.LoadXml("<div><p>The text of this bill is not available on GovTrack. If the bill was recently introduced, it may not be available yet from the Government Printing Office. If the bill is in GovTrack's historical data, the text of the bill may not be available online. You may have also followed an invalid link.</p></div>");
			return d.CreateNavigator();
		}

		public static ArrayList LoadBillAvailabeTextVersions() {
			BillType type;
			int session, number;
			ParseBill2(out session, out type, out number);

			ArrayList ret = new ArrayList();

			string[][] statuses = Bills.GetBillTextStatusCodes(type);
			foreach (string[] s in statuses) {
				bool hashtml = System.IO.File.Exists(Bills.GetBillTextFileName(session, type, number, "html", s[0], null));
				bool hastxt = System.IO.File.Exists(Bills.GetBillTextFileName(session, type, number, "txt", s[0], null));
				if (!hashtml && !hastxt) continue;

				Hashtable h = new Hashtable();
				h["code"] = s[0];
				h["name"] = s[1];
				h["description"] = s[2];
				h["format"] = (hashtml ? "html" : "text");
				h["size"] = (hashtml ? new System.IO.FileInfo(Bills.GetBillTextFileName(session, type, number, "html", s[0], null)).Length : 0);
				ret.Add(h);

				try {
					XmlDocument mods = new XmlDocument();
					XmlNamespaceManager ns = new XmlNamespaceManager(mods.NameTable);
					ns.AddNamespace("mods", "http://www.loc.gov/mods/v3");
					mods.Load(Bills.GetBillTextFileName(session, type, number, "mods.xml", s[0], null));
					h["sortdate"] = mods.SelectSingleNode("mods:mods/mods:originInfo/mods:dateIssued", ns).InnerText;
					h["date"] = Util.DTToDateString((string)h["sortdate"]);
				} catch (Exception e) {
					// remove this try block when we fill in the missing files
					h["date"] = "No Date";
				}
			}

			ret.Reverse();
			return ret;
		}

		public static object LoadBillReport(string reporttype) {
			BillType type;
			int session, number;
			ParseBill2(out session, out type, out number);

			if (reporttype != "cbo" && reporttype != "cbo_summary" && reporttype != "ombsap" && reporttype != "ombsap_summary")
				throw new UserException("An invalid report type was specified in the URL.");
			try {
				return Bills.LoadBillReport(session, type, number, reporttype);
			} catch (Exception e) {
				return "";
			}
		}

		public static XPathNavigator LoadBill(int session, string type, int number) {
			return Bills.LoadBill(session, EnumsConv.BillTypeFromString(type), number);
		}
		
		public static XPathNodeIterator BillSummary(int session, string type, int number) {
			return Util.LoadData(session, "bills.summary" + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(EnumsConv.BillTypeFromString(type)) + number + ".summary.xml").Select("/");		
		}
		
		public static string GetThomasLink(string session, string type, int number) {
			if (type == "hr") type = "h.res.";
			return "http://thomas.loc.gov/cgi-bin/bdquery/z?d" + session + ":" + type + number + ":";
		}
		public string GetThomasBillTextLink(string session, string type, int number) {
			if (type == "hr") type = "h.res.";
			return "http://thomas.loc.gov/cgi-bin/query/z?c" + session + ":" + type + number + ":";
		}

		public Table GetBillVotes() {
			BillType type;
			int session, number;
			ParseBill2(out session, out type, out number);

			return Util.Database.DBSelect("votes",
				"votes.id, votes.date, votes.description, votes.result, DATE_FORMAT(votes.date, '%Y-%m-%dT%T') as datestr",
				new Database.SpecEQ("votes.billsession", session),
				new Database.SpecEQ("votes.billtype", EnumsConv.BillTypeToString(type)),
				new Database.SpecEQ("votes.billnumber", number),
				new Database.SpecOrder("votes.date", false));
		}
		

		public ArrayList LoadMonitoredVotes(string where, string date, string roll) {
			ArrayList ids = new ArrayList();
			foreach (string m in Login.GetMonitors()) {
				Monitor mm = Monitor.FromString(m);
				if (mm != null && mm is PersonMonitor)
					ids.Add(((PersonMonitor)mm).Person);
			}

			Reps.CacheRolesAt2(ids, date);

			Table votes = Util.Database.DBSelect("people_votes", "personid, vote",
				new Database.SpecEQ("voteid", where + Util.DTToYearString(date) + "-" + roll),
				new Database.SpecIn("personid", ids));
			ArrayList ret = new ArrayList();
			foreach (TableRow row in votes) {
				Hashtable h = new Hashtable();
				ret.Add(h);
				h["id"] = row["personid"];
				h["vote"] = row["vote"];
				h["name"] = Reps.FormatPersonName((int)row["personid"], date, "");
			}
			return ret;
		}
		
		private string lopOffYear(int session, string title) {
			for (int y = session * 2 + 1787; y <= session * 2 + 1787 + 1; y++) {
				string suf = " of " + y;
				if (title.EndsWith(suf)) {
					title = title.Substring(0, title.Length-suf.Length);
					break;
				}
			}
			return title;
		}

		public ArrayList GetSameTitledBills() {
			BillType type;
			int session, number;
			ParseBill2(out session, out type, out number);

			// Get all of the many titles of this bill and form
			// a list of SQL starts-with tests.
			List<string> titles = new List<string>();
			List<Database.Spec> titlespecs = new List<Database.Spec>();
			foreach (string title_1 in Util.Database.DBSelectVector(
				"billtitles", "title",
				new Database.SpecEQ("session", session),
				new Database.SpecEQ("type", EnumsConv.BillTypeToString(type)),
				new Database.SpecEQ("number", number))) {

				// Lop off any "of YEAR" from the end of the title
				string title = lopOffYear(session, title_1);
				titles.Add(title);
				titlespecs.Add(new Database.SpecStartsWith("title", title));
			}

			// Return fast if there are no titles...
			if (titlespecs.Count == 0)
				titlespecs.Add(new Database.SpecEQ("title", "blah"));
				
			// Get all bills 
			Table bills = Util.Database.DBSelect("billtitles",
				"session, type, number",
				new Database.SpecNot(new Database.AndSpec(
					new Database.SpecEQ("session", session),
					new Database.SpecEQ("type", EnumsConv.BillTypeToString(type)),
					new Database.SpecEQ("number", number))),
				Database.OrSpec.New(titlespecs.ToArray()),
				new Database.SpecDistinct());
				
			// For each bill, find its main title, and return that if it
			// is not one of the original bill's titles.
			ArrayList ret = new ArrayList();
			foreach (TableRow row in bills) {
				Hashtable b = new Hashtable(row);
				ret.Add(b);
				
				TableRow titlerow = Util.Database.DBSelectFirst(
					"billtitles", "title",
					new Database.SpecEQ("session", row["session"]),
					new Database.SpecEQ("type", row["type"]),
					new Database.SpecEQ("number", row["number"]),
					new Database.SpecEQ("titletype", "short"));
				if (titlerow == null) continue;
				
				string title = lopOffYear((int)row["session"], (string)titlerow["title"]);
				if (!titles.Contains(title)) // not hashed
					b["title"] = title;
			}
			return ret;
		}

		public ArrayList GetRelatedMonitors() {
			string bill = HttpContext.Current.Request["bill"];
			ArrayList ret = new ArrayList();
			foreach (TableRow row in Util.Database.DBSelect("monitormatrix",
				"monitor2, count, tfidf",
				new Database.SpecEQ("monitor1", "bill:" + bill),
				new Database.SpecLimit(15),
				new Database.SpecOrder("tfidf", false))) {
				
				try {
					Monitor m = Monitor.FromString((string)row["monitor2"]);
				
					Hashtable b = new Hashtable(row);
					ret.Add(b);
					b["name"] = m.Display();
					b["link"] = m.Link();
				} catch {
				}
			}
			return ret;
		}
		
		public static string SubmitUrl() {
			if (HttpContext.Current.Request["article_url"] == null
				|| !HttpContext.Current.Request["article_url"].StartsWith("http://"))
				return "Paste a web page address that begins with 'http://'.";

			if (HttpContext.Current.Request["article_url"].StartsWith("http://www.govtrack.us")
				|| HttpContext.Current.Request["article_url"].StartsWith("http://www.opencongress.org"))
				return "You cannot submit a GovTrack or OpenCongress web page. Only news articles from established publications are accepted.";
		
			Hashtable item = new Hashtable();
			item["bill"] = HttpContext.Current.Request["bill"];
			item["url"] = HttpContext.Current.Request["article_url"];
			Util.Database.DBInsert("linksubmission", item);
			return "Thank you for your submission! It is awaiting moderation.";
		}
		
	}
}
