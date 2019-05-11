using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Web;

using XPD;
using GovTrack.Enums;

namespace GovTrack.Web {

	public abstract class Monitor {
		public class Options {
			public bool MeetingsByEventDate;
		}
	
		public abstract string Encoded();
		public abstract string Display();

		public virtual string Link() { return Util.UrlBase + "/users/events.xpd?monitors=" + Encoded(); }
		
		public virtual bool Stale() { return false; }

		public virtual string[] MatchUrls() { return new string[0]; }
		
		//private static DynamicEventsCache cache = new DynamicEventsCache();
		
		public ArrayList GetEvents(DateTime start, DateTime end, Options options) {
			try {
				return GetEventsInternal(start, end, options);
			} catch (Exception e) {
				throw new UserException("There was an internal error preventing the events for '" + Display() + "' from being loaded.", e);
			}
		}
		
		protected abstract ArrayList GetEventsInternal(DateTime start, DateTime end, Options options);
		
		public static Monitor FromString(string monitor) {
			bool validity;
			return FromString(monitor, out validity);
		}

		public static Monitor FromString(string monitor, out bool isValid) {
			isValid = true;
		
			if (monitor.StartsWith("p:"))
				return new PersonMonitor(int.Parse(monitor.Substring(2)));
			if (monitor.StartsWith("pv:"))
				return new PersonVotingRecordMonitor(int.Parse(monitor.Substring(3)));
			if (monitor.StartsWith("ps:"))
				return new PersonSponsorshipMonitor(int.Parse(monitor.Substring(3)));

			if (monitor.StartsWith("bill:"))
				return new BillMonitor(monitor.Substring(5));

			if (monitor.StartsWith("crs:"))
				return new SubjectMonitor(monitor.Substring(4));
			if (monitor.StartsWith("committee:"))
				return new CommitteeMonitor(monitor.Substring(10));

			if (monitor.StartsWith("district:"))
				return new DistrictMonitor(monitor.Substring(9));
			
			if (monitor == "misc:activebills")
				return new ActiveBillsMonitor(monitor);
			if (monitor == "misc:enactedbills")
				return new ActiveBillsMonitor(monitor);
			if (monitor == "misc:introducedbills")
				return new ActiveBillsMonitor(monitor);
			if (monitor == "misc:activebills2")
				return new ActiveBillsMonitor(monitor);

			if (monitor == "misc:allcommittee")
				return new UpcomingCommitteeMeetingsMonitor();

			if (monitor == "misc:allvotes")
				return new AllVotesMonitor();
			if (monitor == "misc:housevotes")
				return new AllVotesMonitor("h");
			if (monitor == "misc:senatevotes")
				return new AllVotesMonitor("s");
				
			if (monitor == "misc:questions")
				return new QuestionAnswerMonitor();

			if (monitor == "misc:videos")
				return new VideosMonitor();

			if (monitor.StartsWith("questions:"))
				return new QuestionAnswerMonitor2(monitor.Substring("questions:".Length));

			isValid = false;
			
			if (monitor == "option:relatedblogs")
				isValid = true;
				
			return null;
		}
	}
		
	public class PersonMonitor : Monitor {
		public readonly int Person;
		
		public PersonMonitor(int id) { Person = id; }
		
		public override string Encoded() { return "p:" + Person; }		
		public override string Display() { return Reps.FormatPersonName(Person, "now", "lastnamestate"); }
		public override string Link() { return Reps.RepLink(Person); }
		
		public override bool Stale() { return !Reps.HasCurrentRole(Person); }

		public override string[] MatchUrls() { return new string[] {
			"http://www.govtrack.us/congress/person.xpd?id=" + Person }; }

		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			string name = Display();
		
			ArrayList ret = new ArrayList();
		
			/*XPathNavigator speeches = Reps.GetSpeeches(Person);
			XPathNodeIterator sp = speeches.Select("speeches/cr[@datetime >= '" + Util.DateTimeToDT(start) + "' and @datetime <= '" + Util.DateTimeToDT(end) + "']");
			while (sp.MoveNext()) {
				SpeechEvent evt = new SpeechEvent(sp.Current, name);
				ret.Add(evt);
			}*/
			
            Table bills = Util.Database.DBSelect("billstatus left join billindex on billindex.session=billstatus.session and billindex.type=billstatus.type and billindex.number=billstatus.number",
                "billstatus.*",
                new Database.SpecEQ("idx", "sponsor"), new Database.SpecEQ("value", Person.ToString()),
                new Database.SpecGE("billstatus.statusdate", start),
                new Database.SpecLT("billstatus.statusdate", end));
			foreach (TableRow bill in bills)
				ret.Add(new BillEvent(bill));
			
            Table videos = Reps.GetRecentVideos(Person, 0, Util.DateTimeToDT(start), Util.DateTimeToDT(end));
			foreach (TableRow video in videos)
				ret.Add(new VideoEvent(video));
			
			return ret;
		}
	}

	public class PersonVotingRecordMonitor : Monitor {
		public readonly int Person;
		
		public PersonVotingRecordMonitor(int id) { Person = id; }
		
		public override string Encoded() { return "pv:" + Person; }		
		public override string Display() { return Reps.FormatPersonName(Person, "now", "short") + "’s Voting Record"; }
		public override string Link() { return Reps.RepLink(Person); }
		
		public override bool Stale() { return !Reps.HasCurrentRole(Person); }

		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			ArrayList ret = new ArrayList();
		
			Table votes = Util.Database.DBSelect("people_votes, votes LEFT JOIN billstatus ON votes.billsession=billstatus.session and votes.billtype=billstatus.type and votes.billnumber=billstatus.number",
				"votes.id, votes.date, votes.description, votes.result, people_votes.displayas, billstatus.title, votes.billsession, votes.billtype, votes.billnumber",
            new Database.SpecEQ("people_votes.personid", Person),
				new Database.UserSpec("votes.id=people_votes.voteid"),
				new Database.SpecGE("votes.date", start),
				new Database.SpecLT("votes.date", end),
				new Database.SpecOrder("votes.date", false)
				);
			
			foreach (TableRow vote in votes)
				ret.Add( new VoteEvent(vote, Person) );
			
			return ret;
		}
	}

	public class PersonSponsorshipMonitor : Monitor {
		public readonly int Person;
		
		public PersonSponsorshipMonitor(int id) { Person = id; }
		
		public override string Encoded() { return "ps:" + Person; }		
		public override string Display() { return Reps.FormatPersonName(Person, "now", "lastnamestate"); }
		public override string Link() { return Reps.RepLink(Person); }
		
		public override bool Stale() { return !Reps.HasCurrentRole(Person); }

		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			string name = Display();
		
			ArrayList ret = new ArrayList();
		
            Table bills = Util.Database.DBSelect("billstatus left join billindex on billindex.session=billstatus.session and billindex.type=billstatus.type and billindex.number=billstatus.number",
                "billstatus.*",
                new Database.SpecEQ("idx", "sponsor"), new Database.SpecEQ("value", Person.ToString()),
                new Database.SpecGE("billstatus.statusdate", start),
                new Database.SpecLT("billstatus.statusdate", end));
			foreach (TableRow bill in bills)
				ret.Add(new BillEvent(bill));

            bills = Util.Database.DBSelect("billstatus left join billindex on billindex.session=billstatus.session and billindex.type=billstatus.type and billindex.number=billstatus.number",
                "billstatus.*",
                new Database.SpecEQ("idx", "cosponsor"), new Database.SpecEQ("value", Person.ToString()),
                new Database.SpecGE("billstatus.statusdate", start),
                new Database.SpecLT("billstatus.statusdate", end));
			foreach (TableRow bill in bills)
				ret.Add(new BillEvent(bill));
			
			return ret;
		}
	}

	public class BillMonitor : Monitor {
		public readonly int Session;
		public readonly BillType Type;
		public readonly int Number;
		
		public static readonly string[]
			reporttypes = new string[] { "cbo", "ombsap" },
			reportnames = new string[] { "Budget Report", "Statement of Administration Policy" },
			reportlongnames = new string[] { "Congressional Budget Office Report", "Statement of Administration Policy" };
		
		public XPathNavigator UseIndexedNavigator = null;
		
		XPathNavigator billSrc = null;
		
		public BillMonitor(int session, BillType type, int number) { Session = session; Type = type; Number = number; }

		public BillMonitor(int session, string type, int number) { Session = session; Type = EnumsConv.BillTypeFromString(type); Number = number; }
		
		public BillMonitor(string id) {
			if (!GovTrack.Web.Pages.Congress.Bill.ParseBill(id, out Session, out Type, out Number))
				throw new ArgumentException("Invalid bill ID: " + id);
		}
		
		public override string Encoded() { return "bill:" + EnumsConv.BillTypeToString(Type) + Session + "-" + Number; }		
		public override string Display() {
			if (UseIndexedNavigator != null)
				return (string)UseIndexedNavigator.Evaluate("string(@title)");
			
			if (billSrc == null)
				billSrc = Bills.LoadBill(Session, Type, Number);
			return Bills.DisplayString(billSrc.Select("/bill"), 100);
		}		
		public override string Link() { return Bills.BillLink2(Session, Type, Number); }

		public override bool Stale() { return Session < Util.CurrentSession; }

		public override string[] MatchUrls() { return new string[] {
			"http://www.govtrack.us/congress/bill.xpd?bill=" + EnumsConv.BillTypeToString(Type) + Session + "-" + Number,
			"http://www.opencongress.org/bill/" + Session + "-" + EnumsConv.BillTypeToString(Type) + Number + "/show",
			"http://feeds.feedburner.com/bill/" + Session + "-" + EnumsConv.BillTypeToString(Type) + Number + "/show",
			GovTrack.Web.Pages.Congress.Bill.GetThomasLink(Session.ToString(), EnumsConv.BillTypeToString(Type), Number)
			 }; }
		
		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			XPathNavigator bill;
			string title;
			
			if (UseIndexedNavigator == null) {
				if (billSrc == null)
					billSrc = Bills.LoadBill(Session, Type, Number);
				bill = billSrc;
				bill.MoveToFirstChild();
				title = Bills.DisplayString(bill.Select("."), 100);
			} else {
				bill = UseIndexedNavigator.Clone();
				title = bill.GetAttribute("title", "");
			}

			ArrayList ret = new ArrayList();
			
			bool loadspeeches = true;
			
			// Bill Activity
			if (UseIndexedNavigator == null) {
				string lastacs = (string)bill.Evaluate("string(actions/" + "*[position()=last()]/@datetime)");
				if (lastacs != "") {
					DateTime lastaction = Util.DTToDateTime(lastacs);
					if (lastaction >= start && lastaction <= end)
						ret.Add(new BillEvent(this));
				}
			} else {
				DateTime date = Util.DTToDateTime((string)bill.Evaluate("string(*/@datetime)"));
				if (date >= start && date <= end)
					ret.Add(new BillEvent(bill.Clone(), true));
			}

			// Committee Meetings
			XPathNavigator committees = Util.LoadCacheData(Util.CurrentSession, "committeeschedule.xml");
			XPathNodeIterator commmtgs = committees.Select("committee-schedule/meeting[count(bill[@session = " + Session + " and @type = '" + EnumsConv.BillTypeToString(Type) + "' and @number = " + Number + "]) > 0]");
			while (commmtgs.MoveNext())
				ret.Add( new CommitteeEvent(commmtgs.Current, options) );
			
			// Reports
			for (int rt = 0; rt < reporttypes.Length; rt++) {
				string path2 = "bills." + reporttypes[rt] + Path.DirectorySeparatorChar + EnumsConv.BillTypeToString(Type) + Number;
				
				try {
					XPathNavigator rep = Util.LoadData(Session, path2 + ".xml");
					DateTime date = Util.DTToDateTime((string)rep.Evaluate("string(report/@datetime)"));
					string summary = (string)rep.Evaluate("string(report/summary)");
					if (summary == null || summary == "") summary = "No summary available.";
					else summary = "\"" + summary + "\"";
					
					if (date >= start && date <= end)
						ret.Add( new ReportEvent("http://www.govtrack.us/congress/billreport.xpd?bill=" + EnumsConv.BillTypeToString(Type) + Session + "-" + Number + "&type=" + reporttypes[rt], reportnames[rt] + " for " + title, "A new " + reportlongnames[rt] + " is available: " + summary, date) );
				} catch (Exception e) { }
			}
			
			// Amendments
			if (billSrc != null) {
				XPathNodeIterator amendments = billSrc.Select("amendments/" + "*");
				while (amendments.MoveNext()) {
					string amendmentno = (string)amendments.Current.Evaluate("string(@number)");
					XPathNavigator amendment = Bills.LoadAmendment(Session, amendmentno);
					XPathNodeIterator actions = amendment.Select("amendment/actions/" + "*[@datetime >= '" + Util.DateTimeToDT(start) + "' and @date <= '" + Util.DateTimeToDT(end) + "']");
					
					string amdchamber = (string)amendment.Evaluate("string(amendment/@chamber)");
					string amdnumber = (string)amendment.Evaluate("string(amendment/@number)");
					string amdname = "";
					if (amdchamber == "h") amdname = "H.Amdt.";
					if (amdchamber == "s") amdname = "S.Amdt.";
					amdname += " " + amdnumber;
					
					while (actions.MoveNext()) {
						string evtitle = amdname;
						string text = actions.Current.Value;
						if (text.IndexOf("(consideration") != -1)
							text = text.Substring(0, text.IndexOf("(consideration"));

						if (actions.Current.Name == "vote") {
							evtitle += " (Vote)";
						} else if (text.IndexOf("proposed") != -1 || text.IndexOf("offered") != -1) {
							evtitle += " (Offered)";
							text += " " + (string)amendment.Evaluate("string(amendment/description)");
						}
							
						BillEvent be = new BillEvent(bill.Clone(), false);
						be.Summary = "Amendment actions:";
						be.Date = Util.DTToDateTime(actions.Current.GetAttribute("datetime", ""));
						ret.Add(be);
						be.Specifics.Add( new Event.Specific(evtitle, Bills.BillAmendmentLink(Session, Type, Number, amdchamber+amdnumber), text) );
					}
				}
			}
			
			// Cosponsorship changes
			if (billSrc != null) {
				XPathNodeIterator cosponsors = billSrc.Select("cosponsors/cosponsor");
				while (cosponsors.MoveNext()) {
					string pid = (string)cosponsors.Current.Evaluate("string(@id)");
					string joined = (string)cosponsors.Current.Evaluate("string(@joined)");
					string withdrawn = (string)cosponsors.Current.Evaluate("string(@withdrawn)");
					Monitor p = new PersonSponsorshipMonitor(int.Parse(pid));
					if (joined != "") {
						DateTime joined2 = Util.DTToDateTime(joined);
						if (joined2 >= start && joined2 <= end) {
							BillEvent be = new BillEvent(bill.Clone(), false);
							be.Summary = "Cosponsorship change.";
							be.Date = joined2;
							ret.Add(be);
							be.Specifics.Add( new Event.Specific(p.Display(), p.Link(), "New cosponsor.") );
						}
					}
					if (withdrawn != "") {
						DateTime withdrawn2 = Util.DTToDateTime(withdrawn);
						if (withdrawn2 >= start && withdrawn2 <= end) {
							BillEvent be = new BillEvent(bill.Clone(), false);
							be.Summary = "Cosponsorship change.";
							be.Date = withdrawn2;
							ret.Add(be);
							be.Specifics.Add( new Event.Specific(p.Display(), p.Link(), "Cosponsorship withdrawn.") );
						}
					}
				}
			}
			
			return ret;
		}
	}
	
	public abstract class SubjectCommitteeMonitor : Monitor {
		public readonly string Type;
		public readonly string Term;
		
		public SubjectCommitteeMonitor(string type, string term) {
			Type = type;
			Term = term;
			
			if (term.Length > 160) throw new ArgumentException(term);
			if (term.IndexOfAny(new char[] { '/', '\\', ':' }) != -1) throw new ArgumentException(term);
		}
		
		public override string Encoded() { return Type + ":" + Term; }		
		public override string Display() {
			return Term;
		}

		public override string[] MatchUrls() {
			// This is way inefficient.
            Table bills = Util.Database.DBSelect("billindex", "session, type, number",
                new Database.SpecEQ("session", Util.CurrentSession), new Database.SpecEQ("idx", Type), new Database.SpecEQ("value", Term));
            List<string> urls = new List<string>();
			foreach (TableRow bill in bills)
				foreach (string url in new BillMonitor((int)bill["session"], (string)bill["type"], (int)bill["number"]).MatchUrls())
					urls.Add(url);
			return urls.ToArray();
		}
		
		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			ArrayList ret = new ArrayList();
		
            Table bills = Util.Database.DBSelect("billstatus left join billindex on billindex.session=billstatus.session and billindex.type=billstatus.type and billindex.number=billstatus.number",
                "billstatus.*",
                new Database.SpecEQ("idx", Type), new Database.SpecEQ("value", Term),
                new Database.SpecGE("billstatus.statusdate", start),
                new Database.SpecLT("billstatus.statusdate", end));
			foreach (TableRow bill in bills)
				ret.Add(new BillEvent(bill));

            Table billevents = Util.Database.DBSelect("billevents"
				+ " left join billindex on billindex.session=billevents.session and billindex.type=billevents.type and billindex.number=billevents.number"
				+ " left join billstatus on billstatus.session=billevents.session and billstatus.type=billevents.type and billstatus.number=billevents.number",
                "billevents.session, billevents.type, billevents.number, billevents.date, billevents.eventxml, billstatus.title",
                new Database.SpecEQ("idx", Type), new Database.SpecEQ("value", Term),
                new Database.SpecGE("billevents.date", start),
                new Database.SpecLT("billevents.date", end));
			foreach (TableRow evt in billevents) {
				string title = (string)evt["title"];
				DateTime date = (DateTime)evt["date"];
				XmlDocument info = new XmlDocument();
				info.LoadXml((string)evt["eventxml"]);
				if (info.DocumentElement.Name == "report") {
					int rt = Array.IndexOf(BillMonitor.reporttypes, info.DocumentElement.GetAttribute("type"));
					XmlNode summarynode = info.DocumentElement.SelectSingleNode("summary");
					string summary = "No summary is available.";
					if (summarynode != null) summary = "Here is a summary: " + summarynode.InnerText;
					
					ret.Add( new ReportEvent(
						"http://www.govtrack.us/congress/billreport.xpd?bill=" + evt["type"] + evt["session"] + "-" + evt["number"]
						+ "&type=" + info.DocumentElement.GetAttribute("type"),
						BillMonitor.reportnames[rt] + " for " + title, "A new " + BillMonitor.reportlongnames[rt] + " is available. " + summary, date) );
				}
			}

			// Committee hearings
			if (Type == "committee") {
				XPathNavigator committees = Util.LoadCacheData(Util.CurrentSession, "committeeschedule.xml");
				XPathNodeIterator commmtgs = committees.Select("committee-schedule/meeting");
				while (commmtgs.MoveNext()) {
					if (!commmtgs.Current.GetAttribute("committee", "").StartsWith(Term)) continue;
					DateTime dt;
					if (options.MeetingsByEventDate)
						dt = Util.DTToDateTime(commmtgs.Current.GetAttribute("datetime", ""));
					else
						dt = Util.DTToDateTime(commmtgs.Current.GetAttribute("postdate", ""));
					if (dt > start)
						ret.Add( new CommitteeEvent(commmtgs.Current, options) );
				}
			}
			
			return ret;
		}
	}
	
	public class SubjectMonitor : SubjectCommitteeMonitor {
		public SubjectMonitor(string subject) : base("crs", subject) {
		}
		
		public override string Link() { return Subjects.CRSLink(Term); }
		public override bool Stale() { return !Subjects.IsCurrentTerm(Term); }
	}
	
	public class CommitteeMonitor : SubjectCommitteeMonitor {
		public CommitteeMonitor(string committee) : base("committee", committee) {
		}			
		
		public override string Link() { return Subjects.CommitteeLink(Term); }
	}
	
	public class UpcomingCommitteeMeetingsMonitor : Monitor {
		public override string Encoded() { return "misc:allcommittee"; }		
		public override string Display() {
			return "Upcoming Congressional Committee Meetings";
		}
		
		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			ArrayList ret = new ArrayList();
		
			XPathNavigator committees = Util.LoadCacheData(Util.CurrentSession, "committeeschedule.xml");
			XPathNodeIterator commmtgs = committees.Select("committee-schedule/meeting");
			while (commmtgs.MoveNext()) {
				DateTime dt;
				if (options.MeetingsByEventDate)
					dt = Util.DTToDateTime(commmtgs.Current.GetAttribute("datetime", ""));
				else
					dt = Util.DTToDateTime(commmtgs.Current.GetAttribute("postdate", ""));
				if (dt > start)
					ret.Add( new CommitteeEvent(commmtgs.Current, options) );
			}
			
			return ret;
		}
	}

	public class ActiveBillsMonitor : Monitor {
		string evt;

		public ActiveBillsMonitor(string e) { evt = e; }

		public override string Encoded() {
			return evt;
		}
		public override string Display() {
			switch (evt) {
			case "misc:activebills": return "Active Legislation";
			case "misc:activebills2": return "Active Legislation (Except New Bills)";
			case "misc:enactedbills": return "Enacted Legislation";
			case "misc:introducedbills": return "Introduced Legislation";
			}
			throw new Exception();
		}		
		
		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			Table bills = Util.Database.DBSelect("billstatus", "*",
				new Database.SpecGE("statusdate", start),
				new Database.SpecLT("statusdate", end));

			ArrayList ret = new ArrayList();
			foreach (TableRow bill in bills) {
				switch (evt) {
					case "misc:activebills2":
					case "misc:enactedbills":
					case "misc:introducedbills":
						XmlReader reader = new XmlTextReader(new StringReader((string)bill["statusxml"]));
						reader.Read();
						string statusName = reader.Name;
						switch (evt) {
							case "misc:activebills2": if (statusName == "introduced") continue; break;
							case "misc:enactedbills": if (statusName != "enacted") continue; break;
							case "misc:introducedbills": if (statusName != "introduced") continue; break;
						}
						break;
				}

				ret.Add(new BillEvent(bill));
			}
			
			return ret;
		}
	}
		
	public class AllVotesMonitor : Monitor {
		string where;
		
		public AllVotesMonitor() { }
		public AllVotesMonitor(string where) { this.where = where; }
	
		public override string Encoded() {
			if (where == null)
				return "misc:allvotes";
			else if (where == "h")
				return "misc:housevotes";
			else
				return "misc:senatevotes";
		}

		public override string Display() {
			if (where == null)
				return "Congressional Votes";
			else if (where == "h")
				return "House Votes";
			else
				return "Senate Votes";
		}
		
		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			ArrayList ret = new ArrayList();
		
			Table votes = Util.Database.DBSelect("votes LEFT JOIN billstatus ON votes.billsession=billstatus.session and votes.billtype=billstatus.type and votes.billnumber=billstatus.number",
				"votes.id, votes.date, votes.description, votes.result, billstatus.title, votes.billsession, votes.billtype, votes.billnumber",
				new Database.SpecGE("votes.date", start),
				new Database.SpecLT("votes.date", end),
				where == null ? (Database.Spec)new Database.UserSpec("1") : new Database.SpecStartsWith("votes.id", where),
				new Database.SpecOrder("votes.date", false)
				);
			
			foreach (TableRow vote in votes)
				ret.Add( new VoteEvent(vote, 0) );
			
			return ret;
		}
	}

	public class DistrictMonitor : Monitor {
		public readonly string State;
		public readonly int District;
		
		public DistrictMonitor(string district) : this(district.Substring(0, 2), int.Parse(district.Substring(2))) {
		}

		public DistrictMonitor(string state, int district) {
			State = state.ToLower();
			District = district;
			if (!Util.IsValidStateAbbr(state)) throw new UserException("That's not a valid congressional district.");
		}
		
		public override string Encoded() { return "district:" + State + District; }
		public override string Display() { return State.ToUpper() + "-" + District; }
		public override string Link() { return "http://www.govtrack.us/local/?district=" + State + District; }
		
		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			ArrayList ret = new ArrayList();

			ArrayList reps = Reps.GetReps3(this);
			foreach (int rep in reps)
				ret.AddRange( new PersonMonitor(rep).GetEvents(start, end, options) );

			ret.AddRange( new SubjectMonitor(Util.GetStateName(State)).GetEvents(start, end, options) );
			ret.AddRange( new SubjectMonitor(Util.GetStateName(State)+"-State").GetEvents(start, end, options) );

			return ret;
		}
	}

	public class QuestionAnswerMonitor : Monitor {
		public QuestionAnswerMonitor() { }
		
		public override string Encoded() { return "misc:questions"; }
		public override string Display() { return "Community Questions & Answers"; }
		//public override string Link() { return ...; }
		
		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			ArrayList ret = new ArrayList();

			foreach (TableRow row in Util.Database.DBSelect("questions LEFT JOIN questions AS refq ON refq.id=questions.question", "questions.id, questions.topic, questions.question, questions.approvaldate, questions.text, refq.text AS reftext",
				new Database.SpecEQ("questions.status", "approved"),
				new Database.SpecGE("questions.approvaldate", start),
				new Database.SpecLT("questions.approvaldate", end))) {
				ret.Add(new QuestionAnswerEvent(row));
			}
			return ret;
		}
	}

	public class VideosMonitor : Monitor {
		public VideosMonitor() { }
		
		public override string Encoded() { return "misc:videos"; }
		public override string Display() { return "YouTube Videos"; }
		//public override string Link() { return ...; }
		
		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			ArrayList ret = new ArrayList();
            Table videos = Reps.GetRecentVideos(0, 0, Util.DateTimeToDT(start), Util.DateTimeToDT(end));
			foreach (TableRow video in videos)
				ret.Add(new VideoEvent(video));
			return ret;
		}
	}

	public class QuestionAnswerMonitor2 : Monitor {
		string topic;
		
		public QuestionAnswerMonitor2(string topic) { this.topic = topic; }
		
		public override string Encoded() { return "questions:" + topic; }
		public override string Display() { return "Community Questions & Answers on " + Monitor.FromString(topic).Display(); }
		public override string Link() { return "http://www.govtrack.us/users/questions.xpd?topic=" + topic; }
		
		protected override ArrayList GetEventsInternal(DateTime start, DateTime end, Options options) {
			ArrayList ret = new ArrayList();

			foreach (TableRow row in Util.Database.DBSelect("questions LEFT JOIN questions AS refq ON refq.id=questions.question", "questions.id, questions.topic, questions.question, questions.approvaldate, questions.text, refq.text AS reftext",
				new Database.SpecEQ("questions.status", "approved"),
				new Database.SpecEQ("questions.topic", topic),
				new Database.SpecGE("questions.approvaldate", start),
				new Database.SpecLT("questions.approvaldate", end))) {
				ret.Add(new QuestionAnswerEvent(row));
			}
			return ret;
		}
	}

	public abstract class Event : IComparable {
		public class Options {
			public ArrayList AllMonitors;
		}
	
		public DateTime Date;
		public string TypeName;
		public string Title;
		public string Link;
		public string Summary;
		public string Image;
		public string MonitorCode, MonitorName;
		
		public ArrayList MatchingMonitors = new ArrayList();
		public ArrayList Specifics = new ArrayList();
		
		private bool loadedSpecs = false;
		
		public int CompareTo(object other) {
			return this.Date.CompareTo(((Event)other).Date);
		}
		
		public Event Clone() {
			Event e = (Event)MemberwiseClone();
			e.MatchingMonitors = new ArrayList();
			e.Specifics = new ArrayList();
			return e;
		}

		public virtual void InitializeFields() {
		}
		
		public virtual void GetPeopleIDs(ArrayList list) {
		}

		public class Specific {
			public readonly string Key;
			public readonly string Tag;
			public readonly string Link;
			public readonly string Text;
			public readonly bool Widget;
			
			public Specific(string key, string tag, string link, string text, bool widget) {
				Key = key; Tag = tag; Link = link; Text = text; Widget = widget;
			}

			public Specific(string tag, string link, string text) :
			this("", tag, link, text, false) {
			}
			
			public override int GetHashCode() {
				return Tag.GetHashCode();
			}
			
			public override bool Equals(object other) {
				return Tag == ((Specific)other).Tag;
			}
		}

		protected virtual void LoadSpecifics(Options options) {
		}

		public virtual Hashtable ToHashtable(Options options) {
			if (!loadedSpecs) {
				loadedSpecs = true;
				LoadSpecifics(options);
			}
		
			Hashtable ret = new Hashtable();
			ret["type"] = GetType().Name;
			ret["date"] = Util.DateTimeToDT(Date);
			ret["date_string"] = Util.DateTimeToString(Date);
			ret["typename"] = TypeName;
			ret["title"] = Title;
			ret["link"] = Link;
			
			if (Summary != null)
				ret["summary"] = Summary;
			
			if (Image != null)
				ret["image"] = Image;

			if (MonitorCode != null)
				ret["monitor-code"] = MonitorCode;
			if (MonitorName != null)
				ret["monitor-name"] = MonitorName;
			
			ArrayList spc = new ArrayList();
			foreach (Specific s in Specifics) {
				Hashtable h = new Hashtable();
				h["key"] = s.Key;
				h["tag"] = s.Tag;
				h["link"] = s.Link;
				if (s.Text != null)
					h["text"] = s.Text;
				if (s.Widget)
					h["widget"] = 1;
				spc.Add(h);
			}
			if (spc.Count > 0)
				ret["specifics"] = spc;
			
			ArrayList mon = new ArrayList();
			foreach (Monitor m in MatchingMonitors) {
				Hashtable h = new Hashtable();
				h["text"] = m.Display();
				h["link"] = m.Link();
				mon.Add(h);
			}
			ret["monitors"] = mon;

			return ret;
		}
		
		public static ArrayList GetTrackedEvents(ArrayList monitors, DateTime start, DateTime end, Monitor.Options options) {
			Hashtable events = new Hashtable();
		
			foreach (string monitorstring in monitors) {
				Monitor monitor = Monitor.FromString(monitorstring);
				if (monitor == null) continue;
				
				ArrayList e = monitor.GetEvents(start, end, options);
				
				// GovTrack Insider Articles
				Table insiderlinks = Util.Database.DBSelect(
					"govtrack_insider.aggregator_post_links left join govtrack_insider.aggregator_postlink as l on postlink_id=l.id left join govtrack_insider.aggregator_post as p on post_id=p.id",
					"p.title, p.date, p.slug",
					new Database.SpecGT("p.date", start),
					new Database.SpecLE("p.date", end),
					new Database.SpecIn("l.url", monitor.MatchUrls()),
					new Database.SpecEQ("p.status", "approved") );
				foreach (TableRow link in insiderlinks)
					e.Add( new InsiderEvent(link) );
				
				foreach (Event evt in e) {
					Event added = (Event)events[evt];
					if (added == null) {
						events[evt] = evt;
						evt.MatchingMonitors.Add(monitor);
					} else {
						if (!added.MatchingMonitors.Contains(monitor))
							added.MatchingMonitors.Add(monitor);
						foreach (Specific c in evt.Specifics)
							if (!added.Specifics.Contains(c))
								added.Specifics.Add(c);
					}
				}
			}

			ArrayList peopleIDs = new ArrayList();
			foreach (Event evt in events.Keys)
				evt.GetPeopleIDs(peopleIDs);
			Reps.CacheRolesAt2(peopleIDs, "now");

			foreach (Event evt in events.Keys)
				evt.InitializeFields();
			
			ArrayList ret = new ArrayList(events.Keys);
			ret.Sort();
			return ret;
		}

	}
	
	public class BillEvent : Event {
		public readonly string BillCode;
		public readonly int Session;
		public readonly BillType Type;
		public readonly int Number;
		public readonly object Details;
		public readonly string statusName;

		int sponsor;
		
		public override int GetHashCode() { return Number; }
		public override bool Equals(object o) {
			if (!(o is BillEvent)) return false;
			BillEvent e = (BillEvent)o;
			return Date == e.Date && Summary == e.Summary && Session == e.Session && Type == e.Type && Number == e.Number;
		}
		
		public BillEvent(XPathNavigator data, bool indexed) {
			BillCode = data.GetAttribute("type", "") + data.GetAttribute("session", "") + "-" + data.GetAttribute("number", "");
			Session = int.Parse(data.GetAttribute("session", ""));
			Type = EnumsConv.BillTypeFromString(data.GetAttribute("type", ""));
			Number = int.Parse(data.GetAttribute("number", ""));
			
			TypeName = "Bill Action";
			Link = Bills.BillLink2(Session, Type, Number);
			
			string officialtitle;

			if (indexed) {			
				//Date = int.Parse(data.GetAttribute("lastaction", ""));
				Date = Util.DTToDateTime((string)data.Evaluate("string(*/@dateTime)"));
				Title = Util.Trunc(data.GetAttribute("title", ""), 200);
				Summary = Bills.GetStatusIndexedDetails(data.Select("."), out Details);
				officialtitle = data.GetAttribute("officialtitle", "");
			} else {
				Date = Util.DTToDateTime((string)data.Evaluate("string(actions/*[position()=last()]/@datetime)"));
				Title = Bills.DisplayString(data.Select("."), 300);
				Summary = Bills.GetStatusSourceDetails(data.Select("."), out Details);
				officialtitle = (string)data.Evaluate("string(titles/title[@type='official'][position()=last()])");
			}

			if (!Title.EndsWith(officialtitle))
				Summary += ". " + officialtitle;

			MonitorName = "this bill";
			MonitorCode = "bill:" + data.GetAttribute("type", "") + Session + "-" + Number;
		}

		public BillEvent(BillMonitor bill) 
			: this(Util.Database.DBSelectFirst("billstatus", "*",
				new Database.SpecEQ("session", bill.Session),
				new Database.SpecEQ("type", EnumsConv.BillTypeToString(bill.Type)),
				new Database.SpecEQ("number", bill.Number))) {

			MonitorName = "this bill";
			MonitorCode = bill.Encoded();
		}

		public BillEvent(TableRow billstatus) {
			BillCode = billstatus["type"].ToString() + billstatus["session"].ToString() + "-" + billstatus["number"].ToString();
			Session = (int)billstatus["session"];
			Type = EnumsConv.BillTypeFromString((string)billstatus["type"]);
			Number = (int)billstatus["number"];

			TypeName = "Bill Action";
			Link = Bills.BillLink2(Session, Type, Number);

			int titletrunc = 200;
			Title = Util.Trunc((string)billstatus["title"], titletrunc);
			Date = (DateTime)billstatus["statusdate"];

			XmlReader reader = new XmlTextReader(new StringReader((string)billstatus["statusxml"]));
			reader.Read();
			string statusName = reader.Name;

			switch (statusName) {
				case "introduced":
					Title = "Introduced: " + Title;
					if (reader.GetAttribute("sponsor") != null && reader.GetAttribute("sponsor") != "") {
						sponsor = int.Parse(reader.GetAttribute("sponsor") );
						Summary = " introduced "; // name to be added later
						if (((string)billstatus["title"]).Length < titletrunc)
							Summary += " this bill.";
						else
							Summary += " " + billstatus["title"] + (((string)billstatus["title"]).EndsWith(".") ? "" : ".");
					}
					break;
				case "calendar":
					Title = "Scheduled for Debate: " + Title;
					Summary = "This bill has been added to a schedule of legislation to be considered for debate, or has been recommended by a committee to be considered.";
					break;
				case "vote":
				case "vote2":
				case "override":
				case "conference":
				case "pingpong":
					string result, ch1, ch2;
					if (reader.GetAttribute("result") == "pass") result = "Passed"; else result = "Failed";
					if (reader.GetAttribute("where") == "h") { ch1 = "House"; ch2 = "Senate"; } else { ch1 = "Senate"; ch2 = "House"; }
					
					string mod = "";
					if (statusName == "pingpong") mod = " with Amendment";

					Title = result + " " + ch1 + mod + ": " + Title;
					if (statusName == "vote2" && result == "Passed") Title += " (Passed Both Chambers)";
					if (statusName == "override") Title = "Veto Override Attempt: " + Title;
					if (statusName == "conference") Title = "Conference Report: " + Title;

					Summary = result + " " + ch1 + mod + " ";

					if (reader.GetAttribute("how") != "roll") {
						Summary += reader.GetAttribute("how") + ".";
					} else {
						Summary += reader.GetAttribute("roll-info");
					}
					
					if (statusName == "pingpong") Summary += " Goes back to " + ch2 + ".";

					if (reader.GetAttribute("roll") != null && reader.GetAttribute("roll") != "")
						Details = new Bills.VoteDetails(
							reader.GetAttribute("where") == "h" ? Chamber.House : Chamber.Senate,
							Util.DTToDateTime((string)reader.GetAttribute("datetime")),
							int.Parse(reader.GetAttribute("roll")),
							reader.GetAttribute("result") == "pass",
							"");

					break;
				case "topresident":
					Title = "At the President's Desk: " + Title;
					Summary = "This bill has been passed by the Senate and House and now awaits the signature of the President before becoming law.";
					break;
				case "signed":
					Title = "Signed by President: " + Title;
					Summary = "The President has signed this bill, and it will shortly be enrolled as law.";
					break;
				case "veto":
					Title = "Vetoed by President: " + Title;
					Summary = "The President has vetoed this bill, sending it back to Congress for an attempt to override the veto.";
					break;
				case "enacted":
					Title = "Law Enacted: " + Title;
					Summary = "This bill has become law.";
					break;
				default: throw new InvalidOperationException(statusName + " in " + (string)billstatus["statusxml"]);
			}
			
			//Summary = Bills.GetStatusIndexedDetails(data.Select("."), out Details);

			MonitorName = "this bill";
			MonitorCode = "bill:" + (string)billstatus["type"] + Session + "-" + Number;
		}

		public override void GetPeopleIDs(ArrayList ids) {
			if (sponsor != 0) ids.Add(sponsor);
		}

		public override void InitializeFields() {
			if (sponsor != 0)
				Summary = Reps.FormatPersonName(sponsor, "now", "") + Summary;
		}
		
		protected override void LoadSpecifics(Options options) {
			if (Details is Bills.IntroducedDetails) {
				int id = ((Bills.IntroducedDetails)Details).Sponsor;
				string name = Reps.FormatPersonName(id, "now", "");
				string link = new BillMonitor(Session, Type, Number).Link();
				Specific s = new Specific(name, link, "Sponsor");
				Specifics.Add(s);
			}
			
			if (Details is Bills.VoteDetails) {
				Bills.VoteDetails vd = (Bills.VoteDetails)Details;
				Specifics.Add( new Specific("Roll Call", vd.Link(), null) );
				VoteEvent.LoadSpecifics(this, vd.ID(), options.AllMonitors);
				Image = vd.Image();
			}

			Specifics.Add(new Specific("Weigh In on POPVOX", "https://www.popvox.com/bills/us/" + Bills.GetPopvoxUrl(BillCode), "POPVOX is GovTrack's new sister-site to let you weigh in on legislation."));
		}

	}

	public class SpeechEvent : Event {
		public readonly int Session;
		public readonly string File;
		
		public SpeechEvent(XPathNavigator indexed) : this(indexed, null) {
		}

		public SpeechEvent(XPathNavigator indexed, string speakername) {
			string file = indexed.GetAttribute("file", "");
			Session = int.Parse(file.Substring(0, 3));
			File = file.Substring(4);
			
			Date = Util.DTToDateTime(indexed.GetAttribute("datetime", ""));
			if (indexed.GetAttribute("where", "") == "h")
				TypeName = "House Debate";
			else if (indexed.GetAttribute("where", "") == "s")
				TypeName = "Senate Debate";
			else
				throw new InvalidOperationException();
			Title = indexed.GetAttribute("topics", "");
			if (Title == "")
				Title = indexed.GetAttribute("title", "");
			Link = Record.Link(indexed.Select("."));
			Summary = "\"" + indexed.GetAttribute("excerpt", "").Replace("\"", "'").Trim() + "\"";
			if (speakername == null)
				Summary = "Excerpt: " + Summary;
			else
				Summary = speakername + ": " + Summary;
		}
	
		public override int GetHashCode() { return File.GetHashCode(); }
		public override bool Equals(object o) {
			if (!(o is SpeechEvent)) return false;
			SpeechEvent e = (SpeechEvent)o;
			return Session == e.Session && File == e.File;
		}

	}
	
	public class CommitteeEvent : Event {
		string key;
		string Where;
		ArrayList bills = new ArrayList();
		
		public CommitteeEvent(XPathNavigator index, Monitor.Options options) {
			if (options.MeetingsByEventDate) {
				TypeName = "Committee Meeting";
				Date = Util.DTToDateTime(index.GetAttribute("datetime", ""));
			} else {
				TypeName = "Committee Meeting Notice";
				Date = Util.DTToDateTime(index.GetAttribute("postdate", ""));
			}
			
			Where = index.GetAttribute("where", "");
			Title = index.GetAttribute("committee", "");
			Link = Subjects.CommitteeLink(Title);
			Summary = (string)index.Evaluate("string(subject)");
			
			if (options.MeetingsByEventDate) {
				if (!Summary.EndsWith(".")) Summary += ".";
				Summary += " At " + XmlConvert.ToDateTime(index.GetAttribute("datetime", "")).ToString("h':'mm tt");
			} else {
				Summary = XmlConvert.ToDateTime(index.GetAttribute("datetime", "")).ToString("ddd, MMM dd, yyyy h':'mm tt") + ". " + Summary;
			}
			
			key = index.GetAttribute("datetime", "") + ":" + Title + ":" + Summary;
			
			XPathNodeIterator bills = index.Select("bill");
			while (bills.MoveNext()) {
				this.bills.Add(new BillRef(bills));
			}

			MonitorName = "this committee";
			MonitorCode = "committee:" + Title;
		}

		protected override void LoadSpecifics(Options options) {
			foreach (BillRef billref in bills) {
				try {
					XPathNavigator bill = billref.Load();
					Specific s = new Specific(Bills.DisplayNumber(bill.Select("bill")), Bills.BillLink(bill.Select("bill")), Bills.DisplayTitle(bill.Select("bill")));
					Specifics.Add(s);
				} catch (Exception e) {
					// Bill might not be present on disk.
				}
			}
		}
	
		public override int GetHashCode() { return key.GetHashCode(); }
		public override bool Equals(object o) {
			if (!(o is CommitteeEvent)) return false;
			CommitteeEvent e = (CommitteeEvent)o;
			return key == e.key;
		}
	}
	
	public class ReportEvent : Event {
		string key;		
		
		public ReportEvent(string url, string title, string summary, DateTime date) {
			Date = date;
			Title = title;
			TypeName = "Report";
			Link = url;
			Summary = summary;
			
			key = url;
		}

		public override int GetHashCode() { return key.GetHashCode(); }
		public override bool Equals(object o) {
			if (!(o is ReportEvent)) return false;
			ReportEvent e = (ReportEvent)o;
			return key == e.key;
		}
	}

	public class QuestionAnswerEvent : Event {
		string key;		
		
		public QuestionAnswerEvent(TableRow record) {
			Monitor topic = Monitor.FromString((string)record["topic"]);
			Date = (DateTime)record["approvaldate"];

			//Link = topic.Link();
			Link = Util.UrlBase + "/users/questions.xpd?topic=" + (string)record["topic"];

			if ((int)record["question"] == -1) {
				Title = "New Question Asked";
				Summary = "A visitor asks: \"" + (string)record["text"] + "\"";
				Link += "#qa" + record["id"];
			} else {
				Title = "New Answer Posted";
				Summary = "A visitor has answered another visitors question.";
				Specifics.Add(new Specific(null, "Question", null, (string)record["reftext"], true));
				Specifics.Add(new Specific(null, "Answer", null, (string)record["text"], true));
				//Summary = "In response to the question \"" + (string)record["reftext"] + "\", a visitor responds: \"" + (string)record["text"] + "\".";
				Link += "#qa" + record["question"];
			}
			Title += " about " + topic.Display();
			
			TypeName = "Question & Answer";
			
			key = record["topic"] + "|" + record["id"];
		}

		public override int GetHashCode() { return key.GetHashCode(); }
		public override bool Equals(object o) {
			if (!(o is QuestionAnswerEvent)) return false;
			QuestionAnswerEvent e = (QuestionAnswerEvent)o;
			return key == e.key;
		}
	}

	public class VoteEvent : Event {
		string key;
		string bill, bill_title;
		string amdt, amdt_title;
		
		bool getPersonVotes = true;

		public VoteEvent(XPathNavigator index) {
			int roll = int.Parse(index.GetAttribute("roll", ""));
			string where = index.GetAttribute("where", "");
			
			Chamber chamber;
			if (where == "s" || where == "senate")
				chamber = Chamber.Senate;
			else
				chamber = Chamber.House;
				
			Date = Util.DTToDateTime(index.GetAttribute("datetime", ""));
			int year = Date.Year;
		
			TypeName = EnumsConv.ChamberNameShort(chamber) + " Vote";
			Link = Bills.VoteLink(where, year, roll);
				
			Title = index.GetAttribute("title", "");
			Summary = (index.GetAttribute("result", "") == "pass" ? "Passed" : "Failed") + " " + index.GetAttribute("counts", "") + ".";
			
			Image = Bills.GetVoteImgCarto(where, year, roll, true);

			bill = index.GetAttribute("bill", "");
			bill_title = index.GetAttribute("bill_title", "");
			amdt = index.GetAttribute("amendment", "");
			amdt_title = index.GetAttribute("amendment_title", "");
				
			key = where[0].ToString() + year + "-" + roll;

			AppendAnalysis();

			if (index.GetAttribute("type", "") == "special")
				bill = null;

			if (bill != null && bill != "") {
				MonitorName = bill_title;
				MonitorCode = "bill:" + bill;
			}
		}
	
		public VoteEvent(TableRow vote, int personId) {
			key = (string)vote["id"];
			if (vote["billtype"] != null)
				bill = vote["billtype"].ToString() + vote["billsession"].ToString() + "-" + vote["billnumber"].ToString();
			bill_title = (string)vote["title"];
			
			// amendment info was not fetched from DB
			
			Chamber chamber;
			if (key[0] == 's')
				chamber = Chamber.Senate;
			else
				chamber = Chamber.House;
				
			Date = (DateTime)vote["date"];
			TypeName = EnumsConv.ChamberNameShort(chamber) + " Vote";
			Link = Bills.VoteLink2(key);
				
			Title = (string)vote["description"];
			Summary = (string)vote["result"] + ".";
			
			Image = Bills.GetVoteImgCarto2(key, true);

			AppendAnalysis();
			
			if (personId != 0) {
				string personVote = (string)vote["displayas"];
				AddPersonVoteSpecific(this, personId, personVote);
				getPersonVotes = false;
			}

			if (bill != null && bill != "") {
				MonitorName = bill_title;
				MonitorCode = "bill:" + bill;
			}
		}
		
		private void AppendAnalysis() {
			string file = "gen.rolls-pca" + Path.DirectorySeparatorChar + key + ".txt";
			try {
				string info = Util.LoadFileString(Util.SessionFromDateTime(Date), file);
				if (info != "") Summary += " " + info;
			} catch (Exception e) { }
		}

		public override int GetHashCode() { return key.GetHashCode(); }
		public override bool Equals(object o) {
			if (!(o is VoteEvent)) return false;
			VoteEvent e = (VoteEvent)o;
			return key == e.key;
		}

		protected override void LoadSpecifics(Options options) {
			if (bill != null && bill != "") {
				BillMonitor m = new BillMonitor(bill);
				if (amdt != null && amdt != "" && amdt_title != "") {
					Specific s2 = new Specific("Amendment", Util.UrlBase + "/congress/amendment.xpd?session=" + m.Session + "&amdt=" + amdt, amdt_title);
					Specifics.Add(s2);
					Specific s = new Specific("Amends Bill", m.Link(), bill_title);
					Specifics.Add(s);
				} else {
					Specifics.Add(new Specific("Go to Bill Status", m.Link(), bill_title));
					Specifics.Add(new Specific("Weigh In on POPVOX", "https://www.popvox.com/bills/us/" + Bills.GetPopvoxUrl(bill), "POPVOX is GovTrack's new sister-site to let you weigh in on legislation."));
				}
			}
			
			if (getPersonVotes)
				LoadSpecifics(this, key, options.AllMonitors);

			string newdesc = Title, expl = null;
			Bills.TransformVoteDescription(Title, bill_title, ref newdesc, ref expl);
			Title = newdesc;
			if (expl != null)
				Summary += " (" + expl + ")";
		}
		
		public static string GetResultString(XPathNavigator vote, bool passed) {
			XPathNavigator vote2 = vote.Clone();
			vote2.MoveToFirstChild();
			
			System.Text.StringBuilder b = new System.Text.StringBuilder();
			
			b.Append(passed ? "Passed" : "Failed");
			b.Append(" ");
			b.Append(vote2.GetAttribute("aye", ""));
			b.Append("/");
			b.Append(vote2.GetAttribute("nay", ""));
			
			if (vote2.GetAttribute("nv", "") != "0") {
				b.Append(", ");
				b.Append(vote2.GetAttribute("nv", ""));
				b.Append(" not voting");
			}
			
			if (vote2.GetAttribute("present", "") != "0") {
				b.Append(", ");
				b.Append(vote2.GetAttribute("present", ""));
				b.Append(" present");
			}
			
			b.Append(".");
				
			return b.ToString();
		}
		
		public static void LoadSpecifics(Event evt, string voteid, ArrayList allmonitors) {
			ArrayList pids = new ArrayList();
			foreach (string ms in allmonitors) {
				Monitor m = Monitor.FromString(ms);
				if (m == null || !(m is PersonMonitor)) continue;
				int id = ((PersonMonitor)m).Person;
				pids.Add(id);
			}
			if (pids.Count == 0) return;

			Table votes = Util.Database.DBSelect("people_votes", "personid, displayas", new Database.SpecEQ("voteid", voteid), new Database.SpecIn("personid", pids));
			foreach (TableRow row in votes) {
				int id = (int)row["personid"];
				string cast = (string)row["displayas"];
				AddPersonVoteSpecific(evt, id, cast);
			}
		}
		
		private static void AddPersonVoteSpecific(Event evt, int id, string cast) {
			PersonMonitor m = new PersonMonitor(id);
			if (cast == "") return;
			Specific s = new Specific("vote:" + id, m.Display(), m.Link(), cast, true);
			evt.Specifics.Add(s);
		}
	}

	public class VideoEvent : Event {
		string key;
		int pid;
		
		public VideoEvent(TableRow record) {
			pid = (int)record["personid"];
			Date = (DateTime)record["date"];

			Link = (string)record["link"];

			Title = Reps.FormatPersonName(pid, "now", "") + " posts " + record["source"] + " Video: " + record["title"];
			Summary = "A new video was posted on the Member's official YouTube channel.";
			Image = (string)record["thumbnail"];
			
			TypeName = "Video";
			
			key = (string)record["link"];
		}

		public override int GetHashCode() { return key.GetHashCode(); }
		public override bool Equals(object o) {
			if (!(o is VideoEvent)) return false;
			VideoEvent e = (VideoEvent)o;
			return key == e.key;
		}

		protected override void LoadSpecifics(Options options) {
			PersonMonitor m = new PersonMonitor(pid);
			Specifics.Add(new Specific(m.Display(), m.Link(), null));
			Specifics.Add(new Specific("Watch Video", Link, null));
		}
	}

	public class InsiderEvent : Event {
		public InsiderEvent(TableRow record) {
			Title = (string)record["title"];
			Date = (DateTime)record["date"];
			string slug = (string)record["slug"];

			Link = "http://www.govtrackinsider.com/articles/" + Date.ToString("yyyy'-'MM'-'dd") + "/" + slug;
			
			TypeName = "GovTrack Insider Articles";
			Summary = "This article on GovTrack Insider is relevant to one of your trackers.";
		}

		public override int GetHashCode() { return Link.GetHashCode(); }
		public override bool Equals(object o) {
			if (!(o is InsiderEvent)) return false;
			InsiderEvent e = (InsiderEvent)o;
			return Link == e.Link;
		}
	}

}
