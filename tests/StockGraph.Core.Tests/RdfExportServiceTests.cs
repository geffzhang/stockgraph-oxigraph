using StockGraph.Core.Services;

namespace StockGraph.Core.Tests;

public class RdfExportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly RdfExportService _export;

    public RdfExportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        File.Copy(
            Path.Combine(repoRoot, "Financial-Knowledge-Graphs", "data", "000001.XSHE.csv"),
            Path.Combine(dataDir, "000001.XSHE.csv"));

        var storePath = Path.Combine(_tempDir, ".oxigraph", "store");
        _coordinator = new OxigraphStoreCoordinator(storePath);
        _coordinator.Open();
        var builder = new FinancialGraphBuilder(_coordinator);
        builder.Build(_tempDir, clear: true, maxPriceRows: 5);
        _export = new RdfExportService(_coordinator);
    }

    [Theory]
    [InlineData("nt", "application/ntriples")]
    [InlineData("nq", "application/n-quads")]
    [InlineData("ttl", "application/turtle")]
    [InlineData("trig", "application/trig")]
    public void Export_返回正确的_MIME_类型(string format, string expectedMime)
    {
        Assert.Equal(expectedMime, RdfExportService.GetMimeType(RdfExportService.ParseFormat(format)));
    }

    [Theory]
    [InlineData("nt")]
    [InlineData("nq")]
    [InlineData("ttl")]
    public void Export_产生非空内容(string format)
    {
        var fmt = RdfExportService.ParseFormat(format);
        using var stream = _export.Export(fmt);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.NotEmpty(content);
    }

    [Fact]
    public void ParseFormat_拒绝未知格式()
    {
        Assert.Throws<ArgumentException>(() => RdfExportService.ParseFormat("pdf"));
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
