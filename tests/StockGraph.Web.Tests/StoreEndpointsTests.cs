using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace StockGraph.Web.Tests;

[Collection("WebIntegration")]
public class StoreEndpointsTests
{
    [Fact]
    public async Task build_返回_quad_数量()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: false);
        var response = await fixture.Client.PostAsync("/api/store/build?maxPriceRows=5&clear=true", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("quads").GetInt32() > 0);
    }

    [Fact]
    public async Task build_拒绝无效_chunkSize()
    {
        await using var fixture = new WebEndpointTestFixture(buildFirst: false);
        var response = await fixture.Client.PostAsync("/api/store/build?chunkSize=0", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
