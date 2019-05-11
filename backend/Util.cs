using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Web;
using System.Xml;
using System.Xml.XPath;

namespace GovTrack.Web {

	public class UserException : XPD.UserVisibleException {
		public UserException(string message) : base(message) {
		}
		public UserException(string message, Exception cause) : base(message, cause) {
		}
	}

	public class Util {
	
		struct CongressSession {
			public DateTime Start, End;
			public int Congress;
			public string Session;
		}

		public static readonly string DataPath;
		public static readonly string DataPath2;

		private const string RelativeUrlBase = "";
		private const string GlobalUrlBase = "http://www.govtrack.us";
		
		public static readonly XPathNavigator EmptyDocument = new XmlDocument().CreateNavigator();
		
		static List<CongressSession> sessions = new List<CongressSession>();
		
		public static string UrlBase {
			get {
				if (HttpContext.Current == null) return GlobalUrlBase;
				if (HttpContext.Current.Items["govtrack-UseGlobalPath"] != null) return GlobalUrlBase;
				return RelativeUrlBase;
			}
		}
		
		public static readonly XPD.Database Database;
		
		static StringDictionary StatePrefix, StateName, StateApportionment;
		
		static Util() {
			DataPath2 = "/home/govtrack/data";
			if (System.Environment.GetEnvironmentVariable("SANDBOX") != null)
				DataPath2 = "data";
			
			DataPath = Path.Combine(DataPath2, "us");
			
			if (System.Environment.GetEnvironmentVariable("SANDBOX") == null) {
				using (System.IO.TextReader reader = new System.IO.StreamReader("/home/govtrack/priv/mysqlpw")) {
					string pw = reader.ReadToEnd().Trim();
					Database = new XPD.Database("Protocol=socket;Server=localhost;Database=govtrack;User Id=govtrack;Password=" + pw);
				}
			} else {
				Database = new XPD.Database("Protocol=socket;Server=govtrack.us;Database=govtrack;User Id=govtrack_sandbox;Password=;pooling=yes");
			}
			
			InitUSHashes();
			
			string sessions = LoadFileString(0, "us/sessions.tsv");
			foreach (string line in sessions.Split('\n')) {
				if (line.Trim() == "") break;
				string[] fields = line.Split('\t');
				if (fields[0] == "congress") continue;
				CongressSession s = new CongressSession();
				s.Congress = int.Parse(fields[0]);
				s.Session = fields[1];
				s.Start = DateTime.Parse(fields[2]);
				s.End = DateTime.Parse(fields[3]);
				Util.sessions.Add(s);
			}
		}
		
		// Request Processing
		
		public static bool IsWebRequest { get { return HttpContext.Current != null; } }
		public static bool GetIsWebRequest() { return IsWebRequest; }
		
		// Global Information

		public static int CurrentSession {
			get {
				return 112;
			}
		}
		
		// Sessions
		
		public static string GetCurrentSession() {
			//return SessionFromDateTime(DateTime.Now).ToString();
			return CurrentSession.ToString();
		}
		public static string GetCurrentYear() {
			return DateTime.Now.Year.ToString();
		}
		
		public static DateTime StartOfSession(int session) {
			foreach (CongressSession s in sessions)
				if (s.Congress == session)
					return s.Start;
			throw new ArgumentException();
		}
		public static DateTime EndOfSession(int session) {
			DateTime end = DateTime.MinValue;
			foreach (CongressSession s in sessions)
				if (s.Congress == session)
					end = s.End;
			if (end == DateTime.MinValue)
				throw new ArgumentException("session=" + session);
			return end;
		}
		public static string SessionFromDT(string date) {
			return SessionFromDateTime(DTToDateTime(date)).ToString();
		}
		public static int SessionFromDateTime(DateTime date) {
			foreach (CongressSession s in sessions)
				if (s.Start <= date.Date && date.Date <= s.End+TimeSpan.FromDays(1))
					return s.Congress;
			throw new Exception("Date does not fall within a session of Congress: " + date);
		}
		public static string SubSessionFromDT(string date) {
			return SubSessionFromDateTime(DTToDateTime(date));
		}
		public static string SubSession12FromDT(string date) {
			return
				SubSessionFromDateTime(DTToDateTime(date))
				== StartOfSession(SessionFromDateTime(DTToDateTime(date))).Year.ToString()
				? "1"
				: "2";
		}
		public static string SubSessionFromDateTime(DateTime date) {
			foreach (CongressSession s in sessions)
				if (s.Start <= date.Date && date.Date <= s.End)
					return s.Session;
			throw new Exception("Date does not fall within a session of Congress: " + date);
		}
		
		// Dates
		
		public static string DTToISOString(string date) {
			return DTToDateTime(date).ToString("s");
		}
		private const string rfc822 = "ddd, dd MMM yyyy HH:mm:ss zz00";
		public static string DTToRFC822String(string date) {
			return DTToDateTime(date).ToString(rfc822);
		}
		public static string DTToICalString(string date) {
			return DTToDateTime(date).ToString("yyyyMMdd\\THHmmss");
		}
		public static string DateTimeToString(DateTime date) {
			return date.ToString("MMM d, yyyy");
		}
		public static string DateTimeToStringWithTime(DateTime date) {
			return date.ToString("MMM d, yyyy h:mmtt");
		}
		public static string DateTimeToDBString(DateTime d) {
			string r = d.Year + "-" + d.Month + "-" + d.Day;
			if (d.TimeOfDay.TotalSeconds != 0) {
				r += " " + d.Hour + ":" + d.Minute + ":" + d.Second;
			}
			return r;
		}
		public static string DTToDBString(string date) {
			return DateTimeToDBString(DTToDateTime(date));
		}

		private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1);
		public static DateTime UnixDateToDateTime(long date) {
			return UnixEpoch.AddSeconds(date).ToLocalTime();
		}
		public static long DateTimeToUnixDate(DateTime date) {
			return (long)(date.ToUniversalTime() - UnixEpoch).TotalSeconds;
		}

		public static bool IsDTInFuture(string date) {
			return DTToDateTime(date) >= DateTime.Now;
		}

		public static string NowISOString() {
			return DateTime.Now.ToString("s");
		}
		public static string TodayISOString() {
			return DateTime.Today.ToString("s");
		}
		public static string NowRFC822String() {
			return DateTime.Now.ToString(rfc822);
		}
		public static string TodayRFC822String() {
			return DateTime.Today.ToString(rfc822);
		}
		public static string DateNowString() {
			return DateTime.Now.ToString("MMMM d, yyyy");
		}

		public static string DateTimeToDT(DateTime datetime) {
			return datetime.ToString("s");
		}
		public static DateTime DTToDateTime(string dt) {
			return DateTime.Parse(dt);
		}
		public static string DTToDateString(string dt) {
			return DateTimeToString(DTToDateTime(dt));
		}
		public static string DTToDateTimeString(string dt) {
			if (dt.IndexOf("T") == -1) return DTToDateString(dt);
			return DateTimeToStringWithTime(DTToDateTime(dt));
		}
		public static string DTToYearString(string dt) {
			return DTToDateTime(dt).Year.ToString();
		}
		
		// String Functions
		
		public static string Ordinate(int number) {
			return number.ToString() + OrdinateSuffix(number);
		}
		
		public static string OrdinateSuffix(int number) {
			switch (number % 100) {
			case 11: case 12: case 13:
				return "th";
			}
		
			int mod = number % 10;
			switch (mod) {
				case 1: return "st"; 
				case 2: return "nd";
				case 3: return "rd";
			}
			
			return "th";
		}
		
		public static string Trunc(string str, int length) {
			if (str.Length <= length || length <= 3) return str;
			int i = length;
			for (i = length - 3; i > 0; i--)
				if (Char.IsWhiteSpace(str[i])) break;
			if (i == 0)
				i = length;
			return str.Substring(0, i) + "...";
		}
		
		public static object Alphabet() {
			ArrayList ret = new ArrayList();
			for (char a = 'a'; a <= 'z'; a++)
				ret.Add(a.ToString());
			return ret;
		}
		
		public static string TwoDigits(string number) {
			if (number.Length == 1) return "0" + number;
			return number;
		}
		public static string TwoDigits2(string number) {
			if (number.Length == 2 && char.IsLetter(number[number.Length-1])) return "0" + number;
			if (number.Length == 1) return "0" + number;
			return number;
		}
		
		public static string Pad(int number, int size) {
			string x = "";
			for (int i = 0; i < size; i++) x += "0";
			return number.ToString(x);
		}
		
		public static string UrlEncode(string url) {
			return System.Web.HttpUtility.UrlEncode(url);
		}

		public static string JSEncode(string s) {
			return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\\'").Replace("\n", "\\n");
		}
		
		public static string Replace(string a, string b, string c) {
			return a.Replace(b, c);
		}
		
		public static string FilterNonLetter(string s) {
			System.Text.StringBuilder r = new System.Text.StringBuilder();
			foreach (char c in s)
				if (char.IsLetter(c))
					r.Append(c);
			return r.ToString();
		}
		
		public static ArrayList Split(string s, string sep) {
			ArrayList ret = new ArrayList();
			foreach (string x in s.Split(sep[0]))
				ret.Add(x.Trim());
			return ret;
		}

		public static string RemoveHTMLTags(string input) {
			System.Text.StringBuilder output = new System.Text.StringBuilder();
			bool inTag = false;
			foreach (char c in input) {
				if (inTag && c == '>') { inTag = false; }
				else if (!inTag && c== '<') { inTag = true; }
				else if (!inTag)
					output.Append(c);
			}
			return output.ToString();
		}
		public static string MakeNiceHTML(string input) {
			System.Text.StringBuilder output = new System.Text.StringBuilder();
			bool startTag = false;
			foreach (char c in input) {
				if (c == '<') {
					output.Append(c);
					startTag = true;
				} else if (c == '&' && !startTag)
					output.Append("&amp;");
				else {
					if (startTag && c == ' ') continue;
					if (startTag && !char.IsLetter(c)) {
						output.Append("ignore/>");
					}
					output.Append(c);
					startTag = false;
				}
			}
			return output.ToString();
		}

		public static object Unique(XPathNodeIterator iter) {
			Hashtable items = new Hashtable();
			while (iter.MoveNext())
				items[iter.Current.Value] = items;
			return items.Keys;
		}

		public static string ICalFold(string field, string str) {
			str = str.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\N");
			
			int maxlen = (75 - field.Length)/2; // divide by two in case of two-byte characters
			string ret = "";
			
			while (true) {
				if (str.Length <= maxlen) {
					ret += str;
					return ret;
				} else {
					ret += str.Substring(0, maxlen) + "\n ";
					str = str.Substring(maxlen);
					maxlen = (75-1)/2;
				}
			}
			throw new Exception();
		}
		
		public static string MD5(string str) {
			byte[] original_bytes = System.Text.Encoding.UTF8.GetBytes(str);
			byte[] encoded_bytes = new System.Security.Cryptography.MD5CryptoServiceProvider().ComputeHash(original_bytes);
			System.Text.StringBuilder result = new System.Text.StringBuilder();
			for (int i = 0; i < encoded_bytes.Length; i++)
				result.Append(encoded_bytes[i].ToString("x2"));
			return result.ToString();
		}
		
		// Loading Data Files
		
		/*private static DynamicFileCache DynDataCache = new DynamicFileCache();

		private class DynamicFileCache : XPD.DynamicCache {
			public DynamicFileCache() : base(100) { }
			protected override object LoadItem(object key, out bool shouldCache) {
				DateTime a = DateTime.Now;
				XPathNavigator nav = new XPathDocument((string)key).CreateNavigator();
				DateTime b = DateTime.Now;
				//Console.Error.WriteLine("Loading From Disk: " + key + "\t" + (b-a));
				shouldCache = (b-a) > TimeSpan.FromMilliseconds(50);
				return nav;
			}
			protected override object ReturnItem(object key, object item) {
				return ((XPathNavigator)item).Clone();
			}
			protected override bool IsStale(object key, DateTime loaded) {
				return System.IO.File.GetLastWriteTime((string)key) > loaded;
			}
		}*/
		
		static void Mkdir(string d) {
			string p = Path.GetDirectoryName(d);
			if (p == null)
				return;
			if (!Directory.Exists(p))
				Mkdir(p);
			Directory.CreateDirectory(d);
		}
		
		public static void SandboxDownload(string filename) {
			if (System.Environment.GetEnvironmentVariable("SANDBOX") == null)
				return;
			if (File.Exists(filename))
				return;
			Console.Error.Write("SANDBOX: Downloading " + filename + "... ");
			Console.Error.Flush();
			Mkdir(Path.GetDirectoryName(filename));
			System.Net.WebClient client = new System.Net.WebClient ();
			client.Headers.Add("User-Agent", "GovTrack Sandbox");
			client.DownloadFile("http://www.govtrack.us/" + filename, filename);
			Console.Error.WriteLine("OK");
		}
				
		public static XPathNavigator LoadData(int session, string filename) {
			//try {
				string file;
				if (session != 0) file = DataPath + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + filename;
				else file = DataPath2 + Path.DirectorySeparatorChar + filename;
				SandboxDownload(file);
				//using (TextReader r = new StreamReader(file))
				//	return new XPathDocument(new XmlTextReader(r)).CreateNavigator();
				XmlDocument doc = new XmlDocument();
				doc.Load(file);
				return doc.CreateNavigator();
				//return (XPathNavigator)DynDataCache[file];
			// Can't rethrow here -- callers expect certain exception types
			/*} catch (Exception e) {
				throw new Exception("Error loading " + session + " " + filename, e);
			}*/
		}
		
		public static XPathNavigator LoadCacheData(int session, string filename) {
			if (HttpContext.Current == null)
				return LoadData(session, filename);		
		
			string file;
			if (session != 0) file = DataPath + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + filename;
			else file = DataPath2 + Path.DirectorySeparatorChar + filename;
			
			return LoadCacheFile(file);
		}	
		
		public static XPathNavigator LoadCacheFile(string file) {
			string key = "file:" + file;
			
			System.Web.Caching.Cache cache = HttpContext.Current.Cache;
		
			XPathNavigator nav = cache[key] as XPathNavigator;
			if (nav != null) return nav.Clone();
		
			try {
				SandboxDownload(file);
				using (TextReader r = new StreamReader(file))
					nav = new XPathDocument(new XmlTextReader(r)).CreateNavigator();
			} catch (ArgumentException e) {
				throw new ArgumentException("Error loading " + file, e);
			}
			cache.Insert(key, nav, new System.Web.Caching.CacheDependency(file));
			Console.Error.WriteLine("Caching: " + key);
						
			return nav.Clone();
		}
		
		public static string LoadFileString(int session, string filename) {
			string file;
			if (session != 0) file = DataPath + Path.DirectorySeparatorChar + session + Path.DirectorySeparatorChar + filename;
			else file = DataPath2 + Path.DirectorySeparatorChar + filename;
			try {
			SandboxDownload(file);
			using (System.IO.TextReader reader = new System.IO.StreamReader(file)) {
				return reader.ReadToEnd();
			}
			} catch (System.IO.FileNotFoundException e) { throw new System.IO.FileNotFoundException(file, e); }
		}

		public static string LoadFileStringPath(string filename) {
			try {
			using (System.IO.TextReader reader = new System.IO.StreamReader(filename)) {
				return reader.ReadToEnd();
			}
			} catch (System.IO.FileNotFoundException e) { throw new System.IO.FileNotFoundException(filename, e); }
		}
		
		// Functions for making cached calls to remote REST APIs

		public static object CallAPI(string url, string format, long minutes) {
		/*	XmlDocument d = new XmlDocument();
			d.LoadXml("<error/>");
			d.DocumentElement.SetAttribute("message", "External data is not available.");
			return d.CreateNavigator();
		}
		
		public static object CallAPIDisabled(string url, string format, long minutes) {
		*/
			XPD.TableRow cached = null;
			if (System.Environment.GetEnvironmentVariable("SANDBOX") == null)
				cached = Database.DBSelectFirst("remotecallcache", "retrieved, response, error", new XPD.Database.SpecEQ("url", url));
			
			bool okcache = cached != null && (minutes == -1 || (DateTime.Now - (DateTime)cached["retrieved"]).TotalMinutes <= minutes);

			if (okcache && cached["error"] == null) {
				byte[] data = (byte[])cached["response"];
				
				if (format == "text")
					return System.Text.Encoding.UTF8.GetString(data);
				
				try {
					return new XPathDocument(new MemoryStream(data)).CreateNavigator();
				} catch (Exception e) {
					// Error parsing something already cached but not flagged.

					Hashtable rec = new Hashtable();
					rec["error"] = e.Message;
					Database.DBUpdate("remotecallcache", rec, new XPD.Database.SpecEQ("url", url));
				
					XmlDocument d = new XmlDocument();
					d.LoadXml("<error/>");
					d.DocumentElement.SetAttribute("message", e.Message);
					return d.CreateNavigator();
				}
			} else if (okcache && cached["error"] != null) {
				XmlDocument d = new XmlDocument();
				d.LoadXml("<error/>");
				d.DocumentElement.SetAttribute("message", (string)cached["error"]);
				return d.CreateNavigator();
			} else {
				if (System.Environment.GetEnvironmentVariable("SANDBOX") == null)
					Database.DBDelete("remotecallcache", new XPD.Database.SpecEQ("url", url));

				//Console.Error.WriteLine("API(" + url + ")");
				WebResponse resp = null;
				Stream sret = null;

				try {
				try {
					HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
					req.Timeout = 2000; // two seconds
					resp = req.GetResponse();
					sret = resp.GetResponseStream();
				} catch (System.Net.WebException we) {
					Console.Error.WriteLine(we.Message + ": " + url);
					
					Hashtable rec2 = new Hashtable();
					rec2["url"] = url;
					rec2["retrieved"] = DateTime.Now;
					rec2["response"] = new byte[0];
					rec2["error"] = we.Message;
					if (System.Environment.GetEnvironmentVariable("SANDBOX") == null)
						Database.DBInsert("remotecallcache", rec2);
					
					XmlDocument d = new XmlDocument();
					d.LoadXml("<error/>");
					d.DocumentElement.SetAttribute("message", we.Message);
					return d.CreateNavigator();					
				}
				
				byte[] buf = new byte[1024];
				MemoryStream stream = new MemoryStream();
				int read;
				while ((read = sret.Read(buf, 0, 1024)) != 0)
					stream.Write(buf, 0, read);
				
				byte[] data = stream.ToArray();
				
				Hashtable rec = new Hashtable();
				rec["url"] = url;
				rec["retrieved"] = DateTime.Now;
				rec["response"] = data;
				
				XPathNavigator ret = null;
				if (format == "xml")
				try {
					ret = new XPathDocument(new MemoryStream(data)).CreateNavigator();
				} catch (Exception e) {
					rec["error"] = e.Message;
				}

				if (System.Environment.GetEnvironmentVariable("SANDBOX") == null)
					Database.DBInsert("remotecallcache", rec);
				
				if (format == "text")
					return System.Text.Encoding.UTF8.GetString(data);
				
				if (rec["error"] == null) {
					return ret;
				} else {
					XmlDocument d = new XmlDocument();
					d.LoadXml("<error/>");
					d.DocumentElement.SetAttribute("message", (string)rec["error"]);
					return d.CreateNavigator();
				}

				} finally {
					if (sret != null) sret.Close();
					if (resp != null) resp.Close();
				}
				
			}
		}
		
		// United States Information
		
		private static void InitUSHashes() {
			StatePrefix = new StringDictionary();		
		    StatePrefix["ALABAMA"] = "AL";
		    StatePrefix["ALASKA"] = "AK";
		    StatePrefix["AMERICAN SAMOA"] = "AS";
		    StatePrefix["ARIZONA"] = "AZ";
		    StatePrefix["ARKANSAS"] = "AR";
		    StatePrefix["CALIFORNIA"] = "CA";
		    StatePrefix["COLORADO"] = "CO";
		    StatePrefix["CONNECTICUT"] = "CT";
		    StatePrefix["DELAWARE"] = "DE";
		    StatePrefix["DISTRICT OF COLUMBIA"] = "DC";
		    //StatePrefix["FEDERATED STATES OF MICRONESIA"] = "FM";
		    StatePrefix["FLORIDA"] = "FL";
		    StatePrefix["GEORGIA"] = "GA";
		    StatePrefix["GUAM"] = "GU";
		    StatePrefix["HAWAII"] = "HI";
		    StatePrefix["IDAHO"] = "ID";
		    StatePrefix["ILLINOIS"] = "IL";
		    StatePrefix["INDIANA"] = "IN";
		    StatePrefix["IOWA"] = "IA";
		    StatePrefix["KANSAS"] = "KS";
		    StatePrefix["KENTUCKY"] = "KY";
		    StatePrefix["LOUISIANA"] = "LA";
		    StatePrefix["MAINE"] = "ME";
		    //StatePrefix["MARSHALL ISLANDS"] = "MH";
		    StatePrefix["MARYLAND"] = "MD";
		    StatePrefix["MASSACHUSETTS"] = "MA";
		    StatePrefix["MICHIGAN"] = "MI";
		    StatePrefix["MINNESOTA"] = "MN";
		    StatePrefix["MISSISSIPPI"] = "MS";
		    StatePrefix["MISSOURI"] = "MO";
		    StatePrefix["MONTANA"] = "MT";
		    StatePrefix["NEBRASKA"] = "NE";
		    StatePrefix["NEVADA"] = "NV";
		    StatePrefix["NEW HAMPSHIRE"] = "NH";
		    StatePrefix["NEW JERSEY"] = "NJ";
		    StatePrefix["NEW MEXICO"] = "NM";
		    StatePrefix["NEW YORK"] = "NY";
		    StatePrefix["NORTH CAROLINA"] = "NC";
		    StatePrefix["NORTH DAKOTA"] = "ND";
		    StatePrefix["NORTHERN MARIANA ISLANDS"] = "MP";
		    StatePrefix["OHIO"] = "OH";
		    StatePrefix["OKLAHOMA"] = "OK";
		    StatePrefix["OREGON"] = "OR";
		    //StatePrefix["PALAU"] = "PW";
		    StatePrefix["PENNSYLVANIA"] = "PA";
		    StatePrefix["PUERTO RICO"] = "PR";
		    StatePrefix["RHODE ISLAND"] = "RI";
		    StatePrefix["SOUTH CAROLINA"] = "SC";
		    StatePrefix["SOUTH DAKOTA"] = "SD";
		    StatePrefix["TENNESSEE"] = "TN";
		    StatePrefix["TEXAS"] = "TX";
		    StatePrefix["UTAH"] = "UT";
		    StatePrefix["VERMONT"] = "VT";
		    StatePrefix["VIRGIN ISLANDS"] = "VI"; 
		    StatePrefix["VIRGINIA"] = "VA";
		    StatePrefix["WASHINGTON"] = "WA";
		    StatePrefix["WEST VIRGINIA"] = "WV";
		    StatePrefix["WISCONSIN"] = "WI";
		    StatePrefix["WYOMING"] = "WY";
		    
		    StateName = new StringDictionary();
			StateName["AL"] = "Alabama";
			StateName["AK"] = "Alaska";
			StateName["AS"] = "American Samoa";
			StateName["AZ"] = "Arizona";
			StateName["AR"] = "Arkansas";
			StateName["CA"] = "California";
			StateName["CO"] = "Colorado";
			StateName["CT"] = "Connecticut";
			StateName["DE"] = "Delaware";
			StateName["DC"] = "District of Columbia";
			//StateName["FM"] = "Federated States of Micronesia";
			StateName["FL"] = "Florida";
			StateName["GA"] = "Georgia";
			StateName["GU"] = "Guam";
			StateName["HI"] = "Hawaii";
			StateName["ID"] = "Idaho";
			StateName["IL"] = "Illinois";
			StateName["IN"] = "Indiana";
			StateName["IA"] = "Iowa";
			StateName["KS"] = "Kansas";
			StateName["KY"] = "Kentucky";
			StateName["LA"] = "Louisiana";
			StateName["ME"] = "Maine";
			//StateName["MH"] = "Marshall Islands";
			StateName["MD"] = "Maryland";
			StateName["MA"] = "Massachusetts";
			StateName["MI"] = "Michigan";
			StateName["MN"] = "Minnesota";
			StateName["MS"] = "Mississippi";
			StateName["MO"] = "Missouri";
			StateName["MT"] = "Montana";
			StateName["NE"] = "Nebraska";
			StateName["NV"] = "Nevada";
			StateName["NH"] = "New Hampshire";
			StateName["NJ"] = "New Jersey";
			StateName["NM"] = "New Mexico";
			StateName["NY"] = "New York";
			StateName["NC"] = "North Carolina";
			StateName["ND"] = "North Dakota";
			StateName["MP"] = "Northern Mariana Islands";
			StateName["OH"] = "Ohio";
			StateName["OK"] = "Oklahoma";
			StateName["OR"] = "Oregon";
			//StateName["PW"] = "Palau";
			StateName["PA"] = "Pennsylvania";
			StateName["PR"] = "Puerto Rico";
			StateName["RI"] = "Rhode Island";
			StateName["SC"] = "South Carolina";
			StateName["SD"] = "South Dakota";
			StateName["TN"] = "Tennessee";
			StateName["TX"] = "Texas";
			StateName["UT"] = "Utah";
			StateName["VT"] = "Vermont";
			StateName["VI"] = "Virgin Islands";
			StateName["VA"] = "Virginia";
			StateName["WA"] = "Washington";
			StateName["WV"] = "West Virginia";
			StateName["WI"] = "Wisconsin";
			StateName["WY"] = "Wyoming";
			
			StateApportionment = new StringDictionary();
			StateApportionment["AL"] = "7";
			StateApportionment["AK"] = "1";
			StateApportionment["AS"] = "delegate";
			StateApportionment["AZ"] = "8";
			StateApportionment["AR"] = "4";
			StateApportionment["CA"] = "53";
			StateApportionment["CO"] = "7";
			StateApportionment["CT"] = "5";
			StateApportionment["DE"] = "1";
			StateApportionment["DC"] = "delegate";
			//StateApportionment["FM"] = "Federated States of Micronesia";
			StateApportionment["FL"] = "25";
			StateApportionment["GA"] = "13";
			StateApportionment["GU"] = "delegate";
			StateApportionment["HI"] = "2";
			StateApportionment["ID"] = "2";
			StateApportionment["IL"] = "19";
			StateApportionment["IN"] = "9";
			StateApportionment["IA"] = "5";
			StateApportionment["KS"] = "4";
			StateApportionment["KY"] = "6";
			StateApportionment["LA"] = "7";
			StateApportionment["ME"] = "2";
			//StateApportionment["MH"] = "Marshall Islands";
			StateApportionment["MD"] = "8";
			StateApportionment["MA"] = "10";
			StateApportionment["MI"] = "15";
			StateApportionment["MN"] = "8";
			StateApportionment["MS"] = "4";
			StateApportionment["MO"] = "9";
			StateApportionment["MT"] = "1";
			StateApportionment["NE"] = "3";
			StateApportionment["NV"] = "3";
			StateApportionment["NH"] = "2";
			StateApportionment["NJ"] = "13";
			StateApportionment["NM"] = "3";
			StateApportionment["NY"] = "29";
			StateApportionment["NC"] = "13";
			StateApportionment["ND"] = "1";
			StateApportionment["MP"] = "delegate";
			StateApportionment["OH"] = "18";
			StateApportionment["OK"] = "5";
			StateApportionment["OR"] = "5";
			//StateApportionment["PW"] = "Palau";
			StateApportionment["PA"] = "19";
			StateApportionment["PR"] = "delegate";
			StateApportionment["RI"] = "2";
			StateApportionment["SC"] = "6";
			StateApportionment["SD"] = "1";
			StateApportionment["TN"] = "9";
			StateApportionment["TX"] = "32";
			StateApportionment["UT"] = "3";
			StateApportionment["VT"] = "1";
			StateApportionment["VI"] = "delegate";
			StateApportionment["VA"] = "11";
			StateApportionment["WA"] = "9";
			StateApportionment["WV"] = "3";
			StateApportionment["WI"] = "8";
			StateApportionment["WY"] = "1";
   		}
   		
   		public static string GetStateAbbr(string state) {
   			string ret = (string)StatePrefix[state.ToUpper()];
   			if (ret == null) throw new ArgumentException("Invalid state name: " + state);
   			return ret;
   		}
   		
   		public static string GetStateName(string abbr) {
   			if (abbr == "OL") return "Territory of Orleans"; // What to do with historical stuff?
   			if (abbr == "DK") return "Territory of Dakota"; // What to do with historical stuff?
   			if (abbr == "PI") return "Philippine Islands"; // What to do with historical stuff?
   			string ret = (string)StateName[abbr];
   			if (ret == null) throw new ArgumentException("Invalid state abbreviation: " + abbr);
   			return ret;
   		}

   		public static string GetStateApportionment(string abbr) {
   			string ret = (string)StateApportionment[abbr];
   			if (ret == null) throw new ArgumentException("Invalid state abbreviation: " + abbr);
   			return ret;
   		}
   		
   		public static bool IsValidStateAbbr(string state) {
   			return StateName.ContainsKey(state.ToLower());
   		}
   		public static bool IsValidStateName(string state) {
   			return StatePrefix.ContainsKey(state.ToLower());
   		}
   		
   		public static object StateNames() {
   			return StateName.Values;
   		}
   		
   		public static string ToUpper(string str) {
   			return str.ToUpper();
   		}
   		public static string ToLower(string str) {
   			return str.ToLower();
   		}
		
		public static string Normalize(string s) {
			System.Text.StringBuilder b = new System.Text.StringBuilder();
			foreach (char c in s)
				if (char.IsLetter(c))
					b.Append(c);
			return b.ToString();
		}
		
		public static ArrayList range(int start, int end) {
			ArrayList ret = new ArrayList();
			for (int i = start; i <= end; i++)
				ret.Add(i);
			return ret;
		}
	}

}
