using StockGraph.Core.Services;
using StockGraph.Web.Configuration;

namespace StockGraph.Web.Endpoints;

public static class GraphEndpoints
{
    public static void MapGraphEndpoints(this WebApplication app)
    {
        app.MapGet("/api/graph", (
            GraphProjectionService projector,
            AppSettings settings,
            int? maxDaysPerStock = null,
            int? maxNews = null,
            int? maxRelationships = null) =>
        {
            var proj = projector.Project(
                maxDaysPerStock ?? settings.DefaultMaxDaysPerStock,
                maxNews ?? settings.DefaultMaxNews,
                maxRelationships ?? settings.DefaultMaxRelationships);

            return Results.Json(proj);
        });
    }
}
