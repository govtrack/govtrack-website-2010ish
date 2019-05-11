using System;
using System.Collections;
using System.Xml.XPath;
using System.Web;

using XPD;

using GovTrack;
using GovTrack.Enums;
using GovTrack.Web;

namespace GovTrack.Web.Pages.Local {

	public class Index {
		public string GetState(string district) {
			return new DistrictMonitor(district).State;
		}
		public string GetDistrict(string district) {
			return new DistrictMonitor(district).District.ToString();
		}

		public string GetName(string district) {
			DistrictMonitor d = new DistrictMonitor(district);
			return Util.GetStateName(d.State) + "'s " + Util.Ordinate(d.District);
		}
	
		public object GetEvents(string district) {
			DateTime starttime = DateTime.Today - TimeSpan.FromDays(30);
			
			ArrayList monitors = Login.GetMonitors();
			if (monitors == null || monitors.Count == 0) {
				monitors = new ArrayList();
				monitors.Add("district:" + district);
			}
			
			Monitor.Options monoptions = new Monitor.Options();
			
			ArrayList events = Event.GetTrackedEvents(
				monitors,
				starttime,
				DateTime.Today + TimeSpan.FromDays(1),
				monoptions);
			
			Event.Options evoptions = new Event.Options();
			evoptions.AllMonitors = monitors;

			ArrayList ret = new ArrayList();
			foreach (Event e in events) {
				ret.Add( e.ToHashtable(evoptions) );
			}
				
			return ret;
		}
		
	}

}
