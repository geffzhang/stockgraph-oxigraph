using Oxigraph;
using StockGraph.Core.Services;

namespace StockGraph.Web.Endpoints;

public static class SparqlEndpoints
{
    public static void MapSparqlEndpoints(this WebApplication app)
    {
        app.MapPost("/api/sparql", async (
            HttpContext httpContext,
            OxigraphStoreCoordinator coordinator,
            SparqlService sparql,
            CancellationToken ct = default) =>
        {
            try
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var query = await reader.ReadToEndAsync(ct);
                var result = sparql.Execute(query);

                if (result is QuerySolutions qs)
                {
                    var json = qs.Serialize(QueryResultsFormat.Json);
                    return Results.Text(json, "application/sparql-results+json");
                }
                if (result is QueryBoolean b)
                {
                    return Results.Json(new { boolean = b.Value });
                }
                if (result is QueryTriples qt)
                {
                    using var ms = new System.IO.MemoryStream();
                    qt.SerializeToStream(ms, Oxigraph.RdfFormat.TriG);
                    return Results.Bytes(ms.ToArray(), "application/trig");
                }

                return Results.Json(new { });
            }
            catch (ArgumentException ex)
            {
                return Results.Problem($"SPARQL syntax error: {ex.Message}", statusCode: 422);
            }
        });

        app.MapGet("/api/queries/{name}", (
            SparqlService sparql,
            string name,
            CancellationToken ct = default) =>
        {
            try
            {
                var result = sparql.ExecuteFile(name);
                if (result is QuerySolutions qs)
                {
                    var json = qs.Serialize(QueryResultsFormat.Json);
                    return Results.Text(json, "application/sparql-results+json");
                }
                if (result is QueryBoolean b)
                {
                    return Results.Json(new { boolean = b.Value });
                }
                return Results.Json(new { });
            }
            catch (FileNotFoundException)
            {
                return Results.Problem($"Query file '{name}' not found", statusCode: 404);
            }
            catch (ArgumentException)
            {
                return Results.Problem("Invalid query name", statusCode: 400);
            }
        });
    }
}
