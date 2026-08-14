using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockGraph.Web.Tests;

[Collection("WebIntegration")]
public class StoreEndpointsTests
{
    private static string RepoRoot => FindRepoRoot(AppContext.BaseDirectory);

    [Fact]
    public async Task build_返回_quad_数量()
    {
        await using var fixture = new StoreTestFixture();
        var response = await fixture.Client.PostAsync("/api/store/build?maxPriceRows=5&clear=true", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("quads").GetInt32() > 0);
    }

    [Fact]
    public async Task build_拒绝无效_chunkSize()
    {
        await using var fixture = new StoreTestFixture();
        var response = await fixture.Client.PostAsync("/api/store/build?chunkSize=0", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class StoreTestFixture : IAsyncDisposable, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string _tempDir;
        public HttpClient Client { get; }

        public StoreTestFixture()
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
