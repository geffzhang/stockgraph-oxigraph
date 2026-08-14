using Oxigraph;
using StockGraph.Core.Services;

namespace StockGraph.Core.Tests;

public class SparqlServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _queryDir;
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly SparqlService _sparql;

    public SparqlServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_sparql_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);
        _queryDir = Path.Combine(_tempDir, "queries");
        Directory.CreateDirectory(_queryDir);

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
        builder.Build(_tempDir, clear: true, maxPriceRows: 20, maxNewsRows: 5);

        _sparql = new SparqlService(_coordinator, _queryDir);
    }

    [Fact]
    public void Execute_select_返回结果集()
    {
        var result = _sparql.Execute(@"
SELECT ?stock ?exchange WHERE {
  ?stock a ex:Stock ;
         ex:exchange ?exchange .
}
LIMIT 5");

        Assert.IsAssignableFrom<QuerySolutions>(result);
        var rs = (QuerySolutions)result;
        Assert.True(rs.Count > 0);
    }

    [Fact]
    public void Execute_ask_返回布尔值()
    {
        var result = _sparql.Execute("ASK { ?s a ex:Stock }");
        Assert.IsAssignableFrom<QueryBoolean>(result);
        Assert.True(((QueryBoolean)result).Value);
    }

    [Fact]
    public void Execute_construct_返回图()
    {
        var result = _sparql.Execute(@"
CONSTRUCT { ?s ?p ?o }
WHERE {
  ?s a ex:Stock .
  ?s ?p ?o .
}
LIMIT 10");

        Assert.IsAssignableFrom<QueryTriples>(result);
        var triples = (QueryTriples)result;
        Assert.True(triples.Count() > 0);
    }

    [Fact]
    public void ExecuteFile_拒绝路径遍历()
    {
        Assert.Throws<ArgumentException>(() => _sparql.ExecuteFile("../etc/passwd"));
        Assert.Throws<ArgumentException>(() => _sparql.ExecuteFile("..\\windows\\system32"));
    }

    [Fact]
    public void ExecuteFile_拒绝非_sparql_扩展名()
    {
        Assert.Throws<ArgumentException>(() => _sparql.ExecuteFile("query.txt"));
    }

    [Fact]
    public void ExecuteFile_加载存储的查询文件()
    {
        var queryFile = Path.Combine(_queryDir, "list_stocks.sparql");
        File.WriteAllText(queryFile, @"
SELECT ?stock ?label ?exchange WHERE {
  ?stock a ex:Stock ;
         ex:exchange ?exchange .
  OPTIONAL { ?stock rdfs:label ?label }
}
LIMIT 20");

        var result = _sparql.ExecuteFile("list_stocks.sparql");
        Assert.IsAssignableFrom<QuerySolutions>(result);
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
