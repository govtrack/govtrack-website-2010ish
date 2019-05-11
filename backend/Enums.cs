using System;

namespace GovTrack.Enums {

	public enum Chamber {
		House,
		Senate
	}

	public enum BillType {
		H,
		HR,
		HJ,
		HC,
		S,
		SR,
		SJ,
		SC
	}

	public enum BillStatus {
		Introduced,
		Calendar,
		Vote,
		Vote2,
		Conference,
		ToPresident,
		Signed,
		Veto,
		Override,
		Enacted
	}
	
	public class EnumsUtil {
		public static Chamber BillTypeChamber(BillType type) {
			switch (type) {
			case BillType.H: return Chamber.House;
			case BillType.HR: return Chamber.House;
			case BillType.HJ: return Chamber.House;
			case BillType.HC: return Chamber.House;
			case BillType.S: return Chamber.Senate;
			case BillType.SR: return Chamber.Senate;
			case BillType.SJ: return Chamber.Senate;
			case BillType.SC: return Chamber.Senate;
			}
			throw new InvalidOperationException();
		}
		
		public static Chamber Other(Chamber chamber) {
			if (chamber == Chamber.Senate) return Chamber.House;
			return Chamber.Senate;
		}
	}

	public class EnumsConv {
	
		public static BillType BillTypeFromString(string type) {
			switch (type) {
			case "h": return BillType.H;
			case "hr": return BillType.HR;
			case "hj": return BillType.HJ;
			case "hc": return BillType.HC;
			case "s": return BillType.S;
			case "sr": return BillType.SR;
			case "sj": return BillType.SJ;
			case "sc": return BillType.SC;
			}
			throw new ArgumentException("Invalid bill type:" + type);
		}	

		public static string BillTypeToString(BillType type) {
			switch (type) {
			case BillType.H: return "h";
			case BillType.HR: return "hr";
			case BillType.HJ: return "hj";
			case BillType.HC: return "hc";
			case BillType.S: return "s";
			case BillType.SR: return "sr";
			case BillType.SJ: return "sj";
			case BillType.SC: return "sc";
			}
			throw new InvalidOperationException();
		}	

		public static string BillTypeToDisplayString(BillType type) {
			switch (type) {
			case BillType.H: return "H.R.";
			case BillType.HR: return "H. Res.";
			case BillType.HJ: return "H. J. Res.";
			case BillType.HC: return "H. Con. Res.";
			case BillType.S: return "S.";
			case BillType.SR: return "S. Res.";
			case BillType.SJ: return "S. J. Res.";
			case BillType.SC: return "S. Con. Res.";
			}
			throw new InvalidOperationException();
		}	

		public static BillStatus BillStatusFromString(string status) {
			switch (status) {
			case "introduced": return BillStatus.Introduced;
			case "calendar": return BillStatus.Calendar;
			case "vote": return BillStatus.Vote;
			case "vote2": return BillStatus.Vote2;
			case "conference": return BillStatus.Conference;
			case "topresident": return BillStatus.ToPresident;
			case "signed": return BillStatus.Signed;
			case "veto": return BillStatus.Veto;
			case "override": return BillStatus.Override;
			case "enacted": return BillStatus.Enacted;
			}
			throw new ArgumentException("Invalid bill status: " + status);
		}
		
		public static string BillStatusToString(BillStatus status) {
			switch (status) {
			case BillStatus.Introduced: return "introduced";
			case BillStatus.Calendar: return "calendar";
			case BillStatus.Vote: return "vote";
			case BillStatus.Vote2: return "vote2";
			case BillStatus.Conference: return "conference";
			case BillStatus.ToPresident: return "topresident";
			case BillStatus.Signed: return "signed";
			case BillStatus.Veto: return "veto";
			case BillStatus.Override: return "override";
			case BillStatus.Enacted: return "enacted";
			}
			throw new ArgumentException("Invalid bill status: " + status);
		}
		
		public static string BillStatusToDisplayString(BillStatus status) {
			switch (status) {
			case BillStatus.Introduced: return "Introduced";
			case BillStatus.Calendar: return "Reported by Committee";
			case BillStatus.Vote: return "Voted on in Originating Chamber";
			case BillStatus.Vote2: return "Voted on in Both Chambers";
			case BillStatus.Conference: return "Resolving Differences";
			case BillStatus.ToPresident: return "Awaiting President's Signature";
			case BillStatus.Signed: return "Signed by The President";
			case BillStatus.Veto: return "Vetoed by The President";
			case BillStatus.Override: return "Veto Override Attempt";
			case BillStatus.Enacted: return "Enacted";
			}
			throw new ArgumentException("Invalid bill status: " + status);
		}
		
		public static string ChamberNameShort(Chamber chamber) {
			if (chamber == Chamber.House) return "House";
			return "Senate";
		}
		public static string ChamberNameLong(Chamber chamber) {
			if (chamber == Chamber.House) return "House of Representative";
			return "Senate";
		}
	}
}
