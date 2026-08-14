using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace StockGraph.Web.Tests;

[Collection("WebIntegration")]
public class GraphEndpointsTests
{
    [Fact]
    public async Task graph_返回投影数据()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: true);
        var response = await fixture.Client.GetAsync("/api/graph?maxDaysPerStock=5&maxNews=3&maxRelationships=10");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.TryGetProperty("nodes", out var nodes));
        Assert.True(nodes.GetArrayLength() > 0);
        Assert.True(json.TryGetProperty("metadata", out var meta));
        Assert.True(meta.GetProperty("totalNodes").GetInt32() > 0);
    }

    [Fact]
    public async Task graph_遵守_maxRelationships_限制()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: true);
        var response = await fixture.Client.GetAsync("/api/graph?maxRelationships=3");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var edges = json.GetProperty("edges");
        Assert.True(edges.GetArrayLength() <= 3);
    }
}
