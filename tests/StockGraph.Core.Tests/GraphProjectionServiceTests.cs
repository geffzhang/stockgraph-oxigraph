using StockGraph.Core.Services;

namespace StockGraph.Core.Tests;

public class GraphProjectionServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly GraphProjectionService _service;

    public GraphProjectionServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_g6_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        File.Copy(
            Path.Combine(repoRoot, "Financial-Knowledge-Graphs", "data", "000001.XSHE.csv"),
            Path.Combine(dataDir, "000001.XSHE.csv"));
        File.Copy(
            Path.Combine(repoRoot, "Financial-Knowledge-Graphs", "data", "latest_news.csv"),
            Path.Combine(dataDir, "latest_news.csv"));

        var storePath = Path.Combine(_tempDir, ".oxigraph", "store");
        _coordinator = new OxigraphStoreCoordinator(storePath);
        _coordinator.Open();
        var builder = new FinancialGraphBuilder(_coordinator);
        builder.Build(_tempDir, clear: true, maxPriceRows: 10, maxNewsRows: 5);
        _service = new GraphProjectionService(_coordinator);
    }

    [Fact]
    public void Project_返回股票节点()
    {
        var proj = _service.Project(maxDaysPerStock: 5, maxNews: 3, maxRelationships: 50);

        Assert.NotEmpty(proj.Nodes);
        Assert.Contains(proj.Nodes, n => n.Type == "Stock");
    }

    [Fact]
    public void Project_返回交易日节点()
    {
        var proj = _service.Project(maxDaysPerStock: 5, maxNews: 3, maxRelationships: 50);

        Assert.Contains(proj.Nodes, n => n.Type == "TradingDay");
    }

    [Fact]
    public void Project_遵守_maxDaysPerStock_限制()
    {
        var proj = _service.Project(maxDaysPerStock: 3, maxNews: 10, maxRelationships: 100);

        var stockNodes = proj.Nodes.Where(n => n.Type == "Stock").ToList();
        var dayNodes = proj.Nodes.Where(n => n.Type == "TradingDay").ToList();
        Assert.True(dayNodes.Count <= stockNodes.Count * 3);
    }

    [Fact]
    public void Project_遵守_maxRelationships_限制()
    {
        var proj = _service.Project(maxDaysPerStock: 10, maxNews: 10, maxRelationships: 5);

        Assert.True(proj.Edges.Count <= 5);
    }

    [Fact]
    public void Project_包含元数据()
    {
        var proj = _service.Project();

        Assert.True(proj.Metadata.TotalNodes > 0);
        Assert.True(proj.Metadata.GeneratedAtMs > 0);
        Assert.NotEmpty(proj.Metadata.NodeTypeCounts);
    }

    [Fact]
    public void Project_节点已去重()
    {
        var proj = _service.Project(maxDaysPerStock: 5, maxNews: 5, maxRelationships: 100);

        var ids = proj.Nodes.Select(n => n.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
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
