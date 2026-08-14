using StockGraph.Core;
using StockGraph.Core.Services;
using StockGraph.Web.Configuration;

namespace StockGraph.Web.Endpoints;

public static class StoreEndpoints
{
    public static void MapStoreEndpoints(this WebApplication app)
    {
        app.MapPost("/api/store/build", async (
            OxigraphStoreCoordinator coordinator,
            FinancialGraphBuilder builder,
            AppSettings settings,
            bool clear = false,
            int? maxPriceRows = null,
            int? maxNewsRows = null,
            int chunkSize = 10_000,
            CancellationToken ct = default) =>
        {
            if (chunkSize <= 0)
                return Results.Problem("chunkSize must be positive", statusCode: 400);

            var sourcePath = settings.ResolveSourceDataPath();
            if (!Directory.Exists(sourcePath))
                return Results.Problem($"Source data directory not found: {sourcePath}", statusCode: 400);

            try
            {
                var stats = await Task.Run(() =>
                    builder.Build(sourcePath, clear,
                        maxPriceRows ?? settings.DefaultMaxPriceRows,
                        maxNewsRows ?? settings.DefaultMaxNewsRows,
                        chunkSize), ct);

                return Results.Json(new
                {
                    quads = stats.Quads,
                    rows = stats.Rows,
                    skippedFiles = stats.SkippedFiles,
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("locked"))
            {
                return Results.Problem("Storage is currently locked by another build operation", statusCode: 409);
            }
        });
    }
}
