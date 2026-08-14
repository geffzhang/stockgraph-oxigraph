namespace StockGraph.Core.Services;

public enum RdfFormat
{
    TriG,
    NQuads,
    Turtle,
    NTriples,
}

public class RdfExportService
{
    private readonly OxigraphStoreCoordinator _coordinator;

    public RdfExportService(OxigraphStoreCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public Stream Export(RdfFormat format, string? namedGraph = null)
    {
        if (namedGraph != null)
        {
            // Validate before interpolating into the GRAPH <...> IRIREF to prevent
            // SPARQL injection (the named graph comes from user input via the Web endpoint).
            if (string.IsNullOrWhiteSpace(namedGraph)
                || !Uri.TryCreate(namedGraph, UriKind.Absolute, out _)
                || namedGraph.IndexOfAny(new[] { '<', '>', ' ', '\t', '\r', '\n' }) >= 0)
            {
                throw new ArgumentException($"Invalid graph IRI: {namedGraph}", nameof(namedGraph));
            }
        }

        var query = namedGraph != null
            ? $"CONSTRUCT {{ ?s ?p ?o }} WHERE {{ GRAPH <{namedGraph}> {{ ?s ?p ?o }} }}"
            : "CONSTRUCT { ?s ?p ?o } WHERE { ?s ?p ?o }";

        var result = _coordinator.ExecuteQuery(query);
        var triples = (Oxigraph.QueryTriples)result;

        var oxigraphFormat = format switch
        {
            RdfFormat.NTriples => Oxigraph.RdfFormat.NTriples,
            RdfFormat.NQuads => Oxigraph.RdfFormat.NQuads,
            RdfFormat.Turtle => Oxigraph.RdfFormat.Turtle,
            RdfFormat.TriG => Oxigraph.RdfFormat.TriG,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        var serialized = triples.Serialize(oxigraphFormat);
        var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(serialized));
        return ms;
    }

    public static RdfFormat ParseFormat(string? format)
    {
        return format?.ToLowerInvariant() switch
        {
            "nt" or "ntriples" => RdfFormat.NTriples,
            "nq" or "nquads" => RdfFormat.NQuads,
            "ttl" or "turtle" => RdfFormat.Turtle,
            "trig" => RdfFormat.TriG,
            _ => throw new ArgumentException($"Unsupported format: {format}", nameof(format)),
        };
    }

    public static string GetMimeType(RdfFormat format) => format switch
    {
        RdfFormat.NTriples => "application/ntriples",
        RdfFormat.NQuads => "application/n-quads",
        RdfFormat.Turtle => "application/turtle",
        RdfFormat.TriG => "application/trig",
        _ => "application/octet-stream",
    };
}
