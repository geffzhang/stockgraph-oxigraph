using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockGraph.Web.Tests;

[Collection("WebIntegration")]
public class GraphEndpointsTests
{
    private static string RepoRoot => FindRepoRoot(AppContext.BaseDirectory);

    [Fact]
    public async Task graph_返回投影数据()
    {
        await using var fixture = new GraphTestFixture();
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
        await using var fixture = new GraphTestFixture();
        var response = await fixture.Client.GetAsync("/api/graph?maxRelationships=3");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var edges = json.GetProperty("edges");
        Assert.True(edges.GetArrayLength() <= 3);
    }

    private sealed class GraphTestFixture : IAsyncDisposable, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string _tempDir;
        public HttpClient Client { get; }

        public GraphTestFixture()
        {
            WebTestLock.Instance.Wait();
            _tempDir = Path.Combine(Path.GetTempPath(), $"sg_wtest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            var dataDir = Path.Combine(_tempDir, "data");
            Directory.CreateDirectory(dataDir);
            File.Copy(
                Path.Combine(RepoRoot, "Financial-Knowledge-Graphs", "data", "000001.XSHE.csv"),
                Path.Combine(dataDir, "000001.XSHE.csv"));

            _factory = new CustomWebApplicationFactory { SourceDataPath = _tempDir };
            Client = _factory.CreateClient();

            // Build first so store has data
            Client.PostAsync("/api/store/build?maxPriceRows=5&clear=true", null).Wait();
        }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
            WebTestLock.Instance.Release();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            if (_factory is IAsyncDisposable ad)
                await ad.DisposeAsync();
            else
                _factory.Dispose();
            try { Directory.Delete(_tempDir, true); } catch { }
            WebTestLock.Instance.Release();
        }
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
