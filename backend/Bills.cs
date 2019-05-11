using System;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.XPath;

using XPD;
using GovTrack.Enums;

namespace GovTrack.Web {

	public class BillRef {
		public readonly int Session;
		public readonly BillType Type;
		public readonly int Number;

		public string TypeCode { get { return EnumsConv.BillTypeToString(Type); } }
		
		public BillRef(int session, BillType type, int number) {
			this.Session = session;
			this.Type = type;
			this.Number = number;
		}

		public BillRef(int session, string type, int number)
		: this(session, EnumsConv.BillTypeFromString(type), number)
		{ }
		
		public BillRef(XPathNavigator nav) {
			this.Session = int.Parse(nav.GetAttribute("session", ""));
			this.Type = EnumsConv.BillTypeFromString(nav.GetAttribute("type", ""));
			this.Number = int.Parse(nav.GetAttribute("number", ""));
		}
		
		public BillRef(XPathNodeIterator iter)
		: this(iter.Current)
		{ }
		
		public XPathNavigator Load() {
			return Bills.LoadBill(Session, Type, Number);
		}
		
		public override string ToString() {
			return EnumsConv.BillTypeToString(Type) + Session + "-" + Number;
		}
		
		public static BillRef FromID(string id) {
			BillType type;
			int session, number;
			if (!GovTrack.Web.Pages.Congress.Bill.ParseBill(id, out session, out type, out number))
				throw new ArgumentException("Invalid bill id.");
			return new BillRef(session, type, number);
		}
		
		public static BillRef FromString(string id, int session) {
			BillType type;
			int number;
			if (!Bills.ParseID(id, out type, out number))
				throw new ArgumentException("Invalid reference to bill.");
			return new BillRef(session, type, number);
		}
	}

	public class Bills {
		public static bool BillExists(int session, string type, int number) {
			string fn = Util.DataPath + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + "bills" + Path.DirectorySeparatorChar + type + number + ".xml";
			Util.SandboxDownload(fn);
			return System.IO.File.Exists(fn);
		}

		public static XPathNavigator LoadBill(int session, BillType type, int number) {
			return Util.LoadData(session, "bills" + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(type) + number + ".xml");		
		}
		
		public static XPathNavigator LoadBill2(int session, string type, int number) {
			return LoadBill(session, EnumsConv.BillTypeFromString(type), number);
		}

		public static XPathNavigator LoadBill3(string id) {
			return BillRef.FromID(id).Load();
		}
		
		public static XPathNavigator LoadBillBlogs(int session, BillType type, int number) {
			try {
				return Util.LoadData(session, "bills.blogs" + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(type) + number + ".xml");
			} catch (System.IO.FileNotFoundException ex) {
				return new XmlDocument().CreateNavigator();
			}
		}

private static string[] bill_text_codes_h = {
"ih",   "Introduced in House", "This is the original text of the bill as it was written by its sponsor and submitted to the House for consideration.",
"ihr",  "Introduced in House-Reprint", null,
"ih_s", "Introduced in House (No.) Star Print", null,
"rih",  "Referral Instructions House", null,
"rfh",  "Referred in House", "This is the text of the bill after moving from the Senate to the House before being considered by House committees.",
"rfhr", "Referred in House-Reprint", null,
"rfh_s","Referred in House (No.) Star Print", null,
"rth",  "Referred to Committee House", null,
"rah",  "Referred w/Amendments House", null,
"rch",  "Reference Change House", null,
"rh",   "Reported in House", "This is the text of the bill after coming out of committee in the House.",
"rhr",  "Reported in House-Reprint", null,
"rh_s", "Reported in House (No.) Star Print", null,
"rdh",  "Received in House", "This is the text of the bill after being approved by the Senate and moving to the House.",
"ash",  "Additional Sponsors House", null,
"sc",   "Sponsor Change House", null,
"cdh",  "Committee Discharged House", null,
"hdh",  "Held at Desk House", null,
"iph",  "Indefinitely Postponed in House", null,
"lth",  "Laid on Table in House", null,
"oph",  "Ordered to be Printed House", null,
"pch",  "Placed on Calendar House", "This is the text of the bill once it was placed on a calendar of business in the House.",
"ah", "Amendment in House", "This is proposed text for a bill offered in the form of an amendment in the House, but it may not have been accepted",
"ah2", "Amendment in House (2)", "This is proposed text for a bill offered in the form of an amendment in the House (the second such to be printed), but it may not have been accepted",
"fah",  "Failed Amendment House", null,
"ath",  "Agreed to House", null,
"cph",  "Considered and Passed House", null,
"eh",   "Engrossed in House", "This is the text of the bill as it was approved by the House, although some bills may be changed further either by the Senate or through a conference committee.",
"ehr",  "Engrossed in House-Reprint", null,
"eh_s", "Engrossed in House, (No.) Star Print [*]", null
};

private static string[] bill_text_codes_h2 = {
"eah",  "Engrossed Amendment House", "This is the text of the bill approved by the House.",
"reah", "Re-engrossed Amendment House", null
};

private static string[] bill_text_codes_s = {
"is",   "Introduced in Senate", "This is the original text of the bill as it was written by its sponsor and submitted to the Senate for consideration.",
"isr",  "Introduced in Senate-Reprint", null,
"is_s", "Introduced in Senate (No.) Star Print", null,
"ris",  "Referral Instructions Senate", null,
"rfs",  "Referred in Senate", "This is the text of the bill after moving from the House to the Senate before being considered by Senate committees.",
"rfsr", "Referred in Senate-Reprint", null,
"rfs_s","Referred in Senate (No.) Star Print", null,
"rts",  "Referred to Committee Senate", null,
"ras",  "Referred w/Amendments Senate", null,
"rcs",  "Reference Change Senate", null,
"rs",   "Reported in Senate", "This is the text of the bill after coming out of committee in the Senate.",
"rsr",  "Reported in Senate-Reprint", null,
"rs_s", "Reported in Senate, (No.) Star Print", null,
"rds",  "Received in Senate", "This is the text of the bill after being approved by the House and moving to the Senate.",
"sas",  "Additional Sponsors Senate", null,
"cds",  "Committee Discharged Senate", null,
"hds",  "Held at Desk Senate", null,
"ips",  "Indefinitely Postponed in Senate", null,
"lts",  "Laid on Table in Senate", null,
"ops",  "Ordered to be Printed Senate", null,
"pcs",  "Placed on Calendar Senate", "This is the text of the bill once it was placed on a calendar of business in the Senate.",
"as", "Amendment in Senate", "This is proposed text for a bill offered in the form of an amendment in the Senate, but it may not have been accepted",
"as2", "Amendment in Senate (2)", "This is proposed text for a bill offered in the form of an amendment in the Senate (the second such to be printed), but it may not have been accepted",
"ats",  "Agreed to Senate", null,
"cps",  "Considered and Passed Senate", null,
"fps",  "Failed Passage Senate", "This is the text of the bill as it was finally approved by the House.",
"es",   "Engrossed in Senate", "This is the text of the bill as it was approved by the Senate, although some bills may be changed further either by the House or through a conference committee.",
"esr",  "Engrossed in Senate-Reprint", null,
"es_s", "Engrossed in Senate (No.) Star Print", null
};

private static string[] bill_text_codes_s2 = {
"eas",  "Engrossed Amendment Senate", "This is the text of the bill approved by the Senate.",
"res",  "Re-engrossed Amendment Senate", null
};

private static string[] bill_text_codes_all = {
"re",   "Reprint of an Amendment", null,
"s_p",  "Star (No.) Print of an Amendment", null,
//"pp",   "Public Print", null,
"enr",  "Enrolled Bill", "This is the final text of the bill or resolution as approved by both the Senate and House.",
"renr", "Re-enrolled", null,
};

		public static string[][] GetBillTextStatusCodes(BillType type) {
			ArrayList ret = new ArrayList();
			string[][] s = new string[5][];
			if (type == BillType.H || type == BillType.HR || type == BillType.HC || type == BillType.HJ) {
				s[0] = bill_text_codes_h;
				s[1] = bill_text_codes_s;
				s[2] = bill_text_codes_s2;
				s[3] = bill_text_codes_h2;
			} else {
				s[0] = bill_text_codes_s;
				s[1] = bill_text_codes_h;
				s[2] = bill_text_codes_h2;
				s[3] = bill_text_codes_s2;
			}
			s[4] = bill_text_codes_all;
			foreach (string[] ss in s) {
				for (int i = 0; i < ss.Length; i+=3) {
					ret.Add(new string[] { ss[i] , ss[i+1], ss[i+2] });
				}
			}
			return (string[][])ret.ToArray(typeof(string[]));
		}

		public static string GetBillTextFileName(int session, BillType type, int number, string format, string version, string compareto) {
			if (format == "html") {
				if (compareto == null)
					format = "gen.html";
				else
					return Util.DataPath + Path.DirectorySeparatorChar + "bills.text.cmp" + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(type) + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(type) + number + "_" + compareto + "-" + version + ".xml";
			}
			return Util.DataPath + Path.DirectorySeparatorChar + "bills.text" + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(type) + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(type) + number + version + "." + format;
		}

		public static object LoadBillText(int session, BillType type, int number, string format, string version, string compareto) {
			string filename = GetBillTextFileName(session, type, number, format, version, compareto);

			if (format == "txt") {
				System.IO.StreamReader reader = new System.IO.StreamReader(filename);
				string ret = reader.ReadToEnd();
				ret = ret.Replace((char)0xAD, '-');
				reader.Close();
				return ret;
			} else if (format == "html") {
				using (TextReader r = new StreamReader(filename)) {
					XmlTextReader t = new XmlTextReader(r);
					t.XmlResolver = null;
					return new XPathDocument(t).CreateNavigator();
				}
			}
			throw new ArgumentException();
		}
		
		public static object LoadBillReport(int session, BillType type, int number, string reporttype) {
			if (reporttype.EndsWith("_summary")) {
				try {
					reporttype = reporttype.Substring(0, reporttype.Length-"_summary".Length);
					return Util.LoadData(session, "bills." + reporttype + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(type) + number + ".xml");
				} catch (Exception e) {
					return new XmlDocument().CreateNavigator();
				}
			}
			
			try {
				System.IO.StreamReader reader = new System.IO.StreamReader(
					Util.DataPath + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + "bills." + reporttype + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(type) + number + ".txt");
				string ret = reader.ReadToEnd();
				reader.Close();
				return ret;
			} catch (Exception e) {
				return "";
			}
		}
		
		public static bool AmendmentExists(int session, string number) {
			string fn = Util.DataPath + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + "bills.amdt" + Path.DirectorySeparatorChar + number + ".xml";
			Util.SandboxDownload(fn);
			return System.IO.File.Exists(fn);
		}

		public static XPathNavigator LoadAmendment(int session, string number) {
			try {
				return Util.LoadData(session, "bills.amdt" + Path.DirectorySeparatorChar + number + ".xml");
			} catch (System.IO.FileNotFoundException e) {
				return Util.EmptyDocument;
			}
		}

		public static object LoadAmendmentText(int session, string number) {
			try {
				string text = Util.LoadFileString(session, "bills.amdt" + Path.DirectorySeparatorChar + number + ".txt");
				return text.Split('\n');
			} catch (System.IO.FileNotFoundException e) {
				return new string[] { "" };
			}
		}

		private static string GetRollString(string where, int year, int roll) {
			where = where.Substring(0, 1);
			if (where != "h" && where != "s") throw new ArgumentException("where = " + where);
			return where + year + "-" + roll;
		}

		private static string GetRollFileName(string where, DateTime date, int roll) {
			where = where.Substring(0, 1);
			if (where != "h" && where != "s") throw new ArgumentException("where = " + where);
			if (Util.SessionFromDateTime(date) >= 101)
				return where + date.Year + "-" + roll;		
			else
				return where + roll;
		}
		
		private static string GetRollId(string where, DateTime date, int roll) {
			where = where.Substring(0, 1);
			if (where != "h" && where != "s") throw new ArgumentException("where = " + where);
			int session = Util.SessionFromDateTime(date);
			if (session >= 101)
				return where + date.Year + "-" + roll;		
			else
				return where + session + "-" + roll;
		}
		
		public static XPathNavigator LoadRoll(string where, DateTime date, int roll) {
			return Util.LoadData(Util.SessionFromDateTime(date), "rolls" + Path.DirectorySeparatorChar + GetRollFileName(where, date, roll) + ".xml");
		}

		public static string LoadRollTotals(string where, string date, int roll) {
			DateTime d = Util.DTToDateTime(date);
			string id = GetRollId(where, d, roll);

			TableRow totals;

			totals = Util.Database.DBSelectFirst("people_votes", "count(personid)", new Database.SpecEQ("voteid", id), new Database.SpecEQ("vote", "+"));
			long ayes = (long)totals[0];
			totals = Util.Database.DBSelectFirst("people_votes", "count(personid)", new Database.SpecEQ("voteid", id), new Database.SpecEQ("vote", "-"));
			long nays = (long)totals[0];
			totals = Util.Database.DBSelectFirst("people_votes", "count(personid)", new Database.SpecEQ("voteid", id), new Database.OrSpec(new Database.SpecEQ("vote", "0"), new Database.SpecEQ("vote", "P")));
			long other = (long)totals[0];

			if (ayes + nays + other == 0)
				return "";

			string ret = ayes + " Ayes, " + nays + " Nays";
			if (other > 0)
				ret += ", " + other + " Present/Not Voting";
			return ret;
		}

		public static XPathNavigator LoadRollParse(string id) {
			int session;
			string filename;
			ParseRollId(id, out session, out filename);
			return Util.LoadData(session, "rolls" + Path.DirectorySeparatorChar + filename + ".xml");
		}
		
		private static int SessionFromYear(int year) {
			return (year - 1787)/2;
		}
		
		private static void ParseRollId(string id, out int session, out string filename) {
			string[] parts = id.Split('-');
			if (parts.Length != 2) throw new ArgumentException();
			if (parts[0].Length < 2) throw new ArgumentException();
			if (parts[1].Length < 1) throw new ArgumentException();
			
			string chamber = parts[0].Substring(0, 1);
			parts[0] = parts[0].Substring(1); // session_subsession or year of congress
			int roll = int.Parse(parts[1]);
			
			if (parts[0].IndexOf('_') == -1) {
				// This is a year, which is used from 101st Congress onward.
				int year = int.Parse(parts[0]);
				session = SessionFromYear(year);
				filename = chamber + year + "-" + roll;
			} else {
				// This is a Congress number and session number, used up to the 100th Congress.
				string[] sessions = parts[0].Split('_');
				if (sessions.Length != 2) throw new ArgumentException();
				session = int.Parse(sessions[0]);
				filename = chamber + sessions[1] + "-" + roll;
			}
		}
		
		public class IntroducedDetails {
			public int Sponsor;
			public IntroducedDetails(int sponsor) { Sponsor = sponsor; }
			public override string ToString() {
				return "By " + Reps.FormatPersonName(Sponsor, "now", "");
			}
		}
		
		public class VoteDetails {
			public Chamber Where;
			public DateTime When;
			public int Roll;
			public bool Passed;
			public string Info;
			public VoteDetails(Chamber where, DateTime when, int roll, bool passed, string info) { Where = where; When = when; Roll = roll; Passed = passed; Info = info; }
			public XPathNavigator Load() {
				return LoadRoll(Where == Chamber.House ? "h" : "s", When, Roll);
			}
			public string ID() {
				return (Where == Chamber.House ? "h" : "s") + When.Year + "-" + Roll;
			}
			public string Link() {
				return VoteLink(Where == Chamber.House ? "h" : "s", When.Year, Roll);
			}
			public string Image() {
				return GetVoteImgCarto(Where == Chamber.House ? "h" : "s", When.Year, Roll, true);
			}
			public override string ToString() {
				return Info;
			}
		}
		
		private static string GetStatusText(XPathNavigator root, XPathNavigator status, out object details) {
			try {
			bool isCurrentSession = true; // default
			// this doesn't work on index nodes
			if (root.GetAttribute("session", "") != "") {
				int session = int.Parse(root.GetAttribute("session", ""));
				isCurrentSession = (session == Util.CurrentSession);
			}
			
			BillStatus s = EnumsConv.BillStatusFromString(status.Name);
			switch (s) {
				case BillStatus.Introduced:
					/*if ((string)status.Evaluate("name(parent::*)") == "status")
						details = new IntroducedDetails(int.Parse((string)root.Evaluate("string(/bill/sponsor/@id)")));
					else if ((string)status.Evaluate("name(parent::*)") == "statusxml")
						details = null;
					else
						details = new IntroducedDetails(int.Parse((string)status.Evaluate("string(@sponsor)")));*/
					details = null;
					if (isCurrentSession)
						return "Introduced";
					else
						return "Dead";
				case BillStatus.Calendar:
					details = null;
					if (isCurrentSession)
						return "Reported by Committee";
					else
						return "Dead";
				case BillStatus.Vote:
				case BillStatus.Vote2:
					if (status.GetAttribute("how", "") == "roll") {
						string info = "";
						try {
							DateTime date = Util.DTToDateTime(status.GetAttribute("date", ""));
							string file = GetRollFileName(status.GetAttribute("where", ""), date, int.Parse(status.GetAttribute("roll", ""))) + ".txt";
							info = Util.LoadFileString(Util.SessionFromDateTime(date), "gen.rolls-pca" + Path.DirectorySeparatorChar + file);
						} catch (Exception e) { }
						details = new VoteDetails(
								status.GetAttribute("where", "") == "h" ? Chamber.House : Chamber.Senate,
								Util.DTToDateTime(status.GetAttribute("datetime", "")),
								int.Parse(status.GetAttribute("roll", "")),
								status.GetAttribute("result", "") == "pass",
								info
							);
					} else {
						details = null;
					}
					string result = status.GetAttribute("result", "");
					if (result == "pass") result = "Passed";
					else if (result == "fail") result = "Failed";
					else throw new InvalidOperationException("Invalid vote result: " + result);
					Chamber chamber = EnumsUtil.BillTypeChamber(EnumsConv.BillTypeFromString(status.GetAttribute("where", "")));
					if (s == BillStatus.Vote)
						return result + " " + EnumsConv.ChamberNameShort(chamber);
					else
						return "Passed " + EnumsConv.ChamberNameShort(EnumsUtil.Other(chamber))
							+ ", " + result + " " + EnumsConv.ChamberNameShort(chamber);
				case BillStatus.Conference:
					details = "";
					return "Resolving Differences";
				case BillStatus.ToPresident:
					details = null;
					return "Sent to President";
				case BillStatus.Signed:
					details = null;
					return "Signed by President";
				case BillStatus.Veto:
					details = null;
					return "Vetoed by President";
				case BillStatus.Override:
					details = null;
					string result1 = status.GetAttribute("result", "");
					if (result1 == "pass") result1 = "Succeeded";
					else if (result1 == "fail") result1 = "Failed";
					return "Veto Override " + result1;
				case BillStatus.Enacted:
					details = null;
					return "Enacted";
			}
			throw new InvalidOperationException();
			} catch (Exception e) {
				details = null;
				return "Unknown";
			}
		}
	
		public static string GetStatusIndexed(XPathNodeIterator bill) {
			object details;
			return GetStatusIndexedDetails(bill, out details);
		}
		public static string GetStatusSource(XPathNodeIterator bill) {
			object details;
			string ret = GetStatusSourceDetails(bill, out details);
			if (details == null) return ret;
			return ret + " (" + details.ToString() + ")";
		}

		public static string GetStatusIndexedDetails(XPathNodeIterator bill, out object details) {
			if (!bill.MoveNext()) throw new ArgumentException();
			XPathNavigator root = bill.Current.Clone();
			XPathNavigator status = bill.Current.Clone();
			status.MoveToFirstChild();
			return GetStatusText(root, status, out details);
		}
		
		public static string GetStatusSourceDetails(XPathNodeIterator bill, out object details) {
			if (!bill.MoveNext()) throw new ArgumentException();
			XPathNavigator root = bill.Current.Clone();
			XPathNodeIterator status = root.Select("status/*");
			status.MoveNext();
			return GetStatusText(root, status.Current, out details);
		}

		public static XPathNavigator GetSpeeches(XPathNodeIterator bill) {
			if (!bill.MoveNext()) throw new ArgumentException();
			try {
				return Util.LoadData(
						int.Parse(bill.Current.GetAttribute("session", "")),
						"index.cr.bill" + Path.DirectorySeparatorChar
							+ EnumsConv.BillTypeToString(EnumsConv.BillTypeFromString(bill.Current.GetAttribute("type", "")))
							+ bill.Current.GetAttribute("number", "")
							+ ".xml");
			} catch (Exception e) {
			}
			return new XmlDocument().CreateNavigator();
		}

		public static string BillLink(XPathNodeIterator bill) {
			if (!bill.MoveNext()) throw new ArgumentException();
			return BillLink2(
				int.Parse(bill.Current.GetAttribute("session", "")),
				EnumsConv.BillTypeFromString(bill.Current.GetAttribute("type", "")),
				int.Parse(bill.Current.GetAttribute("number", ""))
				);
		}

		public static string BillLink2(int session, BillType type, int number) {
			return Util.UrlBase + "/congress/bill.xpd?"
				+ "bill="
				+ EnumsConv.BillTypeToString(type)
				+ session
				+ "-"
				+ number;
		}
		
		public static string BillLink3(int session, string type, int number) {
			return BillLink2(session, EnumsConv.BillTypeFromString(type), number);
		}

		public static string BillAmendmentLink(int session, BillType type, int number, string amendmentid) {
			return Util.UrlBase + "/congress/amendment.xpd?"
				+ "session=" + session
				+ "&amdt=" + amendmentid;
		}
		
		public static string CRLinkBill(XPathNodeIterator speech, XPathNodeIterator bill) {
			if (!bill.MoveNext()) throw new ArgumentException();
			return
				Record.Link(speech)
				+ "&bill="
				+ bill.Current.GetAttribute("type", "")
				+ bill.Current.GetAttribute("session", "")
				+ "-"
				+ bill.Current.GetAttribute("number", "");
		}
		
		public static string VoteLink(string where, int year, int roll) {
			return Util.UrlBase + "/congress/vote.xpd?vote="
				+ where[0] + year + "-" + roll;
		}
		public static string VoteLink2(string id) {
			return Util.UrlBase + "/congress/vote.xpd?vote=" + id;
		}

		public static string GetVoteImgGeo(string where, int year, int roll, bool thumbnail) {
			int session = SessionFromYear(year);
			return Util.UrlBase + "/data/us/" + session + "/gen.rolls-geo/" + GetRollString(where, year, roll) + (thumbnail ? "-small" : "") + ".png";
		}
		public static string GetVoteImgGeo2(string id, bool thumbnail) {
			int year = int.Parse(id.Substring(1, 4));
			int session = SessionFromYear(year);
			return Util.UrlBase + "/data/us/" + session + "/gen.rolls-geo/" + id + (thumbnail ? "-small" : "") + ".png";
		}
		public static string GetVoteImgPCA(string where, int year, int roll, bool thumbnail) {
			int session = SessionFromYear(year);
			return Util.UrlBase + "/data/us/" + session + "/gen.rolls-pca/" + GetRollString(where, year, roll) + ".png";		
		}
		public static string GetVoteImgCarto(string where, int year, int roll, bool thumbnail) {
			int session = SessionFromYear(year);
			return Util.UrlBase + "/data/us/" + session + "/gen.rolls-cart/" + GetRollString(where, year, roll) + (thumbnail ? "-small" : "") + ".png";
		}
		public static string GetVoteImgCarto2(string id, bool thumbnail) {
			int year = int.Parse(id.Substring(1, 4));
			int session = SessionFromYear(year);
			return Util.UrlBase + "/data/us/" + session + "/gen.rolls-cart/" + id + (thumbnail ? "-small" : "") + ".png";
		}
	
		public static string BillTypeToDisplayString(string type) {
			return EnumsConv.BillTypeToDisplayString(EnumsConv.BillTypeFromString(type));
		}
		
		public static string GetDisplayNumber(string type, int number) {
			return BillTypeToDisplayString(type) + " " + number;
		}

		public static string GetDisplayNumber2(string id) {
			BillRef br = BillRef.FromID(id);
			return GetDisplayNumber(br.TypeCode, br.Number) + " [" + Util.Ordinate(br.Session) + "]";
		}
		
		public static string GetHashtag(string id) {
			BillRef br = BillRef.FromID(id);
			string hashtag = "#";
			switch (br.Type) {
			case BillType.H: hashtag += "hr"; break;
			case BillType.HR: hashtag += "hres"; break;
			case BillType.HJ: hashtag += "hjres"; break;
			case BillType.HC: hashtag += "hconres"; break;
			case BillType.S: hashtag += "s"; break;
			case BillType.SR: hashtag += "sres"; break;
			case BillType.SJ: hashtag += "sjres"; break;
			case BillType.SC: hashtag += "sconres"; break;
			}
			hashtag += br.Number;
			if (br.Session != Util.CurrentSession) {
				hashtag += "/";
				hashtag += br.Session;
			}
			return hashtag;
		}

		public static string GetTinyThomasUrl(string id) {
			BillRef br = BillRef.FromID(id);
			if (br.Session != Util.CurrentSession) return "";
			string ret = "http://tinythom.as/";
			switch (br.Type) {
			case BillType.H: ret += "hr"; break;
			case BillType.HR: ret += "hres"; break;
			case BillType.HJ: ret += "hj"; break;
			case BillType.HC: ret += "hc"; break;
			case BillType.S: ret += "s"; break;
			case BillType.SR: ret += "sr"; break;
			case BillType.SJ: ret += "sj"; break;
			case BillType.SC: ret += "sc"; break;
			}
			ret += br.Number;
			ret += "/gt";
			return ret;
		}

		public static string GetPopvoxUrl(string id) {
			BillRef br = BillRef.FromID(id);
			string ret = "";
			//if (root) ret = "http://www.popvox.com/bills/us/";
			ret += br.Session + "/";
			switch (br.Type) {
			case BillType.H: ret += "hr"; break;
			case BillType.HR: ret += "hres"; break;
			case BillType.HJ: ret += "hjres"; break;
			case BillType.HC: ret += "hconres"; break;
			case BillType.S: ret += "s"; break;
			case BillType.SR: ret += "sres"; break;
			case BillType.SJ: ret += "sjres"; break;
			case BillType.SC: ret += "sconres"; break;
			}
			ret += br.Number;
			return ret;
		}
		
		public static string DisplayNumber(XPathNodeIterator bill) {
			bill = bill.Clone();
			if (!bill.MoveNext()) throw new ArgumentException();
			int session = int.Parse((string)bill.Current.Evaluate("string(@session)"));
			string type = (string)bill.Current.Evaluate("string(@type)");
			string number = (string)bill.Current.Evaluate("string(@number)");
			string ret = BillTypeToDisplayString(type) + " " + number;
			if (session != Util.CurrentSession) ret += " [" + Util.Ordinate(session) + "]";
			return ret;
		}

		public static string DisplayTitle(XPathNodeIterator bill) {
			bill = bill.Clone();
			if (!bill.MoveNext()) throw new ArgumentException();
			
			string ret;
			
			ret = DisplayTitle2(bill.Current, "short");
			if (ret != "") return ret;
			
			ret = DisplayTitle2(bill.Current, "popular");
			if (ret != "") return ret;

			ret = DisplayTitle2(bill.Current, "official");
			if (ret.EndsWith(".")) ret = ret.Substring(0, ret.Length-1);
			if (ret != "") return ret;

			ret = (string)bill.Current.Evaluate("string(titles/title[position()=last()])");
			return ret;
		}
		
		private static string DisplayTitle2(XPathNavigator bill, string type) {
			string lastas = (string)bill.Evaluate("string(titles/title[@type='" + type + "'][position()=last()]/@as)");
			return (string)bill.Evaluate("string(titles/title[@type='" + type + "'][@as='" + lastas + "'][position()=1])");
		}
		
		public static string DisplayString(XPathNodeIterator bill, int truncate) {
			return Util.Trunc(DisplayNumber(bill) + ": " + DisplayTitle(bill), truncate);
		}
		
		private class ContribData {
			public string name;
			public double total, sorttotal;
			public Hashtable byrep = new Hashtable();
		}
		
		public XPathNavigator GetBillMoney(XPathNodeIterator bill) {
			if (!bill.MoveNext()) throw new ArgumentException();
		
			string sponsor = (string)bill.Current.Evaluate("string(sponsor/@id)");
			int ncosponsors = (int)(double)bill.Current.Evaluate("count(cosponsors/cosponsor)");
			if (ncosponsors > 4) ncosponsors = 4;
			
			Hashtable contribs = new Hashtable();
		
			XPathNodeIterator sponsors = bill.Current.Select("sponsor/@id|cosponsors/cosponsor/@id");
			while (sponsors.MoveNext()) {
				string rep = sponsors.Current.Value;
				if (rep == "") continue;
				
				string repfile = "reps.contributors" + Path.DirectorySeparatorChar + rep + ".xml";
				try {
					XPathNavigator contrib = Util.LoadData(Util.CurrentSession, repfile);
					XPathNodeIterator c = contrib.Select("contributions/contribution");
					while (c.MoveNext()) {
						string n = c.Current.GetAttribute("who", "");
						double a = double.Parse(c.Current.GetAttribute("amount", ""));
						
						ContribData d = (ContribData)contribs[n];
						if (d == null) {
							d = new ContribData();
							d.name = n;
							contribs[n] = d;
						}
						
						d.total += a;					
						d.byrep[rep] = a;
						
						if (rep != sponsor) a /= (double)ncosponsors * 1.5;
						d.sorttotal += a;
					}
				} catch (Exception e) {
					// Silently skip reps whose contributions are not on file
				}
			}
			
			XmlDocument ret = new XmlDocument();
			ret.AppendChild( ret.CreateElement("contributions") );
			foreach (ContribData d in contribs.Values) {
				XmlElement c = ret.CreateElement("contrib");
				ret.DocumentElement.AppendChild(c);
				c.SetAttribute("name", d.name);
				c.SetAttribute("total", d.total.ToString());
				c.SetAttribute("sorttotal", d.sorttotal.ToString());
				foreach (string key in d.byrep.Keys) {
					XmlElement r = ret.CreateElement("rep");
					c.AppendChild(r);
					r.SetAttribute("id", key);
					r.SetAttribute("amount", d.byrep[key].ToString());
				}
			}
			
			return ret.CreateNavigator();
		}
		
		private static object[,] TypeStringMap = {
			// order is important
			{ "h", BillType.H },
			{ "hr", BillType.H },
			{ "hres", BillType.HR },
			{ "hjres", BillType.HJ },
			{ "hconres", BillType.HC },
			{ "s", BillType.S },
			{ "sres", BillType.SR },
			{ "sjres", BillType.SJ },
			{ "sconres", BillType.SC }
			};
		
		public static bool ParseID(string id, out BillType type, out int number) {
			type = BillType.H;
			number = 0;
			if (id.Length > 15) return false;
			
			// Normalize string.  No spaces, dashes, or periods, all lowercase.
			string oldId = id;
			id = "";
			foreach (char c in oldId.ToLower())
				if (c != ' ' && c != '.' && c != '-')
					id += c;
			
			string rest = null;
			
			for (int i = 0; i < TypeStringMap.GetLength(0); i++) {
				if (!id.StartsWith((string)TypeStringMap[i,0])) continue;
				type = (BillType)TypeStringMap[i,1];
				rest = id.Substring(((string)TypeStringMap[i,0]).Length);
			}
			
			if (rest == null) return false;
			
			try {
				number = int.Parse(rest);
			} catch (Exception e) {
				return false;
			}
			
			return true;
		}


		public static string GetTransformedVoteDescription(string desc, string billtitle) {
			string newdesc = desc, explanation = null;
			TransformVoteDescription(desc, billtitle, ref newdesc, ref explanation);
			return newdesc;
		}

		public static string GetVoteExplanation(string desc) {
			string newdesc = desc, explanation = "";
			TransformVoteDescription(desc, null, ref newdesc, ref explanation);
			return explanation;
		}

		public static void TransformVoteDescription(string desc, string billtitle, ref string newdesc, ref string explanation) {
			string type, target;

			// Split on the first colon or " ("
			int colon = desc.IndexOf(':');
			int paren = desc.IndexOf(" (");
			if (colon != -1 && (colon < paren || paren == -1)) {
				type = desc.Substring(0, colon);
				target = desc.Substring(colon+1).Trim();
			} else if (paren != -1 && (paren < colon || colon == -1)) {
				type = desc.Substring(0, paren);
				target = desc.Substring(paren+2, desc.Length-paren-4).Trim();
			} else {
				return;
			}

			switch (type) {
			case "On Motion to Instruct Conferees":
				explanation = "After the Senate and House both pass their own versions of a bill, a conference committee must work out the differences. This motion is used to instruct the conference committee to take certain actions.";
				break;
			case "On Motion to Recommit with Instructions":
				explanation = "This motion sends a bill back to committee with instructions on how to proceed.";
				break;
			case "On the Cloture Motion":
				target = TransformVoteDescriptionTargetBillTitle(billtitle, target);
				newdesc = target;
				explanation = "The Senate uses cloture to end debate on a matter.";
				break;
			case "On Passage of the Bill":
			case "On Passage":
			case "On Agreeing to the Resolution":
			case "On the Resolution":
			case "On the Concurrent Resolution":
			case "On the Joint Resolution":
			case "On the Conference Report":
			case "On Agreeing to the Conference Report":
				if (billtitle != null && billtitle != "")
					target = billtitle;
				newdesc = target + " (" + type + ")";
				break;
			case "Passage, Objections of the President Notwithstanding":
			case "Passage, Objections of the President Not Withstanding":
				if (billtitle != null && billtitle != "")
					target = billtitle;
				newdesc = target + " (Veto Override)";
				break;
			case "On the Amendment": // i.e. target == "Biden Amdt. No. 739"
				if (billtitle != null && billtitle != "")
					target = target + ", " + Util.Trunc(billtitle, 125);
				newdesc = target + " (" + type + ")";
				break;
			case "On Agreeing to the Amendment": // i.e. target == "Amendment 7 to H R 1538"
				target = TransformVoteDescriptionTargetBillTitle(billtitle, target);
				newdesc = target + " (" + type + ")";
				break;
			case "On the Motion":
				newdesc = target + " (" + type + ")";
				break;
			case "On the Nomination":
				if (target.StartsWith("Confirmation "))
					newdesc = "Confirming " + target.Substring("Confirmation ".Length);
				break;
			}

			if (type.StartsWith("On Motion to Suspend the Rules and Agree")
				|| type.StartsWith("On Motion to Suspend the Rules and Pass")) {
				target = TransformVoteDescriptionTargetBillTitle(billtitle, target);
				newdesc = target + " (under suspension of the rules)";
				explanation = "The motion to suspend the rules is used frequently to pass legislative measures that are perceived to have a broad degree of support and little need for prolonged debate.";
			}

			if (desc == "Call of the House: QUORUM") {
				newdesc = "Quorum Call";
				explanation = "The quorum call requires a majority of senators or representatives to be present and may be used to delay floor proceedings.";
			}

		}
		private static string TransformVoteDescriptionTargetBillTitle(string billtitle, string target) {
			if (billtitle != null && billtitle != "") {
				int c = billtitle.IndexOf(':');
				if (c != -1) {
					string b = billtitle.Substring(0,c).Replace(" ", "").Replace(".", "").ToUpper();
					string e = target.Replace(" ", "").Replace(".", "");
					if (e.StartsWith(b)) {
						return Util.Trunc(billtitle, 125);
					}
				}
			}
			return target;
		}

		public object LoadLinks(int session, string type, int number) {
			return Util.Database.DBSelect("billlinks", "source, url, excerpt",
				new Database.SpecEQ("session", session),
				new Database.SpecEQ("type", type),
				new Database.SpecEQ("number", number));
		}
		public object LoadLinks2(int session, string type, int number) {
			return Util.Database.DBSelect("billlinks2", "url, title",
				new Database.SpecEQ("session", session),
				new Database.SpecEQ("type", type),
				new Database.SpecEQ("number", number));
		}

	}

}
