using StockGraph.Core;

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
}