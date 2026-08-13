namespace StockGraph.Core.Models;

public record G6Projection(
    List<G6Node> Nodes,
    List<G6Edge> Edges,
    G6Metadata Metadata);

public record G6Node(
    string Id,
    string Label,
    string Type,
    Dictionary<string, object> Properties);

public record G6Edge(
    string Source,
    string Target,
    string Label,
    string Type);

public record G6Metadata(
    long GeneratedAtMs,
    int TotalNodes,
    int TotalEdges,
    Dictionary<string, int> NodeTypeCounts);