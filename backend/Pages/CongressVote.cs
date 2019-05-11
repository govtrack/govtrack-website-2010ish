using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.XPath;
using System.Web;

using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

using XPD;

namespace GovTrack.Web.Pages.Congress {

	public class Vote {
		static ArrayList voteYearsAvailable;
		
		public static void Init(bool allowDelayedLoad) {
			/*TableRow firstVote = Util.Database.DBSelectFirst("votes", "votes.date",
                new Database.SpecOrder("votes.date", true)
                );
            int firstYear = DateTime.Now.Year;
			if (firstVote["date"] is DateTime)
				firstYear = ((DateTime)firstVote["date"]).Year;*/
			int firstYear = 1789;
			voteYearsAvailable = new ArrayList();
			for (int i = DateTime.Now.Year; i >= firstYear; i--)
				voteYearsAvailable.Add(i);
		}
	
		public string ChamberNameShort(string chamber) {
			if (chamber[0] == 'h') return EnumsConv.ChamberNameShort(Chamber.House);
			if (chamber[0] == 's') return EnumsConv.ChamberNameShort(Chamber.Senate);
			return "";
		}

		public string CachePersonData(XPathNodeIterator ids, string when) {
			List<int> idlist = new List<int>();
			while (ids.MoveNext())
				idlist.Add(int.Parse(ids.Current.Value));
				
			if (idlist.Count == 0) return "";

			Table people = Util.Database.DBSelect("people", "id, firstname, lastname", new Database.SpecIn("id", idlist));
			Hashtable names = new Hashtable();
			foreach (TableRow person in people) {
				int id = (int)(uint)person["id"];
				string fn = (string)person["firstname"];
				if (fn.IndexOf("|") != -1)
					fn = fn.Substring(0, fn.IndexOf("|"));
				string ln = (string)person["lastname"];
				names[id] = ln + ", " + fn;
			}
			HttpContext.Current.Items["govtrack-vote-repname"] = names;

			Table peopleroles = Util.Database.DBSelect("people_roles", "personid, party", new Database.SpecIn("personid", idlist), Reps.RoleThen(-1, Util.DTToDateTime(when)));
			Hashtable parties = new Hashtable();
			foreach (TableRow person in peopleroles) {
				int id = (int)person["personid"];

				string party = (string)person["party"];
				if (party == null || party.Length == 0) party = "?";
				names[id] = (string)names[id] + " [" + party[0] + "]";

				if (party == "?") party = "Unknown";
				parties[id] = party;
			}
			HttpContext.Current.Items["govtrack-vote-repparty"] = parties;
			return "";
		}
		
		public string PersonInfo(int id, string infokey) {
			if (id == 0) return "";
			Hashtable info = (Hashtable)HttpContext.Current.Items["govtrack-" + infokey];
			string ret = (string)info[id];
			if (ret == null) ret = "";
			return ret;
		}	

		public static bool IsVoteTypeOnPassage(string type) {
			int colon = type.IndexOf(':');
			if (colon != -1) type = type.Substring(0, colon);
			int paren = type.IndexOf('(');
			if (paren > 0) type = type.Substring(0, paren-1);

			type = type.ToLower();

			if (type == "on passage") return true;
			if (type == "on passage, as amended") return true;
			if (type == "on passage of the bill") return true;
			if (type == "on passage of the bill, as amended") return true;
			if (type == "on agreeing to the resolution") return true;
			if (type == "on agreeing to the resolution, as amended") return true;
			if (type == "on the resolution") return true;
			if (type == "on the concurrent resolution") return true;
			if (type == "on the joint resolution") return true;
			if (type == "on motion to suspend the rules and pass") return true;
			if (type == "on motion to suspend the rules and pass, as amended") return true;
			if (type == "on motion to suspend the rules and agree") return true;
			if (type == "on motion to suspend the rules and agree, as amended") return true;
			if (type == "on the conference report") return true;
			if (type == "on agreeing to the conference report") return true;
			return false;
		}

		public object GetVotes() {
			int start = 1;
			int count = -1;
			
			if (HttpContext.Current.Request["start"] != null)
				start = int.Parse(HttpContext.Current.Request["start"]);
			if (HttpContext.Current.Request["count"] != null)
				count = int.Parse(HttpContext.Current.Request["count"]);
		
			int year = -1;
			
			if (HttpContext.Current.Request["year"] != null)
				year = int.Parse(HttpContext.Current.Request["year"]);
				
			return GetVotes2(start, count, year);
		}

		public object GetVotes2(int start, int count, int year) {
			Database.Spec dateSpec = new Database.UserSpec("1");
			if (count == -1) {
				if (year == -1)
					year = DateTime.Now.Year;
				string startDate = year + "-01-01";
				string endDate = year + "-12-31";
				dateSpec = new Database.AndSpec(
					new Database.SpecGE("votes.date", startDate),
					new Database.SpecLE("votes.date", endDate));
			}

			string subjectJoin = "";
			Database.Spec subjectSpec = new Database.UserSpec("1");
			if (HttpContext.Current.Request["subject"] != null) {
				subjectJoin = " LEFT JOIN billindex ON votes.billsession=billindex.session and votes.billtype=billindex.type and votes.billnumber=billindex.number";
				subjectSpec = new Database.AndSpec(
					new Database.SpecEQ("billindex.idx", "crs"),
					new Database.SpecEQ("billindex.value", HttpContext.Current.Request["subject"]));
			}

			string personvoteJoin = "", personvoteCols = "";
			ArrayList people = new ArrayList();
			string people_join_type = "LEFT";
			if (HttpContext.Current.Request["person"] != null) {
				people.Add(int.Parse(HttpContext.Current.Request["person"]));
				if (HttpContext.Current.Request["person2"] != null)
					people.Add(int.Parse(HttpContext.Current.Request["person2"]));
				people_join_type = "INNER";
			} else {
				foreach (string p in Login.GetMonitors()) {
					Monitor m = Monitor.FromString(p);
					if (m is PersonMonitor) {
						people.Add(((PersonMonitor)m).Person);
					}
				}
			}

			if (people.Count > 0) {
				personvoteJoin = " " + people_join_type + " JOIN people_votes ON votes.id = people_votes.voteid"
					+ " AND " + new Database.SpecIn("people_votes.personid", people).ToString();
				personvoteCols = ", people_votes.personid, people_votes.vote, people_votes.displayas";
			}

			Database.Spec billSpec = new Database.UserSpec("1");
			if (HttpContext.Current.Request["bill"] != null) {
				BillRef br = BillRef.FromID(HttpContext.Current.Request["bill"]);
				billSpec = new Database.AndSpec(
					new Database.SpecEQ("votes.billsession", br.Session),
					new Database.SpecEQ("votes.billtype", br.TypeCode),
					new Database.SpecEQ("votes.billnumber", br.Number));
				dateSpec = new Database.UserSpec("1"); // must not filter
			}

			Table table = Util.Database.DBSelect("votes"
				+ subjectJoin + personvoteJoin,
                "votes.id, votes.date, votes.description, votes.result"
                	//+ ", billstatus.title"
                	+ ", votes.billsession, votes.billtype, votes.billnumber"
				+ personvoteCols,
				dateSpec,
                new Database.SpecOrder("votes.date", false),
                (subjectJoin == "" && personvoteJoin == "") ? (Database.Spec)new Database.SpecOrder("votes.seq", false) : (Database.Spec)new Database.UserSpec("1"),
				new Database.UserSpec(HttpContext.Current.Request["chamber"] == null ? "1" : "votes.id LIKE '" + HttpContext.Current.Request["chamber"][0] + "%'"),
				subjectSpec, billSpec,
				new Database.SpecLimit(count, start-1)
                );
                
            if (personvoteJoin == "")
            	return table;
            	
        	// Collapse table rows.
        	ArrayList ret = new ArrayList();
        	Hashtable row_hash = new Hashtable();
        	foreach (TableRow row in table) {
        		string vid = (string)row["id"];
        		Hashtable v = (Hashtable)row_hash[vid];
        		if (v == null) {
        			v = new Hashtable(row);
        			row_hash[vid] = v;
        			ret.Add(v);
        			
        			v["people_votes"] = new Hashtable();
        		}
        		
        		if (row["personid"] != null) // did the person vote in this vote
	        		((Hashtable)v["people_votes"])[row["personid"]] = new string[] { (string)row["vote"], (string)row["displayas"] };
        	}

            bool diffsonly = HttpContext.Current.Request["differences"] != null && HttpContext.Current.Request["differences"] == "1";
            bool samesonly = HttpContext.Current.Request["differences"] != null && HttpContext.Current.Request["differences"] == "2";
        	ArrayList ret2 = new ArrayList();
        	
        	foreach (Hashtable row in ret) {
        		ArrayList votes = new ArrayList();
        		string[] v = null;
        		bool diff = false;
        		foreach (int pid in people) {
        			string[] vv = (string[])((Hashtable)row["people_votes"])[pid];
   	    			if (v == null) v = vv;
       				else if (vv != null && !v[0].Equals(vv[0])) diff = true;
        			votes.Add(vv);
        		}
        		
        		if (people.Count > 1 && diffsonly && !diff) continue;
        		if (people.Count > 1 && samesonly && diff) continue;
        		
        		row.Remove("people_votes");
        		row["votes"] = votes;
        		ret2.Add(row);
        	}
        	
        	return ret2;
		}

		public ArrayList GetVoteYearsAvailable() {
			return voteYearsAvailable;
		}
	}
}
