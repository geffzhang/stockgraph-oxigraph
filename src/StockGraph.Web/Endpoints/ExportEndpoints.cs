using StockGraph.Core.Services;

namespace StockGraph.Web.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/export", (
            RdfExportService export,
            string? format = "trig",
            string? graph = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var fmt = RdfExportService.ParseFormat(format);
                var mime = RdfExportService.GetMimeType(fmt);
                var stream = export.Export(fmt, graph);
                return Results.File(stream, mime, $"export.{format}");
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        });
    }
}
