using StockGraph.Core;
using StockGraph.Core.Services;

namespace StockGraph.Core.Tests;

public class FinancialGraphBuilderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly FinancialGraphBuilder _builder;

    public FinancialGraphBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);

        // Find repo root by walking up from BaseDirectory
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = FindRepoRoot(baseDir);
        var sampleDataDir = Path.Combine(repoRoot, "Financial-Knowledge-Graphs", "data");

        File.Copy(
            Path.Combine(sampleDataDir, "000001.XSHE.csv"),
            Path.Combine(dataDir, "000001.XSHE.csv"));
        File.Copy(
            Path.Combine(sampleDataDir, "000063.XSHE.csv"),
            Path.Combine(dataDir, "000063.XSHE.csv"));
        File.Copy(
            Path.Combine(sampleDataDir, "latest_news.csv"),
            Path.Combine(dataDir, "latest_news.csv"));

        var storePath = Path.Combine(_tempDir, ".oxigraph", "store");
        _coordinator = new OxigraphStoreCoordinator(storePath);
        _coordinator.Open();
        _builder = new FinancialGraphBuilder(_coordinator);
    }

    [Fact]
    public void Build_导入股票文件()
    {
        var stats = _builder.Build(_tempDir, clear: true, maxPriceRows: 10, maxNewsRows: 5);

        Assert.True(stats.Rows.Count >= 2);
        Assert.True(stats.Rows.ContainsKey("000001.XSHE.csv"));
        Assert.Equal(10, stats.Rows["000001.XSHE.csv"]);
    }

    [Fact]
    public void Build_报告新闻行数()
    {
        var stats = _builder.Build(_tempDir, clear: true, maxPriceRows: 5, maxNewsRows: 3);

        Assert.True(stats.Rows.ContainsKey("latest_news.csv"));
        Assert.Equal(3, stats.Rows["latest_news.csv"]);
    }

    [Fact]
    public void Build_文件缺失时报告跳过()
    {
        // Must create the "data" subdirectory — Build() always looks for dataDir = sourceDir/data
        var emptyDataDir = Path.Combine(_tempDir, "empty_data", "data");
        Directory.CreateDirectory(emptyDataDir);
        var stats = _builder.Build(Path.Combine(_tempDir, "empty_data"), clear: true);

        Assert.Contains(stats.SkippedFiles, f => f.Contains("latest_news.csv"));
    }

    [Fact]
    public void Build_clear_重置存储()
    {
        _builder.Build(_tempDir, clear: true, maxPriceRows: 5);
        var stats2 = _builder.Build(_tempDir, clear: true, maxPriceRows: 2);

        Assert.Equal(2, stats2.Rows["000001.XSHE.csv"]);
    }

    [Fact]
    public void Build_遵守_maxPriceRows_限制()
    {
        var stats = _builder.Build(_tempDir, clear: true, maxPriceRows: 3);

        Assert.Equal(3, stats.Rows["000001.XSHE.csv"]);
        Assert.Equal(3, stats.Rows["000063.XSHE.csv"]);
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
        // Fallback: return original start dir (tests may fail if not found)
        return startDir;
    }
}