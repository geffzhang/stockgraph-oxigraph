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

    public object ExecuteQuery(string sparql)
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}