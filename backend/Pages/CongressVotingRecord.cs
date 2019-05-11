using System;
using System.Collections;
using System.Xml.XPath;
using System.Web;

using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

using XPD;

namespace GovTrack.Web.Pages.Congress {

	public class VotingRecord {
	
		public ArrayList GetPeople() {
			ArrayList people = new ArrayList();
			
			if (HttpContext.Current.Request["people"] != null) {
				string[] pp = HttpContext.Current.Request["people"].Split(',');
				foreach (string p in pp) {
					try {
						int id = int.Parse(p);
						PersonMonitor m = new PersonMonitor(id);
						people.Add(m);
					} catch {
					}
				}
			} else {
				foreach (string mon in Login.GetMonitors()) {
					Monitor m = Monitor.FromString(mon);
					if (m == null) continue;
					if (!(m is PersonMonitor)) continue;
					people.Add(m);
				}
			}
			
			for (int i = 0; i < people.Count; i++) {
				PersonMonitor m = (PersonMonitor)people[i];
				Hashtable info = new Hashtable();
				info["Id"] = m.Person;
				info["Name"] = m.Display();
				info["Link"] = m.Link();
				info["Key"] = Reps.FormatPersonName(m.Person, "now", "lastname");
				people[i] = info;
			}
			
			people.Sort(new NameSorter());
		
			return people;
		}
		
		private class NameSorter : IComparer {
			public int Compare(object a , object b) {
				string x = (string)((Hashtable)a)["Key"];
				string y = (string)((Hashtable)b)["Key"];
				return x.CompareTo(y);
			}
		}

		/*public class Votes {
			public object[] Data;
			public int HasMore, Start;
			public string Url;
		}*/
	
		public Hashtable GetVotes() {
			Hashtable votes = new Hashtable();

			IList monitors;
			bool wascustom = false;
			if (HttpContext.Current.Request["monitors"] != null) {
				monitors = Login.ParseMonitors(HttpContext.Current.Request["monitors"]);
				wascustom = true;
			} else {
				monitors = Login.GetMonitors();
			}

			foreach (string mon in monitors) {
				Monitor m = Monitor.FromString(mon);
				if (m == null) continue;
				if (m is SubjectCommitteeMonitor) {
					SubjectCommitteeMonitor scm = (SubjectCommitteeMonitor)m;
					GetSubjectVotes(Util.CurrentSession, scm.Type, scm.Term, votes);
				}
				if (m is ActiveBillsMonitor)
					GetAllBillVotes(Util.CurrentSession, votes);
				if (m is BillMonitor) {
					BillMonitor bm = (BillMonitor)m;
					GetVotesFromBill(bm, votes);
				}
			}

			if (votes.Count == 0 && !wascustom)
				GetAllBillVotes(Util.CurrentSession, votes);

			int start = 0;
			int count = 25;

			if (HttpContext.Current.Request["start"] != null)
				start = int.Parse(HttpContext.Current.Request["start"]) - 1;
			
			ArrayList ret1 = new ArrayList(votes.Values);
			ret1.Sort(new VoteSorter());

			if (start + count > ret1.Count) count = ret1.Count - start;
			if (count < 0) count = 0;
			object[] ret = new object[count];
			ret1.CopyTo(start, ret, 0, count);

			Hashtable v = new Hashtable();
			v["Data"] = ret;
			v["Start"] = start+1;
			v["HasMore"] = (ret1.Count > start+count) ? 1 : 0;

			string url = "";
			if (HttpContext.Current.Request["people"] != null)
				url += "&people=" + HttpContext.Current.Request["people"];
			if (HttpContext.Current.Request["monitors"] != null)
				url += "&monitors=" + HttpContext.Current.Request["monitors"];
			v["Url"] = url;

			return v;
		}

		private class VoteSorter : IComparer {
			public int Compare(object a , object b) {
				long x = (long)((Hashtable)a)["Date"];
				long y = (long)((Hashtable)b)["Date"];
				return -x.CompareTo(y);
			}
		}
		
		private void GetSubjectVotes(int session, string type, string term, Hashtable votes) {
			//GetVotesFromBillIndex( GovTrack.Web.Subjects.LoadIndex(session, type, term), votes );
		}
		private void GetAllBillVotes(int session, Hashtable votes) {
			GetVotesFromBillIndex(Util.LoadData(session, "bills.index.xml"), votes);
		}

		private void GetVotesFromBillIndex(XPathNavigator bills, Hashtable votes) {
			if (!bills.MoveToFirstChild()) return;
			if (!bills.MoveToFirstChild()) return;
			while (true) {
				int session = int.Parse(bills.GetAttribute("session", ""));
				string billtype = bills.GetAttribute("type", "");
				int billnumber = int.Parse(bills.GetAttribute("number", ""));
				string status = (string)bills.Evaluate("string(name(*))");
				string billtitle = bills.GetAttribute("title", "");
				string rolls = bills.GetAttribute("rolls", "");
				string statusstr = Bills.GetStatusIndexed(bills.Select("."));
				
				if (rolls != "")
				AddBillInfo(billtype, session, billnumber, rolls.Split(','),
					billtitle, statusstr, votes);

				if (!bills.MoveToNext()) break;
			}
		}

		private void GetVotesFromBill(BillMonitor bm, Hashtable votes) {
			XPathNavigator bill = Bills.LoadBill(bm.Session, bm.Type, bm.Number);
			string title = Bills.DisplayString(bill.Select("*"), 10000);
			string statusstr = Bills.GetStatusSource(bill.Select("*"));

			ArrayList rolls = new ArrayList();
			XPathNodeIterator votesiter = bill.Select("bill/actions/vote");
			while (votesiter.MoveNext()) {
				string roll = votesiter.Current.GetAttribute("roll", "");
				string date = votesiter.Current.GetAttribute("datetime", "");
				string where= votesiter.Current.GetAttribute("where", "");
				if (roll != null && roll != "" && date != null && date != "")
					rolls.Add(date + ":" + where + Util.DTToYearString(date) + "-" + roll);
			}
			
			AddBillInfo(EnumsConv.BillTypeToString(bm.Type), bm.Session, bm.Number,
				(string[])rolls.ToArray(typeof(string)), title, statusstr, votes);
		}

		private void AddBillInfo(string billtype, int session, int billnumber,
			string[] rolls, string billtitle, string statusstr,
			Hashtable votes) {

			string id = "bill:" + billtype + session + "-" + billnumber;

			if (rolls == null || rolls.Length == 0 || votes.ContainsKey(id))
				return;

			Hashtable h = new Hashtable();
			votes[id] = h;
					
			string title = Util.Trunc(billtitle, 65);
			h["Title1"] = title;
			if (title.EndsWith("...")) {
				h["Title2"] = "..." + billtitle.Substring(title.Length-3);
			}

			h["Status"] = statusstr;
			h["Rolls"] = String.Join(",", rolls);
			h["Link"] = "bill.xpd?bill=" + billtype + session + "-" + billnumber;
					
			string date = null;
			foreach (string roll in rolls) {
				string[] r = roll.Split(':');
				if (date == null || r[0].CompareTo(date) > 0) date = r[0];
			}
			h["Date"] = date;
		}
	
		public ArrayList GetVoteData(string rolls, XPathNodeIterator people) {
			Hashtable votes = new Hashtable();
			ArrayList voters = new ArrayList();
			while (people.MoveNext()) {
				string id = people.Current.Value;
				voters.Add(id);
				votes[id] = "";
			}
			
			foreach (string r in rolls.Split(',')) {
				string[] r2 = r.Split(':');
				XPathNavigator x = Bills.LoadRollParse(r2[1]);
				foreach (string id in voters) {
					string v = (string)x.Evaluate("string(roll/voter[@id=" + id  + "]/@vote)");
					if (v != "") votes[id] = v;
				}
			}
		
			ArrayList ret = new ArrayList();
			foreach (string id in voters)
				ret.Add(votes[id]);
			return ret;
		}
	
	}
}
