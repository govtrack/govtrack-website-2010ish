using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Web;
using System.Xml;
using System.Xml.XPath;

using GovTrack;
using GovTrack.Enums;
using XPD;

namespace GovTrack.Web {

	public class Reps {
		// Data for current congresspeople are cached... oy?
		static Table PeopleCacheTable;
		static Hashtable PeopleCacheRows = new Hashtable();
		static Hashtable RolesCache = new Hashtable();
		
		static Dictionary<string,Hashtable> committees;
		static Dictionary<string,List<Hashtable>> committeelists;
		static Hashtable committeeMapSubcommitteeToParent;
		static Hashtable committeeMapThomasToId;
		static Hashtable committeeMapIdToThomas;

		static Hashtable approvalRatings = new Hashtable();
		static Hashtable spectrumInfo = new Hashtable();
		
		static object all_reps;
		static Hashtable reps_by_session = new Hashtable();
		static Hashtable reps_by_year = new Hashtable();

		static string dbfields = "id, lastname, firstname, middlename, nickname, namemod, lastnameenc, bioguideid, pvsid, metavidid, osid, birthday, gender, twitterid";
		
		public static void Init(bool allowDelayedLoad) {
			// Run this later on so we don't halt the start-up too much.
			// We check below if any of the globals are null and handle
			// accordingly.
			AppModule.Run(allowDelayedLoad, delegate(object state) {
				// Initialize the cache of people info
				PeopleCacheTable = Util.Database.DBSelect("people", dbfields,
					new Database.UserSpec("(select count(*) from people_roles where id=personid and startdate > \"1980-00-00\") <> 0"));
				int row = 0;
				foreach (TableRow person in PeopleCacheTable)
					PeopleCacheRows[(int)(uint)person["id"]] = row++;

				all_reps = MakeRepSearchArray(GetCurrentReps(), -1);
			});
			
			committees = new Dictionary<string,Hashtable>();
			committeelists = new Dictionary<string,List<Hashtable>>();
			committeelists["senate"] = new List<Hashtable>();
			committeelists["house"] = new List<Hashtable>();
			committeelists["joint"] = new List<Hashtable>();
			committeeMapSubcommitteeToParent = new Hashtable();
			committeeMapThomasToId = new Hashtable();
			committeeMapIdToThomas = new Hashtable();
			string fn = Util.DataPath + Path.DirectorySeparatorChar + "committees.xml";
			Util.SandboxDownload(fn);
			XmlDocument committeesdoc = new XmlDocument();
			committeesdoc.Load(fn);
			foreach (XmlElement committee in committeesdoc.SelectNodes("committees/committee[count(@obsolete)=0]")) {
				Hashtable c = new Hashtable();
				c["id"] = committee.GetAttribute("code");
				c["displayname"] = committee.GetAttribute("displayname");
				c["url"] = committee.GetAttribute("url");
				c["parentid"] = "";
				committees[(string)c["id"]] = c;
				committeelists[committee.GetAttribute("type")].Add(c);
				
				foreach (XmlElement thomasname in committee.SelectNodes("thomas-names/name")) {
					committeeMapThomasToId[thomasname.GetAttribute("session") + "::" + thomasname.InnerText] = committee.GetAttribute("code");
					committeeMapIdToThomas[thomasname.GetAttribute("session") + "::" + committee.GetAttribute("code")] = thomasname.InnerText;
				}
				
				c["subcommittees"] = new ArrayList();
				foreach (XmlElement subc in committee.SelectNodes("subcommittee[count(@obsolete)=0]")) {
					Hashtable s = new Hashtable();
					s["id"] = committee.GetAttribute("code") + subc.GetAttribute("code");
					s["displayname"] = subc.GetAttribute("displayname");
					s["parentid"] = c["id"];
					s["subcommittees"] = new ArrayList();
					((ArrayList)c["subcommittees"]).Add(s);
					
					committees[(string)s["id"]] = s;
				}
			}

			LoadApprovalRatings();
			LoadSpectrumInfo();
		}
	
		public static string RoleTypeTitle(string type, string title) {
			if (type == "sen") return "Sen.";
			if (type == "rep" && title == "REP") return "Rep.";
			if (type == "rep" && title == "DEL") return "Del.";
			if (type == "rep" && title == "RC") return "Res.Com.";
			if (type == "prez") return "President";
			throw new ArgumentException("Invalid role type: " + type + "/" + title);
		}
		
		public static string RoleTypeName(string type, string title) {
			if (type == "sen") return "U.S. Senator";
			if (type == "rep" && title == "REP") return "U.S. Representative";
			if (type == "rep" && title == "DEL") return "U.S. Delegate";
			if (type == "rep" && title == "RC") return "Resident Commissioner";
			if (type == "prez") return "U.S. President";
			throw new ArgumentException("Invalid role type: " + type + "/" + title);
		}
	
		public static Database.Spec RoleNow(int id) {
			return RoleThen(id, DateTime.Now);
			//return RoleThen(id, Util.EndOfSession(Util.CurrentSession), Util.StartOfSession(Util.CurrentSession));
		}
		
		public static Database.Spec RoleThen(int id, DateTime when) {
			return RoleThen(id, when, when);
		}

		public static Database.Spec RoleThen(int id, DateTime when1, DateTime when2) {
			Database.AndSpec datefilter =
				new Database.AndSpec(
					new Database.SpecLE("startdate", when1),
					new Database.SpecGE("enddate", when2),
					new Database.OrSpec(
						new Database.SpecEQ("type", "prez"),
						new Database.SpecEQ("type", "sen"),
						new Database.SpecEQ("type", "rep"))
					);
			if (id == -1) return datefilter;
			return new Database.AndSpec(
				new Database.SpecEQ("personid", id),
				datefilter);
		}
		
		private static string First(string pipechoices) {
			if (pipechoices == null) return "";
			string[] choices = pipechoices.Split('|');
			return choices[0];
		}
		
		public static TableRow GetPersonInfo(int id) {
			if (PeopleCacheTable != null && PeopleCacheRows != null && PeopleCacheRows.ContainsKey(id)) {
				TableRow rep = (TableRow)PeopleCacheTable[(int)PeopleCacheRows[id]];
				if (rep == null) throw new ArgumentException("Invalid person id: " + id);
				return rep;
			} else {
				TableRow rep = Util.Database.DBSelectFirst("people", dbfields,
					new Database.SpecEQ("id", id));
				if (rep == null) throw new ArgumentException("Invalid person id: " + id);
				return rep;
			}
		}
		public static TableRow GetPersonCurrentRoleInfo(int id) {
			lock (RolesCache) {
				TableRow role = (TableRow)RolesCache[id];
				if (role == null) {
					Database.Spec whenspec = RoleNow(id);
					role = Util.Database.DBSelectFirst("people_roles", "type, state, district, party, title, url", whenspec, new Database.SpecOrder("startdate", false));
					RolesCache[id] = role;
				}
				return role;
			}
		}
		public static bool HasCurrentRole(int id) {
			return GetPersonCurrentRoleInfo(id) != null;
		}
		public static TableRow GetPersonRoleInfoAt(int id, int session) {
			Database.Spec whenspec = RoleThen(id, Util.EndOfSession(session), Util.StartOfSession(session));
			return Util.Database.DBSelectFirst("people_roles", "type, state, district, party, title, url", whenspec, new Database.SpecOrder("startdate", false));
		}

		public static double WhoWasPresidentAt(string date) {
			Database.Spec whenspec = RoleThen(-1, Util.DTToDateTime(date));
			return (int)Util.Database.DBSelectFirst("people_roles", "personid", whenspec, new Database.SpecEQ("type", "prez"))["personid"];
		}

		private static string AsString(object o) {
			if (o is string) return (string)o;
			if (o is byte[]) return System.Text.Encoding.UTF8.GetString((byte[])o);
			throw new ArgumentException(o.ToString());
		}

		public static string FormatPersonName(int id, string when, string style) {
			if (id == 0) return "Unknown Person";
			TableRow rep = GetPersonInfo(id);
			if (rep == null) return "(Unknown)";

			string ln = First(AsString(rep["lastnameenc"]));
			if (style == "lastname") return ln;
			
			string fn = First((string)rep["firstname"]);
			string mn = First((string)rep["middlename"]);
			string nn = First((string)rep["nickname"]);
			string nm = First((string)rep["namemod"]);
			
			if (fn.EndsWith(".")) {
				if (style == "nickmod")
					fn += " " + mn;
				else
					fn = mn;
			}
			if (style == "nickmod" && nn != "") fn += " (" + nn + ")";
			
			string name;
			
			if (style == "short" || style == "lastnamestate")
				name = ln;
			else if (style == "lastfirst" || style == "lastfirstnostate")
				name = ln + ", " + fn;
			else
				name = fn + " " + ln;
				
			if (style == "nickmod" && nm != "") name += ", " + nm;
			
			if (when == "") return name;
			
			TableRow role = null;

			if (HttpContext.Current != null)
				role = (TableRow)HttpContext.Current.Items["govtrack-reps-role-cache-" + when + "-" + id];

			if (role == null) {
				if (when == "now" || when == Util.CurrentSession.ToString() || (when.Length > 4 && (Util.DTToDateTime(when) > Util.StartOfSession(Util.CurrentSession) && Util.DTToDateTime(when) < Util.EndOfSession(Util.CurrentSession)))) {
					role = GetPersonCurrentRoleInfo(id);
				} else {
					Database.Spec whenspec;
					if (when.Length <= 3)
						whenspec = RoleThen(id, Util.EndOfSession(int.Parse(when)), Util.StartOfSession(int.Parse(when)));
					else
						whenspec = RoleThen(id, Util.DTToDateTime(when));
					role = Util.Database.DBSelectFirst("people_roles", "type, state, district, party, title, url", whenspec);
				}
			}
			
			if (role == null) return name;
			if ((string)role["type"] != "sen" && (string)role["type"] != "rep") return name;
			
			if (style != "notitle" && style != "notitlestate")
				name = RoleTypeTitle((string)role["type"], (string)role["title"]) + " " + name;
			
			if (style == "short")
				return name;
			
			char party;
			if (role["party"] != null)
				party = ((string)role["party"])[0];
			else
				party = '?';
				
			if (style == "lastfirstnostate" || style == "nostate" || style == "notitlestate")
				name += " [" + party + "]";
			else if ((string)role["type"] == "sen" || (short)role["district"] <= 0)
				name += " [" + party + "-" + role["state"] + "]";
			else
				name += " [" + party + "-" + role["state"] + role["district"] + "]";
			
			return name;
		}

		public static ArrayList FormatPeopleNames(XPathNodeIterator ids, string when, string style) {
			ArrayList ret = new ArrayList();

			//CacheRolesAt(ids.Clone(), when);
			while (ids.MoveNext()) {
				Hashtable h = new Hashtable();
				h["id"] = ids.Current.Value;
				h["name"] = FormatPersonName(int.Parse(ids.Current.Value), when, style);
				ret.Add(h);
			}

			return ret;
		}
	
		public static string CacheRolesAt(XPathNodeIterator ids, string when) {
			ArrayList idlist = new ArrayList();
			while (ids.MoveNext())
				idlist.Add(ids.Current.Value);
			CacheRolesAt2(idlist, when);
			return "";
		}
	
		public static void CacheRolesAt2(ArrayList idlist, string when) {
			if (HttpContext.Current == null) return;
			if (idlist.Count == 0) return;
			Database.Spec whenspec;
			if (when == "now")
				whenspec = RoleNow(-1);
			else if (when.Length <= 3)
				whenspec = RoleThen(-1, Util.EndOfSession(int.Parse(when)), Util.StartOfSession(int.Parse(when)));
			else
				whenspec = RoleThen(-1, Util.DTToDateTime(when));
			Table roles = Util.Database.DBSelect("people_roles", "personid, type, state, district, party, title", whenspec, new Database.SpecIn("personid", idlist));

			foreach (TableRow role in roles)
				HttpContext.Current.Items["govtrack-reps-role-cache-" + when + "-" + role["personid"]] = role;
		}

		public static string RepLink(int id) {
			return Util.UrlBase + "/congress/person.xpd?"
				+ "id="
				+ id;
		}

		public static string CRLinkRep(XPathNodeIterator speech, int rep) {
			return
				Record.Link(speech)
				+ "&person=" + rep;
		}

		public static XPathNavigator GetSpeeches(int id) {
			try {
				return Util.LoadData(Util.CurrentSession,
					"index.cr.person" + Path.DirectorySeparatorChar
						+ id
						+ ".xml");
			} catch (System.IO.FileNotFoundException e) {
				return Util.EmptyDocument;
			}
		}

		public static ICollection GetBillsSponsored(int id, int session) {
			return GetBills(id, "sponsor", session);
		}
		public static ICollection GetBillsCosponsored(int id, int session) {
			return GetBills(id, "cosponsor", session);
		}
		private static ICollection GetBills(int id, string relation, int session) {
			Table table = Util.Database.DBSelect("billindex left join billstatus on billindex.session=billstatus.session and billindex.type=billstatus.type and billindex.number=billstatus.number",
                "billstatus.*",
                new Database.SpecEQ("idx", relation), new Database.SpecEQ("value", id.ToString()),
				new Database.SpecEQ("billindex.session", session));
			return table;
		}

		public XPathNavigator GetPersonalStats(int id, bool currentOnly) {
			for (int session = Util.CurrentSession; session >= 1; session--) {
				try {
					return Util.LoadData(session, "repstats.person" + Path.DirectorySeparatorChar + id + ".xml");
				} catch (System.IO.FileNotFoundException e) {
				} catch (System.IO.DirectoryNotFoundException e) {
				}
				if (currentOnly) break;
			}
			return Util.EmptyDocument;
		}
		public XPathNavigator GetStats(string type) {
			return Util.LoadData(Util.CurrentSession,
					"repstats" + Path.DirectorySeparatorChar + type + ".xml");
		}
		
		public static ICollection GetCurrentReps() {
			return Util.Database.DBSelectVector(
				"people_roles", "personid",
					new Database.SpecLE("startdate", DateTime.Now),
					new Database.SpecGE("enddate", DateTime.Now),
					new Database.OrSpec(
						new Database.SpecEQ("type", "rep"),
						new Database.SpecEQ("type", "sen")
						)
				);
		}
		
		private static object MakeRepSearchError(string err) {
			Hashtable ret = new Hashtable();
			ret["error"] = err;
			return ret;
		}
		
		private static object MakeRepSearchArray(ICollection reps, int session) {
			ArrayList ret = new ArrayList();
			foreach (int rep in reps) {
				Hashtable h = new Hashtable();
				h["id"] = rep.ToString();
				h["name"] = FormatPersonName(rep, session > 0 ? session.ToString() : "now", session > 0 ? "lastfirstnostate" : "lastfirst");
				h["sortname"] = FormatPersonName(rep, "", "lastfirst");
				
				if (session == -2) {
					ArrayList roles = (ArrayList)GovTrack.Web.Pages.Congress.Person.GetRoles(rep, true, false);
					if (roles.Count > 0) {
						Hashtable role = (Hashtable)roles[roles.Count-1];
						h["startdate"] = role["startdate"];
						h["enddate"] = role["enddate"];
						h["state"] = role["state"];
						h["district"] = role["district"];
						h["type"] = role["type"];
						h["title"] = role["title"];
					} else {
						continue;
					}
				} else {
					TableRow role = session == -1 ? GetPersonCurrentRoleInfo(rep) : GetPersonRoleInfoAt(rep, session);
					if (role != null) {
						h["type"] = role["type"];
						h["title"] = role["title"];
						if ((string)role["type"] == "rep" || (string)role["type"]=="sen") {
							h["state"] = role["state"];
							h["district"] = role["district"];
						}
					} else {
						continue;
					}
				}

				if (h["type"] != null && (string)h["type"] != "prez")
					h["statename"] = Util.GetStateName((string)h["state"]);
				
				ret.Add(h);
			}
			
			if (session == -2)
				ret.Sort(new FindRepByNameSorter());
			else
				ret.Sort(new RepListSorter());
			
			return ret;
		}
		
		private class RepListSorter : IComparer {
			public int Compare(object a, object b) {
				Hashtable ha = a as Hashtable;
				Hashtable hb = b as Hashtable;
				int c;

				if (ha["state"] != null && hb["state"] != null) {
					c = ((string)ha["state"]).CompareTo(hb["state"]);
					if (c != 0) return c;

					c = ((string)ha["type"]).CompareTo(hb["type"]);
					if (c != 0) return -c;
				
					int d1 = ha["district"] == null ? (short)-1 : (short)ha["district"];
					int d2 = hb["district"] == null ? (short)-1 : (short)hb["district"];
					c = d1.CompareTo(d2);
					if (c != 0) return c; 
				}	

				return ((string)ha["name"]).CompareTo(hb["name"]);
			}
		}
		
		public static object FindAll() {
			if (all_reps == null) {
				Console.Error.WriteLine("Access to all_reps before initialization.");
				throw new UserException("This information is not available. Check back in a few seconds. Sorry for the inconvenienc.");
			}
			return all_reps;
		}

		public static object FindBySession(int session) {
			lock (reps_by_session) {
				if (reps_by_session[session] != null)
					return reps_by_session[session];

				ICollection reps = Util.Database.DBSelectVector(
					"people_roles", "personid",
						new Database.SpecLE("startdate", Util.EndOfSession(session)),
						new Database.SpecGE("enddate", Util.StartOfSession(session)),
						new Database.OrSpec(
							new Database.SpecEQ("type", "rep"),
							new Database.SpecEQ("type", "sen")
							)
					);
				object a = MakeRepSearchArray(reps, session);
				reps_by_session[session] = a;
				return a;
			}
		}

		public static object FindByYear(int year) {
			lock (reps_by_year) {
				if (reps_by_year[year] != null)
					return reps_by_session[year];

				ICollection reps = Util.Database.DBSelectVector(
					"people_roles", "personid",
						new Database.SpecLE("startdate", year + "-12-31"),
						new Database.SpecGE("enddate", year + "01-01"),
						new Database.OrSpec(
							new Database.SpecEQ("type", "rep"),
							new Database.SpecEQ("type", "sen")
							)
					);
				object a = MakeRepSearchArray(reps, -2);
				reps_by_year[year] = a;
				return a;
			}
		}

		public static object FindByName(string name, bool current) {
			name = name.ToLower();
			ArrayList reps = new ArrayList();
			if (current) {
				ICollection all = GetCurrentReps();
				foreach (object rep in all) {
					TableRow info = GetPersonInfo((int)rep);
					string n = (string)info["lastname"];
					n = n.ToLower();
					if (n.IndexOf(name) >= 0) reps.Add(rep);
				}
				if (reps.Count > 0)
					return MakeRepSearchArray(reps, -1);
			}

			// do historical search
			Table ids = Util.Database.DBSelect("people", "id", new Database.SpecEQ("lastname", name));
			foreach (TableRow row in ids) {
				int id;
				if (row["id"] is int) id = (int)row["id"];
				else if (row["id"] is uint) id = (int)(uint)row["id"];
				else throw new Exception(row["id"].GetType().FullName);
				reps.Add(id);
			}
			return MakeRepSearchArray(reps, -2);
		}

		public static object FindByState(string state) {
			ICollection all = GetCurrentReps();
			ArrayList reps = new ArrayList();
			string s2 = state.ToUpper();
			try {
				s2 = Util.GetStateAbbr(state);
			} catch (Exception e) {
			}
			foreach (object rep in all) {
				TableRow role = GetPersonCurrentRoleInfo((int)rep);
				if (role == null) continue;
				string s = (string)role["state"];
				if (s == state || s == s2) reps.Add(rep);
			}
			return MakeRepSearchArray(reps, -1);
		}
		
		private static string InnerText(XmlNode n) {
			if (n == null) return null;
			return n.InnerText;
		}

		public static ArrayList AddressToDistricts(string type, string address) {
			address = address.Replace("%", "%25");
			address = address.Replace("&", "%26");
			XmlDocument response = new XmlDocument();
			try {
				response.Load("http://www.govtrack.us/perl/district-lookup.cgi?" + type + "=" + address);
			} catch (Exception e) {
				return new ArrayList();
			}
				
			ArrayList dists = new ArrayList();
			foreach (XmlNode n in response.SelectNodes("congressional-district | congressional-districts/congressional-district")) {
				string state = InnerText(n.SelectSingleNode("state"));
				string dist = InnerText(n.SelectSingleNode("district"));
				if (state == null || dist == null || state == "" || dist == "") continue;
		
				Hashtable sd = new Hashtable();
				sd["state"] = state;
				sd["district"] = dist;
				dists.Add(sd);
			}
			return dists;
		}

		public static object FindByAddress(string type, string address) {
			IList dists = AddressToDistricts(type, address);
			if (dists.Count == 0)
				throw new UserException("The congressional district for that address could not be determined.");
			if (dists.Count > 1)
				throw new UserException("The ZIP code contains more than one congressional district. Try a ZIP+4.");
			return FindByDistricts(dists);
		}
		
		private static object FindByDistricts(IList districts) {
			ArrayList reps = new ArrayList();		
			Hashtable didsenators = new Hashtable();
			
			foreach (IDictionary district in districts) {
				if (!didsenators.ContainsKey(district["state"])) {
					reps.AddRange( Util.Database.DBSelectVector(
						"people_roles", "personid",
							new Database.SpecLE("startdate", DateTime.Now),
							new Database.SpecGE("enddate", DateTime.Now),
							new Database.SpecEQ("state", district["state"]),
							new Database.SpecEQ("type", "sen")
						) );
					didsenators[district["state"]] = districts;				
				}

				reps.AddRange( Util.Database.DBSelectVector(
					"people_roles", "personid",
						new Database.SpecLE("startdate", DateTime.Now),
						new Database.SpecGE("enddate", DateTime.Now),
						new Database.SpecEQ("state", district["state"]),
						new Database.SpecEQ("district", district["district"]),
						new Database.SpecEQ("type", "rep")
					) );
			}

			return MakeRepSearchArray(reps, -1);
		}
		
		public static ArrayList GetReps(string state, string districtstr) {
			if (state == null) return new ArrayList();

			int district;
			if (districtstr == null || districtstr == "")
				district = 0;
			else
				district = int.Parse(districtstr);
			if (district == 98) district = 0;

			return GetReps3(new DistrictMonitor(state, district));
		}

		public static ArrayList GetReps2(string statedistrictstr) {
			return GetReps3(new DistrictMonitor(statedistrictstr));
		}
		
		public static ArrayList GetReps3(DistrictMonitor district) {
			ArrayList ret = new ArrayList();
			
			ret.AddRange( Util.Database.DBSelectVector(
				"people_roles", "personid",
					new Database.SpecLE("startdate", DateTime.Now),
					new Database.SpecGE("enddate", DateTime.Now),
					new Database.SpecEQ("state", district.State),
					new Database.SpecEQ("type", "sen")
						) );

			ret.AddRange( Util.Database.DBSelectVector(
				"people_roles", "personid",
					new Database.SpecLE("startdate", DateTime.Now),
					new Database.SpecGE("enddate", DateTime.Now),
					new Database.SpecEQ("state", district.State),
					new Database.SpecEQ("district", district.District),
					new Database.SpecEQ("type", "rep")
				) );
			
			return ret;
		}
		
		public bool IsMonitoringAll(string state, string district) {
			foreach (int id in GetReps(state, district)) {
				if (!Login.HasMonitor("p:" + id))
					return false;
			}
			
			return true;
		}
		
		public static bool HasImage(string id) {
			return System.IO.File.Exists(Util.DataPath2 + Path.DirectorySeparatorChar + "photos" + Path.DirectorySeparatorChar + id + ".jpeg");
		}
		public static Hashtable GetImageCredit(string id) {
			Hashtable ret = new Hashtable();
			string credit = Util.LoadFileString(0, Path.DirectorySeparatorChar + "photos" + Path.DirectorySeparatorChar + id + "-credit.txt").Trim();
			int space = credit.IndexOf(' ');
			if (space == -1) {
				ret["link"] = "http://www.govtrack.us"; // shouldn't happen
				ret["text"] = credit;
			} else {
				ret["link"] = credit.Substring(0, space);
				ret["text"] = credit.Substring(space+1);
			}
			return ret;
		}


		public static object GetCommitteeList(string type) {
			return committeelists[type.ToLower()];
		}
		public static object GetSubcommitteeList(string committeeid) {
			return committees[committeeid]["subcommittees"];
		}
		public static string GetCommitteeSortString(string displayname) {
			return displayname.Replace("the ", "");
		}
		public static string GetCommitteeParent(string subcommitteeid) {
			try {
				return (string)committees[subcommitteeid]["parentid"];
			} catch (Exception) {
				return ""; // is a full committee, not a subcommittee
			}
		}
		public static string GetCommitteeName(string committeeid) {
			return (string)committees[committeeid]["displayname"];
		}
		public static ArrayList GetCommitteeMembers(string committeeid) {
			ArrayList ret = new ArrayList();
			foreach (TableRow row in Util.Database.DBSelect("people_committees", "personid, role", new Database.SpecEQ("committeeid", committeeid))) {
				ret.Add(row);
			}
			return ret;
		}
		public static ArrayList GetPersonCommittees(int id) {
			ArrayList ret = new ArrayList();
			foreach (TableRow row in Util.Database.DBSelect("people_committees LEFT JOIN committees ON committeeid=id ", "id, parent, displayname, role", new Database.SpecEQ("personid", id))) {
				ret.Add(row);
			}
			return ret;
		}
		public static string GetCommitteeCurrentThomasName(string committeeid) {
			string name = (string)committeeMapIdToThomas[Util.CurrentSession + "::" + committeeid];
			if (name == null) return "";
			return name;
		}
		public static string GetCommitteeArchiveThomasName(int session, string committeeid) {
			string name = (string)committeeMapIdToThomas[session + "::" + committeeid];
			if (name == null) return "";
			return name;
		}
		public static string GetCommitteeId(int session, string comm, string subcomm) {
			if (!comm.StartsWith("Senate ") && !comm.StartsWith("House ")) return "";
			if (comm == "House Administration") comm = "House House Administration";
			string id = (string)committeeMapThomasToId[session + "::" + comm + (subcomm != "" ? "::" + subcomm : "")];
			if (id == null && subcomm != "") // fall back
				id = (string)committeeMapThomasToId[session + "::" + comm];
			if (id == null)
				id = "";
			return id;
		}

		private static void LoadApprovalRatings() {
			if (approvalRatings.Count > 0) return;
			if (!System.IO.File.Exists("/home/govtrack/extdata/misc/surveyusa.xml")) return;
			XmlDocument doc = new XmlDocument();
			doc.Load("/home/govtrack/extdata/misc/surveyusa.xml");

			foreach (XmlElement n in doc.SelectNodes("approval-ratings/person")) {
				Hashtable r = new Hashtable();
				approvalRatings[n.GetAttribute("id")] = r;
				r["approval"] = n.GetAttribute("approval");
				r["link-survey"] = n.GetAttribute("link-survey");
				//r["link-tracking"] = n.GetAttribute("link-tracking");
				r["date"] = n.GetAttribute("date");
				r["mean"] = (int)float.Parse(doc.DocumentElement.GetAttribute("mean-approval"));
			}
		}
		public static Hashtable GetApprovalRating(string id) {
			Hashtable ret = (Hashtable)approvalRatings[id];
			if (ret == null) ret = new Hashtable();
			return ret;
		}

		public static bool HasSpectrumImage(string id) {
			return System.IO.File.Exists(Util.DataPath + Path.DirectorySeparatorChar + Util.CurrentSession + Path.DirectorySeparatorChar + "stats" + Path.DirectorySeparatorChar + "person" + Path.DirectorySeparatorChar + "sponsorshipanalysis" + Path.DirectorySeparatorChar + id + ".png");
		}
		private static void LoadSpectrumInfo() {
			foreach (string hs in new string[] { "h", "s" }) {
				string fn = Util.DataPath + Path.DirectorySeparatorChar + Util.CurrentSession + Path.DirectorySeparatorChar + "stats" + Path.DirectorySeparatorChar + "sponsorshipanalysis_" + hs + ".txt";
				Console.Error.WriteLine(fn);
				Util.SandboxDownload(fn);
				if (!System.IO.File.Exists(fn)) return;
				using (TextReader reader = new StreamReader(fn)) {
					string s;
					while ((s = reader.ReadLine()) != null) {
						string[] fields = s.Split(',');
						Hashtable r = new Hashtable();
						spectrumInfo[fields[0].Trim()] = r;
						r["description"] = fields[5].Trim();
					}
				}
			}
		}
		public static Hashtable GetSpectrumInfo(string id) {
			Hashtable ret = (Hashtable)spectrumInfo[id];
			if (ret == null) ret = new Hashtable();
			return ret;
		}

		/*
		public static ArrayList FindRepByName(string text) {
			if (text.Trim().Length == 0) return new ArrayList();

			long now = Util.DateTimeToDate(DateTime.Now);

			ArrayList ret = new ArrayList();
			foreach (uint id in Util.Database.DBSelectVector("people", "id",
				new Database.OrSpec(
					new Database.SpecStartsWith("lastname", text),
					new Database.SpecStartsWith("lastnameenc", text)
				))) {

				Hashtable entry = new Hashtable();
				entry["id"] = id;
				entry["name"] = FormatPersonName((int)id, 0, "lastfirst");

				ArrayList roles = (ArrayList)GovTrack.Web.Pages.Congress.Person.GetRoles((int)id, true, false);
				if (roles.Count == 0) continue; // ??

				Hashtable role = (Hashtable)roles[roles.Count-1];
				entry["startdate"] = role["startdate"];
				entry["enddate"] = role["enddate"];
				entry["state"] = role["state"];
				entry["district"] = role["district"];
				entry["type"] = role["type"];
				entry["title"] = role["title"];

				ret.Add(entry);
			}

			ret.Sort(new FindRepByNameSorter());

			return ret;
		}
		*/

		class FindRepByNameSorter : IComparer {
			public int Compare(object a, object b) {
				string end1 = (string)((Hashtable)a)["enddate"];
				string end2 = (string)((Hashtable)b)["enddate"];
				int x = end1.CompareTo(end2);
				if (x != 0) return -x;

				string start1 = (string)((Hashtable)a)["startdate"];
				string start2 = (string)((Hashtable)b)["startdate"];
				x = start1.CompareTo(start2);
				if (x != 0) return -x;

				string name1 = (string)((Hashtable)a)["name"];
				string name2 = (string)((Hashtable)b)["name"];
				return name1.CompareTo(name2);
			}
		}

		public static string GetDistrictLocalities(int session, string state, int district) {
			return "";
			TableRow row = Util.Database.DBSelectFirst("cdgeometry", "description",
                new Database.SpecEQ("session", session),
                new Database.SpecEQ("state", state),
				new Database.SpecEQ("district", district));
			return (string)row["description"];
		}

		public static Table GetRecentVideos(int id, int count, string since, string upto) {
			return Util.Database.DBSelect("people_videos",
				"personid, source, date, title, thumbnail, link",
                id > 0 ? (Database.Spec)new Database.SpecEQ("personid", id) : (Database.Spec)new Database.SpecGT("personid", 0),
                new Database.SpecGE("date", since == "" ? "0000-00-00" : Util.DTToDBString(since)),
                new Database.SpecLT("date", upto == "" ? "2100-01-01" : Util.DTToDBString(upto)),
                new Database.SpecOrder("date", false),
                new Database.SpecLimit(count <= 0 ? 9999 : count)
                );
		}
	}
}
