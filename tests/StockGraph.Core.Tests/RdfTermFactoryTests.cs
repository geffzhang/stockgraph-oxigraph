using StockGraph.Core;
using StockGraph.Core.Models;
using Oxigraph;
using Oxigraph.Extensions.DotNetRDF;
using StockGraph.Core.Services;
using VDS.RDF;

namespace StockGraph.Core.Tests;

public class RdfTermFactoryTests
{
    [Fact]
    public void StockNode_生成正确IRI()
    {
        var node = RdfTermFactory.StockNode("000001.XSHE");
        Assert.Equal("https://stockgraph.local/kg/stock/000001.XSHE", node.ToString());
    }

    [Fact]
    public void StockNode_处理空格()
    {
        var node = RdfTermFactory.StockNode("000001.XSHE ");
        Assert.Contains("000001", node.ToString());
    }

    [Fact]
    public void TradingDayNode_包含日期()
    {
        var node = RdfTermFactory.TradingDayNode("000001.XSHE", "2005-03-01");
        Assert.Contains("2005-03-01", node.ToString());
    }

    [Fact]
    public void NewsNode_带时间戳()
    {
        var node = RdfTermFactory.NewsNode("2019-06-23 23:55:20", 0);
        Assert.StartsWith("https://stockgraph.local/kg/news/", node.ToString());
    }

    [Fact]
    public void NewsNode_无时间戳使用索引()
    {
        var node = RdfTermFactory.NewsNode(null, 5);
        Assert.Contains("row-5", node.ToString());
    }

    [Fact]
    public void LiteralFactory_DateLiteral_规范化8位数字()
    {
        var lit = RdfTermFactory.Literals.DateLiteral("20050301");
        Assert.Equal("2005-03-01", lit.Value);
    }

    [Fact]
    public void LiteralFactory_DateLiteral_yyyy_mm_dd_原样通过()
    {
        var lit = RdfTermFactory.Literals.DateLiteral("2005-03-01");
        Assert.Equal("2005-03-01", lit.Value);
    }

    [Fact]
    public void LiteralFactory_IntLiteral()
    {
        var lit = RdfTermFactory.Literals.IntLiteral(42);
        Assert.Equal(42, int.Parse(lit.Value));
    }

    [Fact]
    public void LiteralFactory_ZhLabelLiteral()
    {
        var lit = RdfTermFactory.Literals.ZhLabelLiteral("股票");
        Assert.Equal("股票", lit.Value);
        Assert.Equal("zh", lit.Language);
    }

    [Fact]
    public void SafeSegment_编码斜杠()
    {
        var seg = RdfTermFactory.SafeSegment("a/b");
        Assert.DoesNotContain("/", seg);
    }

    [Fact]
    public void Quad_序列化为正确的JSON格式()
    {
        var subject = new NamedNode("http://example.com/s");
        var predicate = new NamedNode("http://example.com/p");
        var obj = new Literal("hello", "en", new NamedNode("http://www.w3.org/2001/XMLSchema#string"), null);
        var graph = new DefaultGraph();
        var quad = new Oxigraph.Quad(subject, predicate, obj, graph);

        var opts = new System.Text.Json.JsonSerializerOptions
        {
            Converters = {
                new NamedOrBlankNodeConverter(),
                new NamedNodeConverter(),
                new TermConverter(),
                new GraphNameConverter(),
            }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(quad, opts);
        Assert.Contains("\"type\":\"uri\"", json);
        Assert.Contains("\"type\":\"literal\"", json);
    }

    [Fact]
    public void Quad_无选项序列化使用属性级转换器()
    {
        var subject = new NamedNode("http://example.com/s");
        var predicate = new NamedNode("http://example.com/p");
        var obj = new Literal("hello", null, null, null);
        var graph = new DefaultGraph();
        var quad = new Oxigraph.Quad(subject, predicate, obj, graph);

        var json = System.Text.Json.JsonSerializer.Serialize(quad);
        Assert.Contains("\"type\":\"uri\"", json);
    }

    [Fact]
    public void Store_Add_Quad_成功添加()
    {
        using var store = new Store();
        var subject = new NamedNode("http://example.com/s");
        var predicate = new NamedNode("http://example.com/p");
        var obj = new Literal("hello", null, null, null);
        var graph = new DefaultGraph();
        var quad = new Oxigraph.Quad(subject, predicate, obj, graph);

        store.Add(quad);

        Assert.Equal(1ul, store.Count);
    }

    [Fact]
    public void Store_Add_带XsdString_DATATYPE的Literal()
    {
        using var store = new Store();
        var subject = new NamedNode("http://example.com/s");
        var predicate = new NamedNode("http://example.com/p");
        var obj = new Literal("hello", null, new NamedNode("http://www.w3.org/2001/XMLSchema#string"), null);
        var graph = new DefaultGraph();
        var quad = new Oxigraph.Quad(subject, predicate, obj, graph);

        store.Add(quad);

        Assert.Equal(1ul, store.Count);
    }

    [Fact]
    public void Store_Add_用DotNetRDF转换的Quad()
    {
        using var store = new Store();
        var graph = new NamedNode("https://stockgraph.local/kg");

        var subject = (INamedOrBlankNode)((INode)new UriNode(Vocabulary.ExTerm("Stock"))).ToOxigraphTerm();
        var pred = (NamedNode)((INode)RdfTermFactory.RdfType).ToOxigraphTerm();
        var obj = (ITerm)((INode)RdfTermFactory.RdfsClass).ToOxigraphTerm();

        var quad = new Oxigraph.Quad(subject, pred, obj, graph);

        store.Add(quad);

        Assert.Equal(1ul, store.Count);
    }

    [Fact]
    public void FinancialGraphBuilder_通过OxigraphStoreCoordinator构建()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sg_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dataDir = Path.Combine(tempDir, "data");
        Directory.CreateDirectory(dataDir);

        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var srcData = Path.Combine(repoRoot, "Financial-Knowledge-Graphs", "data");

        try
        {
            File.Copy(
                Path.Combine(srcData, "000001.XSHE.csv"),
                Path.Combine(dataDir, "000001.XSHE.csv"));
            File.Copy(
                Path.Combine(srcData, "latest_news.csv"),
                Path.Combine(dataDir, "latest_news.csv"));

            var storePath = Path.Combine(tempDir, ".oxigraph", "store");
            using var coordinator = new OxigraphStoreCoordinator(storePath);
            coordinator.Open();

            var builder = new FinancialGraphBuilder(coordinator);
            var stats = builder.Build(tempDir, clear: true, maxPriceRows: 20, maxNewsRows: 5);

            Assert.True(stats.Quads > 0);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void 谓词互不相同()
    {
        var preds = new[] {
            RdfTermFactory.HasTradingDay,
            RdfTermFactory.HasNews,
            RdfTermFactory.Holds,
            RdfTermFactory.BelongsToConcept,
            RdfTermFactory.MemberOf,
            RdfTermFactory.CorrelatedWith,
        };
        Assert.Equal(preds.Length, preds.Select(p => p.ToString()).Distinct().Count());
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
