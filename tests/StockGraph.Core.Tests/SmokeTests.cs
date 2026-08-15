using StockGraph.Core.Services;

namespace StockGraph.Core.Tests;

public class SmokeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _queryDir;
    private readonly OxigraphStoreCoordinator _coordinator;

    public SmokeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_smoke_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);
        _queryDir = Path.Combine(_tempDir, "queries");
        Directory.CreateDirectory(_queryDir);

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        File.Copy(Path.Combine(repoRoot, "Financial-Knowledge-Graphs", "data", "000001.XSHE.csv"),
            Path.Combine(dataDir, "000001.XSHE.csv"));
        File.Copy(Path.Combine(repoRoot, "Financial-Knowledge-Graphs", "data", "000063.XSHE.csv"),
            Path.Combine(dataDir, "000063.XSHE.csv"));
        File.Copy(Path.Combine(repoRoot, "Financial-Knowledge-Graphs", "data", "latest_news.csv"),
            Path.Combine(dataDir, "latest_news.csv"));

        foreach (var f in Directory.GetFiles(Path.Combine(repoRoot, "queries"), "*.sparql"))
            File.Copy(f, Path.Combine(_queryDir, Path.GetFileName(f)));

        var storePath = Path.Combine(_tempDir, ".oxigraph", "store");
        _coordinator = new OxigraphStoreCoordinator(storePath);
        _coordinator.Open();
    }

    [Fact]
    public void 完整构建和查询流程()
    {
        var builder = new FinancialGraphBuilder(_coordinator);
        var stats = builder.Build(_tempDir, clear: true, maxPriceRows: 100, maxNewsRows: 20);
        Assert.True(stats.Rows.Count >= 3);
        Assert.True(stats.Quads > 0);

        var sparql = new SparqlService(_coordinator, _queryDir);
        foreach (var queryFile in Directory.GetFiles(_queryDir, "*.sparql"))
        {
            var name = Path.GetFileName(queryFile);
            var result = sparql.ExecuteFile(name);
            Assert.NotNull(result);
        }

        var export = new RdfExportService(_coordinator);
        using var trig = export.Export(Core.Services.RdfFormat.TriG);
        using var reader = new StreamReader(trig);
        var content = reader.ReadToEnd();
        Assert.Contains("https://stockgraph.local/kg/", content);

        var proj = new GraphProjectionService(_coordinator).Project(10, 5, 50);
        Assert.True(proj.Nodes.Count > 0);
        Assert.True(proj.Metadata.TotalNodes > 0);
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static string FindRepoRoot(string startDir)
    {
        var dir = startDir;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Financial-Knowledge-Graphs", "data", "000001.XSHE.csv")))
                return dir;
            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }
        return startDir;
    }
}
