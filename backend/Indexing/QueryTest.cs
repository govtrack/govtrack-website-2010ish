using System;
using System.IO;
using System.Xml;

using Lucene.Net.Index;
using Lucene.Net.Documents;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Search;
using Lucene.Net.QueryParsers;

public class QueryTest {
	public static void Main(string[] args) {
		string indexpath = args[0];
		string query = args[1];
	
		IndexSearcher searcher = new IndexSearcher(indexpath);

		Query parsedquery = QueryParser.Parse(query,
			"summary", new StandardAnalyzer());
		
		Hits hits = searcher.Search(parsedquery);
		
		Console.WriteLine("Found " + hits.Length() + " document(s) that matched query '" + query + "':\n");

		for (int i = 0; i < hits.Length(); i++) {
			Document doc = hits.Doc(i);
			Console.WriteLine(hits.Score(i) + ": " + doc.Get("excerpt") + "\n");
			if (i == 50) { break; }
		}
		searcher.Close();		
	}
}


