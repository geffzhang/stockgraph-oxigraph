using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockGraph.Web.Tests;

[Collection("WebIntegration")]
public class HealthEndpointsTests
{
    [Fact]
    public async Task healthz_存储打开时返回_200()
    {
        await WebTestLock.Instance.WaitAsync();
        await using var factory = new CustomWebApplicationFactory();
        try
        {
            var client = factory.CreateClient();
            var response = await client.GetAsync("/healthz");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("healthy", json.GetProperty("status").GetString());
        }
        finally
        {
            WebTestLock.Instance.Release();
        }
    }
}
