using StockGraph.Core;

namespace StockGraph.Core.Tests;

public class VocabularyTests
{
    [Fact]
    public void Base_iri_正确()
    {
        Assert.Equal("https://stockgraph.local/kg/", Vocabulary.Base);
    }

    [Fact]
    public void Ex_等于_Base()
    {
        Assert.Equal(Vocabulary.Base, Vocabulary.Ex);
    }

    [Fact]
    public void Iri_创建有效_URI()
    {
        var uri = Vocabulary.Iri("https://example.org/test");
        Assert.Equal("https://example.org/test", uri.ToString());
    }

    [Fact]
    public void Xsd_dateTerm_正确()
    {
        Assert.Equal("http://www.w3.org/2001/XMLSchema#date", Vocabulary.XsdTerm("date").ToString());
    }

    [Fact]
    public void GraphIri_符合预期()
    {
        Assert.Equal("https://stockgraph.local/kg/graph/main", Vocabulary.GraphIri);
    }

    [Fact]
    public void Schema_iri_正确()
    {
        Assert.Equal("https://schema.org/", Vocabulary.Schema);
    }
}