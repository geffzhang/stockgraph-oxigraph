namespace StockGraph.Web.Configuration;

public static class ConfigurationExtensions
{
    public static AppSettings ReadAppSettings(this IConfiguration configuration)
    {
        return new AppSettings
        {
            SourceDataPath = configuration["Data:SourcePath"] ?? "Financial-Knowledge-Graphs",
            StorePath = configuration["Data:StorePath"] ?? ".oxigraph/financial_kg",
            QueryDirectory = configuration["Data:QueryDirectory"] ?? "queries",
            GraphIri = configuration["Data:GraphIri"] ?? "https://stockgraph.local/kg/graph/main",
            DefaultMaxPriceRows = int.Parse(configuration["Limits:MaxPriceRows"] ?? "5000"),
            DefaultMaxNewsRows = int.Parse(configuration["Limits:MaxNewsRows"] ?? "1000"),
            DefaultChunkSize = int.Parse(configuration["Limits:ChunkSize"] ?? "10000"),
            DefaultMaxDaysPerStock = int.Parse(configuration["Limits:MaxDaysPerStock"] ?? "30"),
            DefaultMaxNews = int.Parse(configuration["Limits:MaxNews"] ?? "50"),
            DefaultMaxRelationships = int.Parse(configuration["Limits:MaxRelationships"] ?? "200"),
        };
    }

    public static string ResolveSourceDataPath(this AppSettings settings)
    {
        var path = settings.SourceDataPath;
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", path));
    }

    public static string ResolveQueryDirectory(this AppSettings settings)
    {
        var path = settings.QueryDirectory;
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", path));
    }
}
