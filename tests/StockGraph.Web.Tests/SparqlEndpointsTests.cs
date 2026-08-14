using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace StockGraph.Web.Tests;

[Collection("WebIntegration")]
public class SparqlEndpointsTests
{
    [Fact]
    public async Task sparql_post_select_返回结果()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: true);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sparql")
        {
            Content = new StringContent("SELECT ?stock WHERE { ?stock a ex:Stock } LIMIT 5",
                Encoding.UTF8, "application/sparql-query"),
        };
        var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/sparql-results+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, json.ValueKind);
    }

    [Fact]
    public async Task sparql_post_ask_返回布尔值()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: true);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sparql")
        {
            Content = new StringContent("ASK { ?s a ex:Stock }",
                Encoding.UTF8, "application/sparql-query"),
        };
        var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("boolean", out _));
    }
}
