using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockGraph.Web.Tests;

[Collection("WebIntegration")]
public class SparqlEndpointsTests
{
    private static string RepoRoot => FindRepoRoot(AppContext.BaseDirectory);

    [Fact]
    public async Task sparql_post_select_返回结果()
    {
        await using var fixture = new SparqlTestFixture();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sparql")
        {
            Content = new StringContent("SELECT ?stock WHERE { ?stock a ex:Stock } LIMIT 5",
                Encoding.UTF8, "application/sparql-query"),
        };
        var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task sparql_post_ask_返回布尔值()
    {
        await using var fixture = new SparqlTestFixture();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sparql")
        {
            Content = new StringContent("ASK { ?s a ex:Stock }",
                Encoding.UTF8, "application/sparql-query"),
        };
        var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class SparqlTestFixture : IAsyncDisposable, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string _tempDir;
        public HttpClient Client { get; }

        public SparqlTestFixture()
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
