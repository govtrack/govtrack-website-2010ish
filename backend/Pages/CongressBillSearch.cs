using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using System.Web;
using System.Xml;

using XPD;
using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

using Lucene.Net.Index;
using Lucene.Net.Documents;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Search;
using Lucene.Net.QueryParsers;

namespace GovTrack.Web.Pages.Congress {

	public class BillSearch {
		private class TermBuilder {
			ArrayList terms = new ArrayList();
			StringBuilder term = new StringBuilder();
			
			public void Append(char c) {
				term.Append(c);
				if (term.Length > 30)
					throw new UserException("You have a word that is too long.");
			}
			
			public void NewTerm() {
				if (term.Length == 0) return;
				if (term.Length < 3)
					throw new UserException("Your search term '" + term.ToString() + "' was too short.");
				terms.Add(term.ToString().ToLower());
				term = new StringBuilder();
				if (terms.Count > 8)
					throw new UserException("You have too many words in your search.  Try grouping phrases with quotes.");
			}
			
			public string[] GetTerms() {
				NewTerm();
				return (string[])terms.ToArray(typeof(string));
			}
		}
		
		public static Hashtable SearchNew() {
			return Search(false);
		}

		public static Hashtable SearchApi() {
			return Search(true);
		}

		public static Hashtable Search(bool api) {
			BillType type;
			int number;
			int session = -1;
			
			if (HttpContext.Current.Request["session"] != null && HttpContext.Current.Request["session"] != "")
				session = int.Parse(HttpContext.Current.Request["session"]);
			
			string q = HttpContext.Current.Request["q"];

			int start = 0, count = (!api ? 30 : 1000);
			if (HttpContext.Current.Request["start"] != null) start = int.Parse(HttpContext.Current.Request["start"]);
			if (HttpContext.Current.Request["count"] != null) count = int.Parse(HttpContext.Current.Request["count"]);
			
			BooleanQuery query = new BooleanQuery();
			
			Hashtable no_results = new Hashtable();
			no_results["count"] = 0;

			if (q != null && q.IndexOf("*") > -1)
				return no_results;

			if (!api && session == -1 && q != null) {
				int slash = q.IndexOf('/');
				if (slash >= q.Length - 4 && slash > 2) {
					try {
						session = int.Parse(q.Substring(slash+1)); // and if that worked...
						q = q.Substring(0, slash);
						HttpContext.Current.Response.Redirect("billsearch.xpd?session=" + session + "&q=" + HttpUtility.UrlEncode(q));
						return null;
					} catch { }
				}
			}

			if (session == -1) session = Util.CurrentSession;

			//Console.Error.WriteLine("Find: " + q);
			
			string search_method = "search";
			ArrayList specs = new ArrayList();
			Hashtable scores = new Hashtable();
			
			// Match a bill number exactly
			if (q != null && Bills.ParseID(q, out type, out number)) {
				if (!api) {
					// Redirect the user right to the bill page.
					// Don't even check if bill exists.
					HttpContext.Current.Response.Redirect(
						Bills.BillLink2(session, type, number));
					return null;
				} else {
					search_method = "search by bill number";
					scores[session + EnumsConv.BillTypeToString(type) + number] = 1.0F;
					specs.Add(new Database.AndSpec(
						new Database.SpecEQ("session", session),
						new Database.SpecEQ("type", EnumsConv.BillTypeToString(type)),
						new Database.SpecEQ("number", number)));
				}
			}

			// Match public law number exactly
			if (!api && q != null && (q.StartsWith("P.L.") || q.StartsWith("PL"))) {
				try {
					string num = null;
					if (q.StartsWith("P.L.")) num = q.Substring(4);
					if (q.StartsWith("PL")) num = q.Substring(2);
					num = num.Replace(" ", "");
					
					int dash = num.IndexOf('-');
					int s = int.Parse(num.Substring(0, dash));
					
					TableRow bill = Util.Database.DBSelectFirst("billindex", "session, type, number",
						new Database.SpecEQ("idx", "publiclawnumber"),
						new Database.SpecEQ("session", s),
						new Database.SpecEQ("value", num));
					
					if (bill != null) {
						if (!api) {
							HttpContext.Current.Response.Redirect(Bills.BillLink3((int)bill["session"], (string)bill["type"], (int)bill["number"]));
							return null;
						} else {
							search_method = "search by public law number";
							scores[(int)bill["session"] + (string)bill["type"] + (int)bill["number"]] = 1.0F;
							specs.Add(new Database.AndSpec(
								new Database.SpecEQ("session", (int)bill["session"]),
								new Database.SpecEQ("type", (string)bill["type"]),
								new Database.SpecEQ("number", (int)bill["number"])));
						}
					}
				} catch {
				}
			}

			if (session == -1) session = Util.CurrentSession;

			// Match USC reference
			Regex uscexp = new Regex(@"(\d[0-9A-Za-z\-]*)\s+U\.?S\.?C\.?\s+(\d[0-9A-Za-z\-]*)((\s*\([^\) ]+\))*)",
			        RegexOptions.IgnoreCase);
			Match uscmc = (q == null ? null : uscexp.Match(q));
			if (uscmc != null && uscmc.Success) {
				string title = uscmc.Groups[1].Value;
				string section = uscmc.Groups[2].Value;
				string paragraph = uscmc.Groups[3].Value;
				
				string[] ps = paragraph.Split('[', '(', ')', ' ');
				int psi = 0; while (psi < ps.Length-1 && ps[psi] == "") psi++;
				int pse = ps.Length-1; while (pse > 0 && ps[pse] == "") pse--;
				if (ps.Length != 0)
					paragraph = "_" + String.Join("_", ps, psi, pse-psi+1);

				Table table = Util.Database.DBSelect("billusc", "session, type, number",
					new Database.SpecEQ("session", session),
					new Database.OrSpec(
						new Database.SpecEQ("ref", title + "_" + section + paragraph),
						new Database.SpecStartsWith("ref", title + "_" + section + paragraph + "_") ));
				foreach (TableRow bill in table) {
					search_method = "search by U.S.C. section";
					scores[(int)bill["session"] + (string)bill["type"] + (int)bill["number"]] = 1.0F;
					specs.Add(new Database.AndSpec(
						new Database.SpecEQ("session", (int)bill["session"]),
						new Database.SpecEQ("type", (string)bill["type"]),
						new Database.SpecEQ("number", (int)bill["number"])));
				}
			}
			
			int total_count = -1;

			if (specs.Count == 0) {

			if (q != null && q.Trim() != "") {
			BooleanQuery query1 = new BooleanQuery();
			query.Add(query1, BooleanClause.Occur.MUST);
			try {
				/*if (!q.StartsWith("-")) {
					PhraseQuery pq = new PhraseQuery();
					pq.Add( new Term("shorttitles", q) );
					pq.SetBoost((float)4);
					query1.Add(pq, false, false);
				}*/

				Query query_titles2 = new QueryParser("shorttitles", new StandardAnalyzer()).Parse(q);
				query_titles2.SetBoost((float)3);
				query1.Add(query_titles2, BooleanClause.Occur.SHOULD);

				Query query_titles1 = new QueryParser("officialtitles", new StandardAnalyzer()).Parse(q);
				query_titles1.SetBoost((float)2);
				query1.Add(query_titles1, BooleanClause.Occur.SHOULD);

				Query query_summary = new QueryParser("summary", new StandardAnalyzer()).Parse(q);
				query1.Add(query_summary, BooleanClause.Occur.SHOULD);

				Query query_text = new QueryParser("fulltext", new StandardAnalyzer()).Parse(q);
				query1.Add(query_text, BooleanClause.Occur.SHOULD);
			} catch (Exception e) {
				return no_results;
			}
			}

			string chamber = HttpContext.Current.Request["chamber"];
			string[] status = HttpContext.Current.Request["status"] == null ? null : HttpContext.Current.Request["status"].Split(',');
			string sponsor = HttpContext.Current.Request["sponsor"];
			string cosponsor = HttpContext.Current.Request["cosponsor"];

			if (chamber != null && (chamber == "s" || chamber == "h"))
				query.Add(new WildcardQuery(new Term("type", chamber + "*")), BooleanClause.Occur.MUST);
			if (status != null && status[0] != "") {
				List<Term> terms = new List<Term>();
				foreach (string s in status)
					terms.Add(new Term("state", s));
				MultiPhraseQuery mpq = new MultiPhraseQuery();
				mpq.Add(terms.ToArray());
				query.Add(mpq, BooleanClause.Occur.MUST);
			}
			if (sponsor != null && sponsor != "")
				query.Add(new TermQuery(new Term("sponsor", sponsor)), BooleanClause.Occur.MUST);
			if (cosponsor != null && cosponsor != "")
				query.Add(new TermQuery(new Term("cosponsor", cosponsor)), BooleanClause.Occur.MUST);

			IndexSearcher searcher = new IndexSearcher(Util.DataPath + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + "index.bills.lucene");
		
			Sort sort = null;
			if (HttpContext.Current.Request["sort"] != null && HttpContext.Current.Request["sort"] == "introduced")
				sort = new Sort(new SortField("introduced", SortField.STRING, true));
			if (HttpContext.Current.Request["sort"] != null && HttpContext.Current.Request["sort"] == "lastaction")
				sort = new Sort(new SortField("lastaction", SortField.STRING, true));
			
			Hits hits = searcher.Search(query, sort == null ? new Sort() : sort);
			
			int end = hits.Length();
			if (start + count < end) end = start + count;
			total_count = hits.Length();
			
			for (int i = start; i < end; i++) {
				Document doc = hits.Doc(i);
				string billsession = doc.Get("session");
				string billtype = doc.Get("type");
				string billnumber = doc.Get("number");

				int istatus = (int)EnumsConv.BillStatusFromString(doc.Get("status"));

				float score;
				if (sort == null) // readjust the score based on status
					score = hits.Score(i) + istatus/(float)8*(float).2;
				else // keep order from Lucene
					score = -i;

				scores[billsession + billtype + billnumber] = score;
				specs.Add(new Database.AndSpec(
					new Database.SpecEQ("session", billsession),
					new Database.SpecEQ("type", billtype),
					new Database.SpecEQ("number", billnumber)));
			}

			if (HttpContext.Current.Request["sort"] != null && HttpContext.Current.Request["sort"] == "hits" && specs.Count > 0) {
				Table hitsinfo = Util.Database.DBSelect("billhits", "*",
					Database.OrSpec.New((Database.Spec[])specs.ToArray(typeof(Database.Spec))));
				foreach (TableRow billhits in hitsinfo) {
					scores["" + billhits["session"] + billhits["type"] + billhits["number"]] = (float)(int)billhits["hits1"];
				}
			}
			
			}

			if (specs.Count == 0)
				return no_results;
			
			Table billinfo = Util.Database.DBSelect("billstatus", "*",
				Database.OrSpec.New((Database.Spec[])specs.ToArray(typeof(Database.Spec))));

			if (total_count == -1)
				total_count = billinfo.Rows;

			ArrayList ret = new ArrayList();
			foreach (TableRow r in billinfo)
				ret.Add(r);
	
			BillHitComparer bhc = new BillHitComparer();
			bhc.scores = scores;
			ret.Sort(bhc);
			
			Hashtable ret2 = new Hashtable();
			ret2["count"] = total_count;
			ret2["method"] = search_method;
			ret2["results"] = ret;

			return ret2;
		}

		private class BillHitComparer : IComparer {
			public Hashtable scores;
			public int Compare(object a, object b) {
				TableRow x = (TableRow)a;
				TableRow y = (TableRow)b;
				float s1 = (float)scores["" + x["session"] + x["type"] + x["number"]];
				float s2 = (float)scores["" + y["session"] + y["type"] + y["number"]];
				return s2.CompareTo(s1);
			}
		}
	}
}
