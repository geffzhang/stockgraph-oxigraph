# Financial Knowledge Graphs with Oxigraph (.NET)

This project stores the original `Financial-Knowledge-Graphs` Neo4j notebook
domain as a local RDF graph backed by Oxigraph, served by an ASP.NET Core Web
API on .NET 10. The store and query layer are implemented in C# (`StockGraph.Core`),
exposed through a minimal-API web host (`StockGraph.Web`).

The domain is modeled as RDF triples/quads:

- entities become IRIs under `https://stockgraph.local/kg/`
- labels and human text use `rdfs:label` / `schema:*`
- relationships become RDF predicates such as `ex:holds`, `ex:belongsToConcept`, `ex:publishedAnnouncement`, `ex:memberOf`
- queries use SPARQL instead of Cypher

## Prerequisites

- **.NET 10 SDK**
- **A local Oxigraph .NET checkout.** `src/StockGraph.Core/StockGraph.Core.csproj`
  currently references Oxigraph via local `ProjectReference`s at
  `E:\GitHub\oxigraph\dotnet` (the `Oxigraph` and `Oxigraph.Extensions.DotNetRDF`
  projects). You need that checkout on your machine, or you must update the
  `ProjectReference` paths to point at your own Oxigraph .NET checkout.

## Build

```powershell
dotnet restore StockGraph.slnx
dotnet build StockGraph.slnx
```

## Run

```powershell
dotnet run --project src/StockGraph.Web/StockGraph.Web.csproj
```

The default `http` launch profile serves the API at:

```text
http://localhost:5187
```

Open `http://localhost:5187/` in a browser to see the AntV G6 visualization
(the `/` route redirects to `/index.html`).

## Test

```powershell
dotnet test StockGraph.slnx
```

## API

Interactive API docs are available at `/swagger`. Health check: `GET /healthz`.

### Build the store

```text
POST /api/store/build?clear=true&maxPriceRows=5000&maxNewsRows=1000&chunkSize=10000
```

- `clear=true` wipes the existing store before rebuilding. The store is
  persistent and non-destructive across app restarts — data is only cleared
  when you explicitly pass `clear=true`.
- `maxPriceRows` / `maxNewsRows` default to the configured `Limits:*` values
  when omitted; `chunkSize` defaults to 10,000 and can be overridden per
  request.
- Response: `{ "quads": ..., "rows": ..., "skippedFiles": [...] }`.

### SPARQL

POST the query body with `Content-Type: application/sparql-query`:

```powershell
curl.exe -X POST "http://localhost:5187/api/sparql" `
  -H "Content-Type: application/sparql-query" `
  --data-raw "SELECT (COUNT(*) AS ?count) WHERE { ?s ?p ?o }"
```

SELECT / ASK results return `application/sparql-results+json`; CONSTRUCT /
DESCRIBE return TriG. Pre-written queries in `queries/` can be run via
`GET /api/queries/{name}` (e.g. `GET /api/queries/list_stocks.sparql`).
The name must include the `.sparql` extension.

### Export RDF

```text
GET /api/export?format=trig
```

Supported formats: `trig` (default), `nq`, `ttl`, `nt`. An optional
`graph=<IRI>` parameter exports only that named graph.

### Graph projection (for the G6 visualization)

```text
GET /api/graph?maxDaysPerStock=30&maxNews=50&maxRelationships=200
```

Returns the JSON projection consumed by the G6 frontend at `/index.html`.
Omitted parameters fall back to the configured `Limits:*` defaults.

## Configuration

The `StockGraph.Web` app reads settings from `appsettings.json`, environment
variables, or CLI arguments (standard .NET configuration precedence —
last provider wins). Note that the checked-in `appsettings.json` only contains
`Logging`/`AllowedHosts`; the `Data:*` and `Limits:*` values below are the
in-code defaults and take effect unless you configure them externally.

| Key | Default | Purpose |
| --- | --- | --- |
| `Data:SourcePath` | `Financial-Knowledge-Graphs` | Directory containing the input CSV data |
| `Data:StorePath` | `.oxigraph/financial_kg` | Persistent Oxigraph store directory |
| `Data:QueryDirectory` | `queries` | Directory of pre-written `.sparql` files |
| `Limits:MaxPriceRows` | `5000` | Default max stock price rows per build |
| `Limits:MaxNewsRows` | `1000` | Default max news rows per build |
| `Limits:MaxDaysPerStock` | `30` | Default graph projection days per stock |
| `Limits:MaxNews` | `50` | Default graph projection news items |
| `Limits:MaxRelationships` | `200` | Default graph projection relationship cap |

> **Note:** the Web app stores all data in the default graph — the graph-IRI
> setting is parsed but not currently applied, because the current Oxigraph
> binding has no FROM / FROM NAMED support, so `FinancialGraphBuilder` is
> created without a graph IRI. Likewise, the configured chunk-size limit is
> parsed into `AppSettings.DefaultChunkSize` but is not applied by the build
> endpoint, which hardcodes a default of 10,000 (overridable per request via
> `chunkSize=`).

Examples of external configuration:

```powershell
# Environment variables (double underscore = section separator)
$env:Data__SourcePath = "E:\data\Financial-Knowledge-Graphs"
$env:Limits__MaxPriceRows = "10000"

# CLI arguments
dotnet run --project src/StockGraph.Web/StockGraph.Web.csproj -- --Data:SourcePath "E:\data\Financial-Knowledge-Graphs" --Limits:MaxPriceRows 10000
```

Relative paths such as `Data:SourcePath` and `Data:QueryDirectory` are resolved
relative to the repository root (five levels up from the build output).

## Windows Store Lock Limitation

The persistent store is backed by RocksDB, which takes an exclusive lock on the
store directory. **Do not run multiple Web processes against the same
`Data:StorePath` on Windows** — a second process against the same store is
unsupported and may fail at startup or during request handling. Run one
instance at a time, or give each instance its own `Data:StorePath`.

Within a single process, concurrent build requests are serialized by an
in-process build lock; a build request that collides with an in-flight build
returns HTTP 409 ("Storage is currently locked by another build operation").

## Research Resources

- [美股披露规则书籍与网站资源](docs/sec_disclosure_rules_resources.md)

## Data Inputs

The builder loads the CSV files currently present in `Financial-Knowledge-Graphs\data`:

- `*.XSHE.csv` / `*.XSHG.csv` stock price files
- `latest_news.csv`

It also supports optional CSVs from the original notebooks if they are later restored:

- `stock_basic.csv`
- `holders.csv` / `stock_holders.csv`
- `concept.csv`
- `stock_concept.csv`
- `sh.csv`
- `sz.csv`
- `corr.csv`
- `financial_data\notices\*.csv`

Missing optional inputs are reported and skipped.
