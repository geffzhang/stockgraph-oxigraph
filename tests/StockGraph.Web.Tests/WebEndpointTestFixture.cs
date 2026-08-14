namespace StockGraph.Web.Tests;

/// <summary>
/// Shared per-test fixture for the Web integration tests. Serializes test
/// execution via <see cref="WebTestLock"/>, copies the price CSV into a fresh
/// temp dir, points a <see cref="CustomWebApplicationFactory"/> at that dir and
/// exposes an <see cref="HttpClient"/>. Optionally builds the store before
/// returning so endpoints that read data see a populated store.
/// </summary>
internal sealed class WebEndpointTestFixture : IAsyncDisposable, IDisposable
{
    private readonly CustomWebApplicationFactory? _factory;
    private readonly string? _tempDir;
    private bool _disposed;
    private bool _lockAcquired;

    public HttpClient Client { get; }

    public WebEndpointTestFixture(bool buildFirst = false)
    {
        try
        {
            WebTestLock.Instance.Wait();
            _lockAcquired = true;

            _tempDir = Path.Combine(Path.GetTempPath(), $"sg_wtest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            var dataDir = Path.Combine(_tempDir, "data");
            Directory.CreateDirectory(dataDir);
            File.Copy(
                Path.Combine(RepoRoot, "Financial-Knowledge-Graphs", "data", "000001.XSHE.csv"),
                Path.Combine(dataDir, "000001.XSHE.csv"));

            _factory = new CustomWebApplicationFactory { SourceDataPath = _tempDir };
            Client = _factory.CreateClient();

            if (buildFirst)
            {
                using var response = Client.PostAsync("/api/store/build?maxPriceRows=5&clear=true", null)
                    .GetAwaiter().GetResult();
            }
        }
        catch
        {
            ReleaseResources();
            throw;
        }
    }

    public void Dispose() => ReleaseResources();

    public ValueTask DisposeAsync() => ReleaseResourcesAsync();

    private void ReleaseResources()
    {
        if (_disposed) return;
        _disposed = true;

        try { Client?.Dispose(); } catch { }

        // Dispose the factory (which disposes the host/coordinator and releases
        // the native RocksDB handle) BEFORE deleting the temp store dir, so the
        // delete never races an open native handle.
        try { _factory?.Dispose(); } catch { }
        try { DeleteTempDir(); } catch { }

        ReleaseLock();
    }

    private async ValueTask ReleaseResourcesAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try { Client?.Dispose(); } catch { }

        try
        {
            if (_factory is IAsyncDisposable ad)
                await ad.DisposeAsync().ConfigureAwait(false);
            else
                _factory?.Dispose();
        }
        catch { }

        try { DeleteTempDir(); } catch { }

        ReleaseLock();
    }

    private void DeleteTempDir()
    {
        if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void ReleaseLock()
    {
        if (!_lockAcquired) return;
        _lockAcquired = false;
        WebTestLock.Instance.Release();
    }

    private static string RepoRoot => FindRepoRoot(AppContext.BaseDirectory);

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
