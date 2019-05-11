using System;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.XPath;

using XPD;
using GovTrack.Enums;

namespace GovTrack.Web {

	public class Subjects {
		static object subject_lock = new object();

		static ArrayList AllSubjects;
		static ArrayList TopTerms;
		static Hashtable SubTerms;

		static Hashtable related_sites_by_subject;
		static Hashtable related_sites_by_bill;

		public static void Init(bool allowDelayedLoad) {
			AppModule.Run(allowDelayedLoad, delegate(object state) {
            	LoadTopTerms();
				LoadRelatedPages();
			});
		}
	
		public static string CRSLink(string subject) {
			return Util.UrlBase + "/congress/subjects.xpd?type=crs&term=" + subject.Replace(" ", "+");
		}
		
		public static string CommitteeLink(string committee) {
			int cs = committee.IndexOf(" -- ");
			string id = Reps.GetCommitteeId(Util.CurrentSession, cs == -1 ? committee : committee.Substring(0, cs), cs == -1 ? "" : committee.Substring(cs+4));
			return Util.UrlBase + "/congress/committee.xpd?id=" + id;
		}
	
		public static Table LoadIndex(int session, string type, string term) {
			return LoadIndexByStatusDate(session, type, term, null);
		}

		public static Table LoadIndexByStatusDate(int session, string type, string term, string since) {
			return Util.Database.DBSelect("billindex left join billstatus on billindex.session=billstatus.session and billindex.type=billstatus.type and billindex.number=billstatus.number",
				"billstatus.*",
				new Database.SpecEQ("billindex.session", session),
				new Database.SpecEQ("idx", type), new Database.SpecEQ("value", term),
				(since == null ? null : new Database.SpecGE("billstatus.statusdate", since)));
		}

		public static ArrayList GetAllSubjects() {
			if (AllSubjects == null) {
				Console.Error.WriteLine("Access to subjects data before initialization.");
				throw new UserException("This page is not available at the moment. Please check back in a few seconds.");
			}
			return AllSubjects;
		}

		public static bool IsCurrentTerm(string term) {
			if (AllSubjects == null) {
				Console.Error.WriteLine("Access to subjects data before initialization.");
				throw new UserException("This page is not available at the moment. Please check back in a few seconds.");
			}
			return AllSubjects.BinarySearch(term) >= 0;
		}

		public static ArrayList GetSubjectsForLetter(string letter) {
			char let = letter.ToUpper()[0];
			ArrayList ret = new ArrayList();
			ArrayList subs = GetAllSubjects();
			int first = subs.BinarySearch(let + letter.Substring(1)); // make sure first letter is cap
			if (first < 0) first = -first;
			for (int i = first; i < subs.Count; i++) {
				string s = (string)subs[i];
				if (s[0] != let) break;
				if (letter.Length != 1 && !s.ToLower().StartsWith(letter.ToLower())) continue;
				ret.Add(s);
			}
			return ret;
		}

		public static ArrayList GetTopTerms() {
			if (TopTerms == null) {
				Console.Error.WriteLine("Access to subjects data before initialization.");
				throw new UserException("This page is not available at the moment. Please check back in a few seconds.");
			}
			return TopTerms;
		}

		private static void LoadTopTerms() {
			ArrayList top_terms = new ArrayList();
			Hashtable sub_terms = new Hashtable();
			ArrayList subjects = new ArrayList();

			foreach (string filename in new string[] { "liv111.xml", "crsnet.xml" }) {
			Util.SandboxDownload(Util.DataPath + Path.DirectorySeparatorChar + filename);
			XmlDocument liv = new XmlDocument();
			liv.Load(Util.DataPath + Path.DirectorySeparatorChar + filename);
			foreach (XmlElement tt in liv.SelectNodes("liv/top-term")) {
				subjects.Add(tt.GetAttribute("value"));
				top_terms.Add(tt.GetAttribute("value"));
				ArrayList sub = new ArrayList();
				sub_terms[tt.GetAttribute("value")] = sub;
				foreach (XmlElement t in tt.SelectNodes("term")) {
					sub.Add(t.GetAttribute("value"));
					subjects.Add(t.GetAttribute("value"));
				}
			}
			}
			
			subjects.Sort();
			
			TopTerms = top_terms;
			SubTerms = sub_terms;
			AllSubjects = subjects;
		}

		public static ArrayList GetSubTerms(string term) {
			if (SubTerms == null) {
				Console.Error.WriteLine("Access to subjects data before initialization.");
				throw new UserException("This page is not available at the moment. Please check back in a few seconds.");
			}

			ArrayList ret = (ArrayList)SubTerms[term];
			if (ret == null) ret = new ArrayList();
			return ret;
		}

		private static void LoadRelatedPages() {
			related_sites_by_subject = new Hashtable();
			related_sites_by_bill = new Hashtable();

			XmlDocument relatedsites = new XmlDocument();
			try {
				relatedsites.Load("/home/govtrack/extdata/relatedsites/citizenjoe.xml");
			} catch (Exception e) {
			}

			foreach (XmlElement e in relatedsites.SelectNodes("site/page/bill")) {
				AddEntry(related_sites_by_bill, e.GetAttribute("id"), (XmlElement)e.ParentNode);
			}

			foreach (XmlElement e in relatedsites.SelectNodes("site/page/crs")) {
				AddEntry(related_sites_by_subject, e.GetAttribute("term"), (XmlElement)e.ParentNode);
			}
		}

		public static ArrayList GetRelatedPages(string billid, XPathNodeIterator crsterms) {
			Hashtable r = new Hashtable();

			if (billid != "" && related_sites_by_bill.ContainsKey(billid))
				foreach (XmlElement e in (ArrayList)related_sites_by_bill[billid])
					r[e] = e;

			bool first = true;
			while (crsterms.MoveNext()) {
				string term = crsterms.Current.Value;
				if (related_sites_by_subject.ContainsKey(term))
					foreach (XmlElement e in (ArrayList)related_sites_by_subject[term])
						r[e] = e;
				if (first && r.Count > 0) break; first = false;
			}

			ArrayList ret = new ArrayList();
			foreach (XmlElement e in r.Keys) {
				Hashtable h = new Hashtable();
				h["title"] = e.GetAttribute("title");
				h["link"] = e.GetAttribute("link");
				h["site"] = e.OwnerDocument.DocumentElement.GetAttribute("name");
				h["sitelink"] = e.GetAttribute("link");
				ret.Add(h);
			}
			return ret;
		}

		private static void AddEntry(Hashtable h, string key, XmlElement value) {
			ArrayList a;
			a = (ArrayList)h[key];
			if (a == null) {
				a = new ArrayList();
				h[key] = a;
			}
			a.Add(value);
		}
	}
	
}
