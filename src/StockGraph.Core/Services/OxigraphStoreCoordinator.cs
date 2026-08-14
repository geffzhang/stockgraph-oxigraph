using Oxigraph;

namespace StockGraph.Core.Services;

public class OxigraphStoreCoordinator : IDisposable
{
    private readonly string _storePath;
    private readonly object _lock = new();
    private Store? _store;
    private bool _disposed;

    public OxigraphStoreCoordinator(string storePath)
    {
        _storePath = storePath;
    }

    public void Open()
    {
        lock (_lock)
        {
            // Idempotent: if the store is already open, do nothing. Opening a
            // store is non-destructive — we never dispose or delete an existing
            // store directory, because doing so races with any lingering native
            // RocksDB LOCK handle (e.g. when the same path is reused across tests).
            if (_store != null)
                return;

            // Ensure parent directory exists.
            var dir = Path.GetDirectoryName(_storePath)!;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Retry a few times with a small delay — native RocksDB handles may not
            // be released immediately after Dispose in some environments (e.g. tests).
            const int maxRetries = 3;
            Exception? lastError = null;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    _store = new Store(_storePath);
                    return;
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    lastError = ex;
                    Thread.Sleep(50);
                }
            }
            throw lastError!;
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            _store?.Dispose();
            _store = null;
        }
    }

    /// <summary>
    /// Closes the store and resets state so a fresh open is possible.
    /// Safe for re-use across test scenarios.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            if (_store != null)
            {
                _store.Dispose();
                _store = null;
            }
        }
    }

    /// <summary>
    /// Executes a SPARQL query against the store.
    /// </summary>
    /// <param name="sparql">The SPARQL query string.</param>
    /// <returns>A <see cref="QueryResults"/> containing the query results.</returns>
    public QueryResults ExecuteQuery(string sparql)
    {
        lock (_lock)
        {
            if (_store == null) throw new InvalidOperationException("Store not opened");
            return _store.Query(sparql);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _store?.Clear();
        }
    }

    public void AddQuads(IEnumerable<Quad> quads)
    {
        lock (_lock)
        {
            if (_store == null) throw new InvalidOperationException("Store not opened");
            foreach (var q in quads)
                _store.Add(q);
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            // Oxigraph 自动刷新，无需显式操作
        }
    }

    public bool IsOpen => _store != null;

    /// <summary>
    /// Builds the store from source data. This operation is delegated to <see cref="FinancialGraphBuilder"/>.
    /// </summary>
    public void Build()
        => throw new NotImplementedException("Build is handled by FinancialGraphBuilder");

    /// <summary>
    /// Exports the store data to a specified RDF format. This operation is delegated to <see cref="RdfExportService"/>.
    /// </summary>
    public void Export(string format, Stream outputStream)
        => throw new NotImplementedException("Export is handled by RdfExportService");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}