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
            if (_store != null) return;
            var dir = Path.GetDirectoryName(_storePath)!;
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            _store = new Store(_storePath);
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
    /// Executes a SPARQL query against the store.
    /// </summary>
    /// <param name="sparql">The SPARQL query string.</param>
    /// <returns>An <see cref="Oxigraph.QueryResults"/> containing the query results. Caller is responsible for disposing.</returns>
    public Oxigraph.QueryResults? ExecuteQuery(string sparql)
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

    public void AddQuads(IEnumerable<Oxigraph.Quad> quads)
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
    /// <remarks>
    /// This stub throws <see cref="NotImplementedException"/> as the actual build logic
    /// resides in FinancialGraphBuilder (Task 2). The coordinator provides the underlying
    /// operations (Clear, AddQuads, Flush) that FinancialGraphBuilder orchestrates.
    /// </remarks>
    public void Build()
        => throw new NotImplementedException("Build is handled by FinancialGraphBuilder");

    /// <summary>
    /// Exports the store data to a specified RDF format. This operation is delegated to <see cref="RdfExportService"/>.
    /// </summary>
    /// <remarks>
    /// This stub throws <see cref="NotImplementedException"/> as the actual export logic
    /// resides in RdfExportService (Task 4). The coordinator provides query execution
    /// that RdfExportService uses to retrieve data for export.
    /// </remarks>
    /// <param name="format">The RDF format (e.g., "trig", "nq", "ttl", "nt").</param>
    /// <param name="outputStream">The stream to write exported data to.</param>
    public void Export(string format, Stream outputStream)
        => throw new NotImplementedException("Export is handled by RdfExportService");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}