using System;
using System.Collections;
using System.Xml;
using System.Xml.XPath;
using System.Web;

using GovTrack.Enums;
using XPD;

namespace GovTrack.Web {

	public class Comments {

		public static Table GetQuestions(string topic) {
			if (topic != "")
				return Util.Database.DBSelect("questions", "id, submissiondate as date, text",
					new Database.SpecEQ("question", -1),
					new Database.SpecEQ("topic", topic),
					new Database.SpecEQ("status", "approved"),
					new Database.SpecOrder("approvaldate", true));
			else
				return Util.Database.DBSelect("questions", "id, submissiondate as date, text, topic",
					new Database.SpecEQ("question", -1),
					new Database.SpecEQ("status", "approved"),
					new Database.SpecOrder("approvaldate", false),
					new Database.SpecLimit(20));
		}

		public static void AddQuestion(string topic, string question) {
			Hashtable rec = new Hashtable();
			rec["topic"] = topic;
			rec["question"] = -1;
			rec["text"] = question;
			rec["submissiondate"] = DateTime.Now;
			rec["ipaddr"] = HttpContext.Current.Request.UserHostAddress;
			Util.Database.DBInsert("questions", rec);
		}

		public static void AddAnswer(int question, string answer) {
			TableRow q = Util.Database.DBSelectFirst("questions", "topic",
				new Database.SpecEQ("question", -1),
				new Database.SpecEQ("id", question));
			if (q == null) return;

			Hashtable rec = new Hashtable();
			rec["topic"] = q["topic"];
			rec["question"] = question;
			rec["text"] = answer;
			rec["submissiondate"] = DateTime.Now;
			rec["ipaddr"] = HttpContext.Current.Request.UserHostAddress;
			Util.Database.DBInsert("questions", rec);
		}

		public static Table GetAnswers(int question) {
			return Util.Database.DBSelect("questions", "id, submissiondate as date, text",
				new Database.SpecEQ("question", question),
				new Database.SpecEQ("status", "approved"),
				new Database.SpecOrder("approvaldate", true));
		}

		public static System.Xml.XPath.XPathNavigator GetPageNote(string monitorid) {
			TableRow row = Util.Database.DBSelectFirst("notes", "xhtml",
				new Database.SpecEQ("pageid", monitorid));
			XmlDocument doc = new XmlDocument();
			if (row == null) {
				doc.LoadXml("<empty/>");
			} else {
				string s = (string)row["xhtml"];
				if (!s.StartsWith("<"))
					s = "<document>" + s + "</document>";
				doc.LoadXml(s);
			}
			return doc.CreateNavigator();
		}

		public static string GetPageAltTitle(string monitorid) {
			TableRow row = Util.Database.DBSelectFirst("notes", "alttitle",
				new Database.SpecEQ("pageid", monitorid));
			if (row == null) return "";
			string ret = (string)row["alttitle"];
			if (ret == null) ret = "";
			return ret;
		}
	}
}
