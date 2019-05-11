using System;
using System.Collections;
using System.IO;
using System.Xml.XPath;
using System.Web;

using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

using XPD;

namespace GovTrack.Web.Pages.Congress {

	public class Person {

		public double GetPersonId() {
			string bgid = HttpContext.Current.Request.Params["bioguideid"];
			if (bgid != null) {
				TableRow row = Util.Database.DBSelectFirst("people", "id", new Database.SpecEQ("bioguideid", bgid));
				if (row != null) {
					HttpContext.Current.Response.Redirect("person.xpd?id=" + row["id"]);
					return 0;
				}
				throw new UserVisibleException("You have followed an invalid link to GovTrack.  The Bioguide ID specified in the URL does not correspond to a Member of Congress. (Some people have duplicate IDs; GovTrack recognizes just one.)");
			}

			string id = HttpContext.Current.Request.Params["id"];
			if (id == null)
				throw new UserVisibleException("You have followed an invalid link to GovTrack.  No person ID was specified in the address of this page.");
			try {
				return double.Parse(id);
			} catch {
				throw new UserVisibleException("You have followed an invalid link to GovTrack.  The person ID specified in the address of this page is not valid.");
			}
		}
	
		public object GetGeneralInfo(int id) {
			TableRow reptable = GovTrack.Web.Reps.GetPersonInfo(id);
			
			Hashtable rep = new Hashtable(reptable);
			
			if (rep["birthday"] != null) {
				DateTime birthday = (DateTime)rep["birthday"];
				
				// Fixup birthday field
				rep["birthday"] = birthday.ToString("MMM d, yyyy");

				// add age field
				int age = DateTime.Now.Year - birthday.Year;
				DateTime birthdaythisyear = birthday.AddYears(age);
				if (birthdaythisyear > DateTime.Now) age--;
				rep["age"] = age;
			}
			
			return rep;
		}
	
		public static object GetCurrentRole(int id) {
			TableRow rolerow = Reps.GetPersonCurrentRoleInfo(id);
			if (rolerow == null) return "none";
			Hashtable role = new Hashtable(rolerow);
			FixupRole(role);
			return role;
		}

		public static object GetRoles(int id, bool mergeStateOnly, bool wantsPredecessor) {
			ArrayList roles = new ArrayList();
			foreach (TableRow role in Util.Database.DBSelect("people_roles",
				"type, state, district, startdate, enddate, title, party, class",
				new Database.SpecEQ("personid", id),
				new Database.SpecOrder("startdate", true)) ) {

				Hashtable h = new Hashtable(role);
				FixupRole(h);
				
				// Collapse this role with a previous one
				if (roles.Count > 0) {
					Hashtable prev = (Hashtable)roles[roles.Count-1];
					if (MergeRoles(prev, h, mergeStateOnly))
						continue;
				}

				if (wantsPredecessor) {
				// Who preceeded this person in this role?
				TableRow predecessor = Util.Database.DBSelectFirst("people_roles",
					"personid",
					new Database.SpecEQ("type", role["type"]),
					new Database.SpecEQ("state", role["state"]),
					new Database.SpecEQ("district", role["district"]),
					new Database.SpecLE("enddate", role["startdate"]),
					new Database.SpecGE("enddate", ((DateTime)role["startdate"] - TimeSpan.FromDays(365*2))),
					new Database.SpecEQ("class", role["class"]),
					new Database.SpecOrder("enddate", false));
				if (predecessor != null)
					h["predecessor"] = predecessor["personid"];
				}
				
				roles.Add(h);
			}
			return roles;
		}

		private static bool MergeRoles(Hashtable prev, Hashtable role, bool mergeStateOnly) {
			if (!prev["type"].Equals(role["type"])) return false;
			if (!prev["state"].Equals(role["state"])) return false;
			if (!mergeStateOnly && prev["type"].Equals("rep") && !prev["district"].Equals(role["district"])) return false;
			string prevend = (string)prev["enddate"];
			string nowstart = (string)role["startdate"];
			if (Util.DTToDateTime(nowstart) - Util.DTToDateTime(prevend) > TimeSpan.FromDays(100)) return false;
			prev["enddate"] = role["enddate"];
			prev["mostrecentstart"] = role["startdate"];
			return true;
		}
		
		private static void FixupRole(Hashtable role) {
			if (role["startdate"] != null)
				role["startdate"] = Util.DateTimeToDT((DateTime)role["startdate"]);
			if (role["enddate"] != null)
				role["enddate"] = Util.DateTimeToDT((DateTime)role["enddate"]);
		}

		public static string ZScoreText(double z, string low, string high) {
			if (z < -6) {   return "Exceedingly " + low; }
			if (z < -3.5) { return "Extremely " + low; }
			if (z < -2) {   return "Very " + low; }
			if (z < -1) {   return low; }
			if (z < +1) {   return "Average"; }
			if (z < +2) {   return high; }
			if (z < +3.5) { return "Very " + high; }
			if (z < +6) {   return "Extremely " + high; }
			                return "Exceedingly " + high;
		}	
	
	
		public string DistrictMapLink(string state, string district) {
		    return "/congress/findyourreps.xpd?state=" + state + (district == "" ? "" : "&district=" + district);
		}
		
		public Table GetRecentVotes(int id, int count) {
			return Util.Database.DBSelect("people_votes, votes LEFT JOIN billstatus ON votes.billsession=billstatus.session and votes.billtype=billstatus.type and votes.billnumber=billstatus.number",
				"votes.id, votes.date, year(votes.date) as year, votes.description, votes.result, people_votes.vote, people_votes.displayas, billstatus.title, votes.billsession, votes.billtype, votes.billnumber",
                new Database.SpecEQ("people_votes.personid", id),
                //new Database.SpecGE("votes.date", (DateTime.Now-TimeSpan.FromDays(90)).ToString("s")),
				new Database.UserSpec("votes.id=people_votes.voteid"),
				new Database.SpecOrder("people_votes.date", false),
				new Database.SpecLimit(count)
				);
		}


	}

	public class SendLetter {
		public object GetBill() {
			if (HttpContext.Current.Request.Params["bill"] != null) {
				BillType type;
				int session, number;
				
				if (!Bill.ParseBill(HttpContext.Current.Request["bill"], out session, out type, out number))
					throw new UserException("An invalid bill ID was specified in the URL.");
					
				XPathNavigator bill = Bills.LoadBill(session, type, number);
				return bill;
			}

			return "";
		}
	
	
		public object GetRecipients() {
			ArrayList recips = new ArrayList();
			
			if (HttpContext.Current.Request.Params["bill"] != null) {
				BillType type;
				int session, number;
				
				if (!Bill.ParseBill(HttpContext.Current.Request["bill"], out session, out type, out number))
					throw new UserException("An invalid bill ID was specified in the URL.");
				
				XPathNavigator bill = Bills.LoadBill(session, type, number);
				string sponsor = (string)bill.Evaluate("string(/bill/sponsor/@id)");
				recips.Add(sponsor);
			} else if (HttpContext.Current.Request.Params["person"] != null) {
				recips.Add(HttpContext.Current.Request.Params["person"]);
			} else {
				throw new UserException("No recipient information was specified in the URL.");
			}
			
			foreach (string monitor in Login.GetMonitors())
				if (monitor.StartsWith("p:"))
					recips.Add(monitor.Substring(2));
			
			
			return recips;
		}
	}	
	
	public class FindYourReps : XPD.FormHandler {
		public bool Process(HttpContext context) {
			/*string zipcode = context.Request["zipcode"];
			if (zipcode != null && zipcode != "") {
				string[] cdists = Reps.AddressToDistrict("zipcode", zipcode);
				if (cdists.Length == 0) {
					context.Items["govtrack-zipcode_formreturn"] = "The ZIP code you entered was not found in our database.";
				} else if (cdists.Length == 1) {
					string state = cdists[0].Substring(0, 2);
					string dist = cdists[0].Substring(2);
					context.Response.Redirect("findyourreps.xpd?state=" + state + "&district=" + dist);
					return true;
				} else {
					string state = cdists[0].Substring(0, 2);
					foreach (string cdist in cdists) {
						if (!cdist.StartsWith(state)) {
							context.Items["govtrack-zipcode_formreturn"] = "Your ZIP code crosses state lines.  You'll have to choose which state you live in.";
							return false;
						}
					}
					context.Response.Redirect("findyourreps.xpd?state=" + state + "&zipstatus=multiple");
					return true;
				}
			}*/
			
			string action = context.Request["action"];
			if (action != null && action == "monitor") {
				foreach (int id in Reps.GetReps(context.Request["state"], context.Request["district"])) {
					Login.AddMonitor("p:" + id);
				}
			}
			
			string statename = context.Request["state"];
			if (statename != null && Util.IsValidStateName(statename)) {
				context.Response.Redirect("findyourreps.xpd?state=" + Util.GetStateAbbr(statename));
				return true;
			}

			return false;
		}
	}
}
