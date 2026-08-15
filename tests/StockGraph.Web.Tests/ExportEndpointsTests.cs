using System.Net;
using Xunit;

namespace StockGraph.Web.Tests;

[Collection("WebIntegration")]
public class ExportEndpointsTests
{
    [Fact]
    public async Task export_nt_返回_ntriples()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: true);
        var response = await fixture.Client.GetAsync("/api/export?format=nt");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/ntriples", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_nq_返回_nquads()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: true);
        var response = await fixture.Client.GetAsync("/api/export?format=nq");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/n-quads", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_ttl_返回_turtle()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: true);
        var response = await fixture.Client.GetAsync("/api/export?format=ttl");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/turtle", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_trig_返回_trig()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: true);
        var response = await fixture.Client.GetAsync("/api/export?format=trig");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/trig", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_未知格式返回_400()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: true);
        var response = await fixture.Client.GetAsync("/api/export?format=pdf");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
