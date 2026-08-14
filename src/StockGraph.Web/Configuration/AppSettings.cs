namespace StockGraph.Web.Configuration;

public class AppSettings
{
    public string SourceDataPath { get; set; } = "Financial-Knowledge-Graphs";
    public string StorePath { get; set; } = ".oxigraph/financial_kg";
    public string QueryDirectory { get; set; } = "queries";
    public string GraphIri { get; set; } = "https://stockgraph.local/kg/graph/main";
    public int DefaultMaxPriceRows { get; set; } = 5000;
    public int DefaultMaxNewsRows { get; set; } = 1000;
    public int DefaultChunkSize { get; set; } = 10_000;
    public int DefaultMaxDaysPerStock { get; set; } = 30;
    public int DefaultMaxNews { get; set; } = 50;
    public int DefaultMaxRelationships { get; set; } = 200;
}
