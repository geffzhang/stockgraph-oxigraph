using Oxigraph;

namespace StockGraph.Core.Services;

public class SparqlService
{
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly string _queryDirectory;
    private readonly Dictionary<string, string> _prefixes;

    public SparqlService(OxigraphStoreCoordinator coordinator, string queryDirectory)
    {
        _coordinator = coordinator;
        _queryDirectory = queryDirectory;
        _prefixes = new Dictionary<string, string>
        {
            ["ex"] = Vocabulary.Ex,
            ["schema"] = Vocabulary.Schema,
            ["rdf"] = Vocabulary.Rdf,
            ["rdfs"] = Vocabulary.Rdfs,
            ["xsd"] = Vocabulary.Xsd,
        };
    }

    public QueryResults Execute(string sparql)
    {
        var prefixBlock = string.Join("\n", _prefixes.Select(kv => $"PREFIX {kv.Key}: <{kv.Value}>"));
        return _coordinator.ExecuteQuery(prefixBlock + "\n" + sparql);
    }

    public QueryResults ExecuteFile(string name)
    {
        ValidateFileName(name);
        var filePath = Path.Combine(_queryDirectory, name);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Query file not found: {name}");
        var query = File.ReadAllText(filePath);
        return Execute(query);
    }

    private static void ValidateFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Query name cannot be empty", nameof(name));
        if (name.Contains("..") || name.Contains('/') || name.Contains('\\'))
            throw new ArgumentException("Query name cannot contain path traversal", nameof(name));
        if (!name.EndsWith(".sparql", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Query name must end with .sparql", nameof(name));
    }
}
