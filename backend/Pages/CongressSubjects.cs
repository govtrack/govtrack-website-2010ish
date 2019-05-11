using System;
using System.Collections;
using System.IO;
using System.Xml.XPath;
using System.Web;

using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

namespace GovTrack.Web.Pages.Congress {

	public class Subjects {
		public object GetRandomCRS(int count) {
			ArrayList crs = GovTrack.Web.Subjects.GetAllSubjects();
			ArrayList ret = new ArrayList();
			Random rand = new Random();
			for (int i = 0; i < count; i++)
				ret.Add( crs[rand.Next() % crs.Count] );
			ret.Sort();
			return ret;
		}
		
		public object GetCRSTerms(string letter) {
			return GovTrack.Web.Subjects.GetSubjectsForLetter(letter);
		}

		public XPD.Table GetBills(string type, string term) {
			if (term.StartsWith(".") || term.IndexOf("/") >= 0)
				throw new UserException("An invalid subject term was specified in the URL.");

			int session = Util.CurrentSession;
			if (HttpContext.Current.Request["session"] != null)
				session = int.Parse(HttpContext.Current.Request["session"]);

			return GovTrack.Web.Subjects.LoadIndex(session, type, term);
		}
	}
	
}
