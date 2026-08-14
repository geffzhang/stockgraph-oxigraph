using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StockGraph.Web.Tests;

[Collection("WebIntegration")]
public class ExportEndpointsTests
{
    private static string RepoRoot => FindRepoRoot(AppContext.BaseDirectory);

    [Fact]
    public async Task export_nt_返回_ntriples()
    {
        await using var fixture = new ExportTestFixture();
        var response = await fixture.Client.GetAsync("/api/export?format=nt");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/ntriples", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_nq_返回_nquads()
    {
        await using var fixture = new ExportTestFixture();
        var response = await fixture.Client.GetAsync("/api/export?format=nq");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/n-quads", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_ttl_返回_turtle()
    {
        await using var fixture = new ExportTestFixture();
        var response = await fixture.Client.GetAsync("/api/export?format=ttl");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/turtle", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_trig_返回_trig()
    {
        await using var fixture = new ExportTestFixture();
        var response = await fixture.Client.GetAsync("/api/export?format=trig");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/trig", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_未知格式返回_400()
    {
        await using var fixture = new ExportTestFixture();
        var response = await fixture.Client.GetAsync("/api/export?format=pdf");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class ExportTestFixture : IAsyncDisposable, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string _tempDir;
        public HttpClient Client { get; }

        public ExportTestFixture()
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
