using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;

using Lucene.Net.Index;
using Lucene.Net.Documents;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Search;
using Lucene.Net.QueryParsers;

public class Indexer {
	public static void Main(string[] args) {
		if (args[0] == "BILL") {
			IndexBill(args[1], null);
		} else {
			string basepath = args[0];
			IndexBills(basepath);
			//IndexRecord(basepath);
		}
	}
	
	static void IndexRecord(string basepath) {
		IndexWriter writer = new IndexWriter(
			basepath + "/index.cr.lucene-new",
			new StandardAnalyzer(), true);
			
		foreach (string crfile in Directory.GetFiles(basepath + "/cr")) {
			XmlElement cr = Load(crfile);
			
			{
				Document doc = new Document();
				doc.Add(FieldKeyword("where", cr.GetAttribute("where")));
				doc.Add(FieldKeyword("when", cr.GetAttribute("when")));
				doc.Add(FieldKeyword("datetime", cr.GetAttribute("datetime")));
				doc.Add(FieldKeyword("ordinal", cr.GetAttribute("ordinal")));
				doc.Add(FieldText("title", cr.GetAttribute("title")));
				doc.Add(FieldKeyword("type", "section"));
				string text = "";
				foreach (XmlElement graph in cr.SelectNodes("*/paragraph"))
					text += graph.InnerText + " \n";
				doc.Add(FieldUnStored("text", cr.GetAttribute("title") + "\n" + text));
				doc.Add(FieldUnIndexed("excerpt", Trim(text, 300)));
				writer.AddDocument(doc);
			}
			
			foreach (XmlElement speaking in cr.SelectNodes("speaking")) {
				string text = "";
				foreach (XmlElement graph in speaking.SelectNodes("paragraph"))
					text += graph.InnerText + " \n";
				if (text.Length < 500)
					continue;

				Document doc = new Document();
				doc.Add(FieldKeyword("where", cr.GetAttribute("where")));
				doc.Add(FieldKeyword("when", cr.GetAttribute("when")));
				doc.Add(FieldKeyword("datetime", cr.GetAttribute("datetime")));
				doc.Add(FieldKeyword("ordinal", cr.GetAttribute("ordinal")));
				doc.Add(FieldKeyword("title", cr.GetAttribute("title")));
				doc.Add(FieldKeyword("type", "speaker"));
				doc.Add(FieldKeyword("speaker", speaking.GetAttribute("speaker")));
				doc.Add(FieldUnStored("text", text));
				doc.Add(FieldUnIndexed("excerpt", Trim(text, 300)));
				writer.AddDocument(doc);
			}
		}
		
		writer.Optimize();
		writer.Close();
	}
	
	static void IndexBills(string basepath) {
		IndexWriter writer = new IndexWriter(
			basepath + "/index.bills.lucene-new",
			new StandardAnalyzer(), true);
		
		foreach (string billfile in Directory.GetFiles(basepath + "/bills")) {
			try {
				IndexBill(billfile, writer);
			} catch (Exception e) {
				Console.WriteLine(billfile + ": " + e.Message);
			}
		}
		
		writer.Optimize();
		writer.Close();
	}
	
	static void IndexBill(string file, IndexWriter writer) {
			XmlElement bill = Load(file);

			Document doc = new Document();
			doc.Add(FieldKeyword("session", bill.GetAttribute("session")));
			doc.Add(FieldKeyword("type", bill.GetAttribute("type")));
			doc.Add(FieldKeyword("number", bill.GetAttribute("number")));

			doc.Add(FieldKeyword("status", bill.SelectSingleNode("status/*").Name));
			doc.Add(FieldKeyword("state", bill.SelectSingleNode("state").InnerText));

			doc.Add(FieldKeyword("introduced", bill.SelectSingleNode("introduced/@datetime").Value));
			
			XmlNode la = bill.SelectSingleNode("actions/*[position()=last()]/@datetime");
			if (la == null) la = bill.SelectSingleNode("introduced/@datetime");
			doc.Add(FieldKeyword("lastaction", la.Value));

			string officialtitles = "", shorttitles = "";
			foreach (XmlElement title in bill.SelectNodes("titles/title")) {
				officialtitles += title.InnerText + "\n";
				if (title.GetAttribute("type") != "official")
					shorttitles += title.InnerText + "\n";
			}

			doc.Add(FieldUnStored("shorttitles", shorttitles));
			doc.Add(FieldUnStored("officialtitles", officialtitles));

			foreach (XmlAttribute sponsor in bill.SelectNodes("sponsor/@id")) {
				doc.Add(FieldKeyword("sponsor", sponsor.Value));
			}
			foreach (XmlAttribute sponsor in bill.SelectNodes("cosponsors/cosponsor/@id")) {
				doc.Add(FieldKeyword("cosponsor", sponsor.Value));
			}

			doc.Add(FieldUnStored("summary", bill.SelectSingleNode("summary").InnerText));

			string fulltextfile = "/home/govtrack/data/us/bills.text/" + bill.GetAttribute("session") + "/" + bill.GetAttribute("type") + "/" + bill.GetAttribute("type") + bill.GetAttribute("number") + ".html";
			if (File.Exists(fulltextfile)) {
				XmlDocument fulltext = new XmlDocument();
				fulltext.Load(fulltextfile);
				doc.Add(FieldUnStored("fulltext", fulltext.InnerText));
			} else {
				Console.WriteLine("Missing full text: " + fulltextfile);
			}
			
			if (writer != null)
				writer.AddDocument(doc);
			else
				Console.WriteLine(doc);
	}

	static XmlElement Load(string file) {
		XmlDocument doc = new XmlDocument();
		doc.Load(file);
		return doc.DocumentElement;
	}
	
	static string Trim(string text, int length) {
		text = text.Replace("\n", "");
		if (text.Length <= length) return text;
		return text.Substring(0, length) + "...";
	}

	static Field FieldText(string name, string value) {
		return new Field(name, value, Field.Store.YES, Field.Index.TOKENIZED);
	}
	static Field FieldKeyword(string name, string value) {
		return new Field(name, value, Field.Store.YES, Field.Index.UN_TOKENIZED);
	}
	static Field FieldUnIndexed(string name, string value) {
		return new Field(name, value, Field.Store.YES, Field.Index.NO);
	}
	static Field FieldUnStored(string name, string value) {
		return new Field(name, value, Field.Store.NO, Field.Index.TOKENIZED);
	}
}


