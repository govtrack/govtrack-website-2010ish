using System;
using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.XPath;

using XPD;

namespace GovTrack.Web {

	public class Drafts {
		public static object LoadDraft(string code) {
			TableRow row = Util.Database.DBSelectFirst("drafts", "id, status, title, description, submitdate, contenttype, scribd_api_result, submitter",
				new Database.SpecEQ("code", code));
			if (row == null)
				throw new UserVisibleException("There is no document at this address.");
				
			Hashtable vals = new Hashtable();
			vals["status"] = "verified";
			Util.Database.DBUpdate("drafts", vals, new Database.SpecEQ("code", code));
			Util.Database.DBIncrement("drafts", "views", new Database.SpecEQ("code", code));
			
			return row;
		}

		public static object Docs() {
			return Util.Database.DBSelect("drafts", "id, code, title, description, submitdate, contenttype",
				new Database.SpecEQ("status", "verified"),
				new Database.SpecOrder("submitdate", false));
		}
	}
	
}
