using StockGraph.Core;
using StockGraph.Core.Services;
using StockGraph.Web.Configuration;
using StockGraph.Web.Endpoints;
using StockGraph.Web.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "StockGraph API", Version = "v1" });
});

var settings = builder.Configuration.ReadAppSettings();
builder.Services.AddSingleton(settings);

// Register coordinator as a singleton so the store persists across requests.
// Open() is called lazily on first use — after ConfigureWebHost has had a
// chance to override the config values in tests.
var coordinator = new OxigraphStoreCoordinator(settings.StorePath);
builder.Services.AddSingleton(coordinator);

// Services resolve the coordinator directly (it's already instantiated).
// The builder is constructed WITHOUT a graph IRI so all quads are loaded into
// the default graph — Oxigraph does not support FROM/FROM NAMED SPARQL clauses,
// so default-graph queries (SparqlService, GraphProjectionService, RdfExportService)
// would otherwise see an empty dataset.
builder.Services.AddSingleton(sp => new FinancialGraphBuilder(coordinator));
builder.Services.AddSingleton(sp => new SparqlService(
    coordinator, settings.ResolveQueryDirectory()));
builder.Services.AddSingleton(sp => new RdfExportService(coordinator));
builder.Services.AddSingleton(sp => new GraphProjectionService(coordinator));

var app = builder.Build();

// Register the exception handler BEFORE the lazy-open middleware so that
// failures in coordinator.Open() (locked store -> 409, not opened -> 503)
// flow into ProblemDetailsMapper instead of bypassing it as unhandled 500s.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exFeature?.Error != null)
        {
            var (status, detail) = ProblemDetailsMapper.MapException(exFeature.Error);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = $"https://stockgraph.local/errors/{status}",
                title = status switch { 400 => "Bad Request", 409 => "Conflict", 499 => "Client Close Request", 503 => "Service Unavailable", _ => "Internal Server Error" },
                status,
                detail = status == 500 ? "An unexpected error occurred" : detail,
                instance = context.Request.Path,
            }, options: null, contentType: "application/problem+json");
        }
    });
});

// Defer Open() until the first request rather than at startup so test
// factories can override Data:StorePath before the store is opened.
// Open() is destructive (it disposes any open store, deletes the store
// directory, and re-creates it), so it must run exactly once. Double-checked
// locking prevents concurrent first requests from racing each other through
// Open() and tearing the store down mid-flight.
var storeLock = new object();
var storeOpened = false;
app.Use(async (context, next) =>
{
    if (!storeOpened)
    {
        lock (storeLock)
        {
            if (!storeOpened)
            {
                coordinator.Open();
                storeOpened = true;
            }
        }
    }
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(AppContext.BaseDirectory, "wwwroot"))
});

app.MapHealthEndpoints();
app.MapStoreEndpoints();
app.MapSparqlEndpoints();
app.MapExportEndpoints();
app.MapGraphEndpoints();

app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
