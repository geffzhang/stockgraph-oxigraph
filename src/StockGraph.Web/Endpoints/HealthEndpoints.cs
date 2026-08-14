using StockGraph.Core.Services;

namespace StockGraph.Web.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/healthz", (OxigraphStoreCoordinator coordinator) =>
        {
            if (!coordinator.IsOpen)
                return Results.Json(new { status = "unavailable", store = "not opened" }, statusCode: 503);
            return Results.Json(new { status = "healthy", store = "available" });
        });
    }
}
