using System;
using System.Web;

namespace GovTrack.Web {

	public class AppModule : IHttpModule {
		static bool didInit = false;
	
		public AppModule() {
		}
		
		public void Init(HttpApplication app) {
			InitData(true);
		}
		
		public static void InitData(bool allowDelayedLoad) {
			lock (typeof(AppModule)) {
				if (didInit) return;
				didInit = true;
			
				try {
					if (allowDelayedLoad) Console.Error.WriteLine("Initializing GovTrack...");
					new Util(); // be sure to call static constructor from App and not in a thread worker later
					Pages.Index.Init(allowDelayedLoad);
					Reps.Init(allowDelayedLoad);
					Subjects.Init(allowDelayedLoad);
					Pages.Congress.Vote.Init(allowDelayedLoad);
					if (allowDelayedLoad) Console.Error.WriteLine("GovTrack initialization complete.");
				} catch (Exception e) {
					Console.Error.WriteLine("Fatal error initializing GovTrack:");
					Console.Error.WriteLine(e);
					Console.Error.WriteLine("Terminating.");
					System.Environment.Exit(1);
				}
			}
		}
	
		public void Dispose() {
		}
		
		public static void Run(bool delayed, System.Threading.WaitCallback func) {
			if (delayed)
				System.Threading.ThreadPool.QueueUserWorkItem(func);
			else
				func(null);
		}
	}

}
