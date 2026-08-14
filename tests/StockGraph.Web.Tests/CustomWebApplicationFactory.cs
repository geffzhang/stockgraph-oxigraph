using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace StockGraph.Web.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _uniqueStorePath;
    private bool _disposed;

    public string SourceDataPath { get; set; } = "";

    public CustomWebApplicationFactory()
    {
        // Each factory instance gets its own RocksDB store path to avoid lock conflicts
        // when tests run in the same process.
        _uniqueStorePath = Path.Combine(Path.GetTempPath(), $"sg_store_{Guid.NewGuid():N}");
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // UseSetting fires BEFORE Program.cs's top-level statements run, so these
        // reliably override the eagerly-resolved AppSettings (ReadAppSettings is
        // called in Program.cs before Build(), at which point the deferred
        // ConfigureAppConfiguration callbacks have not fired yet — without this,
        // every test would share the default .oxigraph/financial_kg store and
        // collide on the RocksDB LOCK file).
        builder.UseSetting("Data:StorePath", _uniqueStorePath);
        if (!string.IsNullOrEmpty(SourceDataPath))
            builder.UseSetting("Data:SourcePath", SourceDataPath);

        builder.ConfigureAppConfiguration((context, b) =>
        {
            var dict = new Dictionary<string, string?>();

            if (!string.IsNullOrEmpty(SourceDataPath))
                dict["Data:SourcePath"] = SourceDataPath;

            // Unique store path per factory instance — critical for test isolation
            dict["Data:StorePath"] = _uniqueStorePath;

            b.AddInMemoryCollection(dict);
        });
    }

    /// <summary>
    /// Deletes the store directory so the native RocksDB handle is released before
    /// the next test (or the next CreateClient call) tries to open a store.
    /// </summary>
    internal void ResetStoreDirectory()
    {
        try
        {
            if (Directory.Exists(_uniqueStorePath))
                Directory.Delete(_uniqueStorePath, recursive: true);
        }
        catch { /* best-effort */ }
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            ResetStoreDirectory();
        }

        base.Dispose(disposing);
    }
}
