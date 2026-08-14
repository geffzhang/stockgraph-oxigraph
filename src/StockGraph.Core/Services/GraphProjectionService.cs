using Oxigraph;
using StockGraph.Core.Models;

namespace StockGraph.Core.Services;

public class GraphProjectionService
{
    private readonly OxigraphStoreCoordinator _coordinator;

    private static readonly string PrefixBlock =
        "PREFIX ex: <https://stockgraph.local/kg/>\n" +
        "PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>\n" +
        "PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>\n";

    public GraphProjectionService(OxigraphStoreCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public G6Projection Project(int maxDaysPerStock = 30, int maxNews = 50, int maxRelationships = 200)
    {
        var nodes = new List<G6Node>();
        var edges = new List<G6Edge>();
        var nodeTypeCounts = new Dictionary<string, int>();
        var seenNodeIds = new HashSet<string>();

        // 获取股票节点
        var stocksQuery = PrefixBlock + @"
SELECT ?stock ?label ?exchange WHERE {
  ?stock a ex:Stock .
  OPTIONAL { ?stock rdfs:label ?label }
  OPTIONAL { ?stock ex:exchange ?exchange }
}
LIMIT 50";
        var stocksResult = _coordinator.ExecuteQuery(stocksQuery);

        if (stocksResult is QuerySolutions stockSet)
        {
            foreach (var row in stockSet)
            {
                var stockUri = TermToString(row["stock"]);
                var label = TermToString(row["label"]) ?? stockUri.Split('/').Last();
                var exchange = TermToString(row["exchange"]) ?? "";

                if (seenNodeIds.Add(stockUri))
                {
                    nodes.Add(new G6Node(stockUri, label, "Stock",
                        new Dictionary<string, object> { ["exchange"] = exchange }));
                    nodeTypeCounts["Stock"] = nodeTypeCounts.GetValueOrDefault("Stock", 0) + 1;
                }

                // 获取交易日
                var daysQuery = PrefixBlock + $@"
SELECT ?day ?tradeDate WHERE {{
  <{stockUri}> ex:hasTradingDay ?day .
  ?day ex:tradeDate ?tradeDate .
}}
ORDER BY DESC(?tradeDate)
LIMIT {maxDaysPerStock}";
                var daysResult = _coordinator.ExecuteQuery(daysQuery);

                if (daysResult is QuerySolutions daySet)
                {
                    foreach (var dayRow in daySet)
                    {
                        var dayUri = TermToString(dayRow["day"]);
                        var date = TermToString(dayRow["tradeDate"]) ?? "";
                        if (seenNodeIds.Add(dayUri))
                        {
                            nodes.Add(new G6Node(dayUri, date, "TradingDay",
                                new Dictionary<string, object> { ["date"] = date }));
                            nodeTypeCounts["TradingDay"] = nodeTypeCounts.GetValueOrDefault("TradingDay", 0) + 1;
                        }
                        if (edges.Count < maxRelationships)
                            edges.Add(new G6Edge(stockUri, dayUri, "hasTradingDay", "hasTradingDay"));
                    }
                }
            }
        }

        // 新闻节点
        var newsQuery = PrefixBlock + $@"
SELECT ?news ?label WHERE {{
  ?news a ex:NewsArticle .
  OPTIONAL {{ ?news rdfs:label ?label }}
}}
LIMIT {maxNews}";
        var newsResult = _coordinator.ExecuteQuery(newsQuery);

        if (newsResult is QuerySolutions newsSet)
        {
            foreach (var row in newsSet)
            {
                var newsUri = TermToString(row["news"]);
                var label = TermToString(row["label"]) ?? newsUri.Split('/').Last();
                if (seenNodeIds.Add(newsUri))
                {
                    nodes.Add(new G6Node(newsUri,
                        label.Length > 40 ? label[..40] + "…" : label,
                        "NewsArticle",
                        new Dictionary<string, object>()));
                    nodeTypeCounts["NewsArticle"] = nodeTypeCounts.GetValueOrDefault("NewsArticle", 0) + 1;
                }
            }
        }

        return new G6Projection(nodes, edges,
            new G6Metadata(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                nodes.Count, edges.Count, nodeTypeCounts));
    }

    /// <summary>Extract the string value from an RDF term, handling NamedNode, BlankNode, and Literal types.</summary>
    private static string TermToString(ITerm? term) => term switch
    {
        NamedNode nn => nn.Value,
        BlankNode bn => bn.Value,
        Literal lit => lit.Value,
        _ => term?.ToString() ?? "",
    };
}
