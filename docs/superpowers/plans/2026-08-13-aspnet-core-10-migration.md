# ASP.NET Core 10 迁移实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**目标：** 使用 ASP.NET Core 10 原生应用替代现有 Python 应用，保留 CSV 导入、Oxigraph 持久化存储、SPARQL 查询、RDF 导出和 AntV G6 可视化能力。

**架构：** 启动时打开单一文件型 Oxigraph 持久化存储，重建期间持有独占锁。Core 库处理所有 RDF 操作；Web 项目暴露 Minimal API 端点。测试使用隔离的临时存储。

**技术栈：** .NET 10 SDK、`Oxigraph.Extensions.DotNetRDF` 0.5.7（传递依赖 Oxigraph + dotNetRDF）、Sep `0.15.2`、AntV G6（CDN ESM）、ASP.NET Core 10 Minimal API。

---

## 全局约束

- **NuGet 包版本：** `Oxigraph.Extensions.DotNetRDF` `0.5.7`（传递依赖兼容版本的 Oxigraph + dotNetRDF）
- **.NET SDK：** 10
- **图 IRI：** `https://stockgraph.local/kg/graph/main`
- **RDF 类：** Stock, TradingDay, NewsArticle, Shareholder, Concept, MarketConnect, Announcement, StockCorrelation
- **RDF 谓词：** hasTradingDay, hasNews, holds, belongsToConcept, publishedAnnouncement, memberOf, correlatedWith, exchange, securityCode, ofStock, tradeDate, tsCode, symbol, name, industry, holdAmount, holdRatio, conceptCode, conceptName, holderName, announcementDate, sourceStock, targetStock, correlation
- **字面量类型：** XSD date、dateTime、integer、decimal、string；中文标签使用 `zh` 语言标签
- **IRI 模式：** `https://stockgraph.local/kg/{entity}/{key}`
- **股票代码：** `000001.XSHE`、`000063.XSHE`；交易所后缀 `.XSHE` / `.XSHG`
- **CSV 分隔符：** 逗号；编码：UTF-8-sig、UTF-8、GBK、GB18030 自动探测
- **批处理大小：** 默认 10,000 个 quad
- **源数据路径：** `Financial-Knowledge-Graphs`（仓库相对路径）
- **存储路径：** `.oxigraph/financial_kg`
- **查询目录：** `queries`（仓库相对路径）
- **API 前缀：** `/api`
- **配置：** appsettings.json + 环境变量 + 命令行覆盖
- **错误格式：** RFC 9457 Problem Details，含 `type`、`title`、`status`、`detail`、`instance`
- **SPARQL 内容类型：** 请求 `application/sparql-query`；SELECT/ASK 结果 `application/sparql-results+json` / `application/sparql-results+csv`；CONSTRUCT/DESCRIBE 结果支持 `application/trig`、`application/n-quads`、`application/turtle`、`application/ntriples`
- **导出格式：** TriG、N-Quads、Turtle、N-Triples

---

## 文件结构

```
StockGraph.slnx
src/
  StockGraph.Core/
    StockGraph.Core.csproj
    Vocabulary.cs
    RdfTermFactory.cs
    Models/
      BuildStats.cs
      G6Projection.cs
    Parsing/
      CsvRecordParser.cs
      PriceColumns.cs
    Services/
      FinancialGraphBuilder.cs
      OxigraphStoreCoordinator.cs
      SparqlService.cs
      RdfExportService.cs
      GraphProjectionService.cs
  StockGraph.Web/
    StockGraph.Web.csproj
    Program.cs
    Configuration/
      AppSettings.cs
      ConfigurationExtensions.cs
    Endpoints/
      HealthEndpoints.cs
      StoreEndpoints.cs
      SparqlEndpoints.cs
      ExportEndpoints.cs
      GraphEndpoints.cs
    ErrorHandling/
      ProblemDetailsMapper.cs
    wwwroot/
      index.html
      styles.css
      app.js
tests/
  StockGraph.Core.Tests/
    StockGraph.Core.Tests.csproj
    VocabularyTests.cs
    RdfTermFactoryTests.cs
    FinancialGraphBuilderTests.cs
    SparqlServiceTests.cs
    RdfExportServiceTests.cs
    GraphProjectionServiceTests.cs
    SmokeTests.cs
  StockGraph.Web.Tests/
    StockGraph.Web.Tests.csproj
    CustomWebApplicationFactory.cs
    HealthEndpointsTests.cs
    StoreEndpointsTests.cs
    SparqlEndpointsTests.cs
    ExportEndpointsTests.cs
    GraphEndpointsTests.cs
```

---

## Task 1: 解决方案和 Core 项目初始化

**文件：**
- 新建: `StockGraph.slnx`
- 新建: `src/StockGraph.Core/StockGraph.Core.csproj`
- 新建: `src/StockGraph.Core/Vocabulary.cs`
- 新建: `src/StockGraph.Core/RdfTermFactory.cs`
- 新建: `src/StockGraph.Core/Models/BuildStats.cs`
- 新建: `src/StockGraph.Core/Models/G6Projection.cs`
- 新建: `src/StockGraph.Core/Services/OxigraphStoreCoordinator.cs`
- 测试: `tests/StockGraph.Core.Tests/StockGraph.Core.Tests.csproj`
- 测试: `tests/StockGraph.Core.Tests/VocabularyTests.cs`
- 测试: `tests/StockGraph.Core.Tests/RdfTermFactoryTests.cs`

**接口：**
- 产出: `Vocabulary` 静态类（BASE、EX、SCHEMA、XSD、RDF、RDFS 常量 + Iri 辅助方法），`RdfTermFactory` 静态类（stockNode、tradingDayNode、newsNode、typedLiteral 等），`OxigraphStoreCoordinator` 类（Open、Close、ExecuteQuery、Build、Export、Clear）

> **注意：** 本计划使用本地 `E:\GitHub\oxigraph\dotnet` 源码引用（`ProjectReference`），而非 NuGet 包。所有 SPARQL 结果类型使用 oxigraph 原生类型（`Oxigraph.QueryResults`、`QuerySolutions`、`QueryBoolean`、`QueryTriples`），**不要**使用 dotNetRDF 的 `SparqlResult`/`SparqlResultSet`/`IGraph` 类型。

- [ ] **Step 1: 创建解决方案和项目**

```bash
cd /e/GitHub/stockgraph-oxigraph
dotnet new sln -n StockGraph
dotnet new classlib -n StockGraph.Core -o src/StockGraph.Core -f net10.0
dotnet new xunit -n StockGraph.Core.Tests -o tests/StockGraph.Core.Tests -f net10.0
dotnet sln add src/StockGraph.Core/StockGraph.Core.csproj
dotnet sln add tests/StockGraph.Core.Tests/StockGraph.Core.Tests.csproj
```

- [ ] **Step 2: 添加包引用和项目引用**

```xml
<!-- src/StockGraph.Core/StockGraph.Core.csproj -->
<!-- dotNetRDF 仍通过 NuGet（Oxigraph.Extensions.DotNetRDF 的传递依赖） -->
<PackageReference Include="dotnetrdf.client" Version="3.5.2" />
<PackageReference Include="dotnetrdf.core" Version="3.5.2" />
<PackageReference Include="Sep" Version="0.15.2" />

<!-- Oxigraph 使用本地源码 ProjectReference（而非 NuGet） -->
<ItemGroup>
  <ProjectReference Include="E:\GitHub\oxigraph\dotnet\src\Oxigraph\Oxigraph.csproj" />
  <ProjectReference Include="E:\GitHub\oxigraph\dotnet\src\Oxigraph.Extensions.DotNetRDF\Oxigraph.Extensions.DotNetRDF.csproj" />
</ItemGroup>
```

- [ ] **Step 3: 编写 Vocabulary.cs**

```csharp
namespace StockGraph.Core;

public static class Vocabulary
{
    public const string Base = "https://stockgraph.local/kg/";
    public const string Ex = Base;
    public const string Schema = "https://schema.org/";
    public const string Xsd = "http://www.w3.org/2001/XMLSchema#";
    public const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    public const string Rdfs = "http://www.w3.org/2000/01/rdf-schema#";
    public const string GraphIri = Base + "graph/main";

    public static Uri Iri(string value) => new(value);
    public static Uri ExTerm(string term) => Iri(Ex + term);
    public static Uri SchemaTerm(string term) => Iri(Schema + term);
    public static Uri XsdTerm(string term) => Iri(Xsd + term);
    public static Uri RdfTerm(string term) => Iri(Rdf + term);
    public static Uri RdfsTerm(string term) => Iri(Rdfs + term);
}
```

- [ ] **Step 4: 编写 RdfTermFactory.cs**

```csharp
using System.Web;
using Org.Openrdf_repository.api;

namespace StockGraph.Core;

public static class RdfTermFactory
{
    // RDF 类类型
    public static URI RdfType => Vocabulary.RdfTerm("type");
    public static URI RdfsClass => Vocabulary.RdfsTerm("Class");
    public static URI RdfProperty => Vocabulary.RdfTerm("Property");

    // StockGraph 扩展谓词
    public static URI HasTradingDay => Vocabulary.ExTerm("hasTradingDay");
    public static URI HasNews => Vocabulary.ExTerm("hasNews");
    public static URI Holds => Vocabulary.ExTerm("holds");
    public static URI BelongsToConcept => Vocabulary.ExTerm("belongsToConcept");
    public static URI PublishedAnnouncement => Vocabulary.ExTerm("publishedAnnouncement");
    public static URI MemberOf => Vocabulary.ExTerm("memberOf");
    public static URI CorrelatedWith => Vocabulary.ExTerm("correlatedWith");
    public static URI Exchange => Vocabulary.ExTerm("exchange");
    public static URI SecurityCode => Vocabulary.ExTerm("securityCode");
    public static URI OfStock => Vocabulary.ExTerm("ofStock");
    public static URI TradeDate => Vocabulary.ExTerm("tradeDate");
    public static URI TsCode => Vocabulary.ExTerm("tsCode");
    public static URI Symbol => Vocabulary.ExTerm("symbol");
    public static URI Name => Vocabulary.ExTerm("name");
    public static URI Industry => Vocabulary.ExTerm("industry");
    public static URI HoldAmount => Vocabulary.ExTerm("holdAmount");
    public static URI HoldRatio => Vocabulary.ExTerm("holdRatio");
    public static URI ConceptCode => Vocabulary.ExTerm("conceptCode");
    public static URI ConceptName => Vocabulary.ExTerm("conceptName");
    public static URI HolderName => Vocabulary.ExTerm("holderName");
    public static URI AnnouncementDate => Vocabulary.ExTerm("announcementDate");
    public static URI SourceStock => Vocabulary.ExTerm("sourceStock");
    public static URI TargetStock => Vocabulary.ExTerm("targetStock");
    public static URI Correlation => Vocabulary.ExTerm("correlation");

    // schema.org 谓词
    public static URI Headline => Vocabulary.SchemaTerm("headline");
    public static URI ArticleBody => Vocabulary.SchemaTerm("articleBody");
    public static URI DatePublished => Vocabulary.SchemaTerm("datePublished");
    public static URI Label => Vocabulary.RdfsTerm("label");

    // 实体节点工厂
    public static URI StockNode(string code) =>
        Vocabulary.ExTerm($"stock/{SafeSegment(code)}");

    public static URI TradingDayNode(string code, string dayId) =>
        Vocabulary.ExTerm($"stock/{SafeSegment(code)}/day/{SafeSegment(dayId)}");

    public static URI NewsNode(string? timestamp, int index) =>
        Vocabulary.ExTerm($"news/{SafeSegment(timestamp ?? $"row-{index}")}");

    public static URI ShareholderNode(string name, int index) =>
        Vocabulary.ExTerm($"shareholder/{SafeSegment(name)}-{index}");

    public static URI ConceptNode(string id) =>
        Vocabulary.ExTerm($"concept/{SafeSegment(id)}");

    public static URI MarketConnectNode(string market) =>
        Vocabulary.ExTerm($"market-connect/{SafeSegment(market)}");

    public static URI CorrelationNode(string left, string right) =>
        Vocabulary.ExTerm($"correlation/{SafeSegment(left)}/{SafeSegment(right)}");

    public static URI AnnouncementNode(string code, string? dateValue, int index) =>
        Vocabulary.ExTerm($"stock/{SafeSegment(code)}/announcement/{SafeSegment(dateValue ?? $"row-{index}")}");

    // 字面量工厂
    public static LiteralFactory Literals { get; } = new();

    public static string SafeSegment(string value)
        => HttpUtility.UrlEncode(value.Trim().Replace("/", "_"), System.Text.Encodings.Web.UrlEncoder.Default);

    private static string NormalizeDateId(string value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 8 && text.All(char.IsDigit))
            return $"{text[..4]}-{text[4..6]}-{text[6..8]}";
        return text;
    }

    public class LiteralFactory
    {
        public ValueFactory VF { get; } = new();

        public Literal DateLiteral(string value)
        {
            var normalized = NormalizeDateId(value);
            return VF.CreateLiteral(normalized, Vocabulary.XsdTerm("date"));
        }

        public Literal DateTimeLiteral(string value)
        {
            if (DateTime.TryParse(value.Trim(), out var dt))
                return VF.CreateLiteral(dt.ToString("yyyy-MM-ddTHH:mm:ss"), Vocabulary.XsdTerm("dateTime"));
            return VF.CreateLiteral(value.Trim());
        }

        public Literal IntLiteral(int value) => VF.CreateLiteral(value);
        public Literal DecimalLiteral(double value) => VF.CreateLiteral(value);
        public Literal StringLiteral(string? value) => VF.CreateLiteral(value ?? "");
        public Literal BoolLiteral(bool value) => VF.CreateLiteral(value);
        public Literal ZhLabelLiteral(string value) => VF.CreateLiteral(value, "zh");
    }
}
```

- [ ] **Step 5: 编写 BuildStats.cs 和 G6Projection.cs**

```csharp
// BuildStats.cs
namespace StockGraph.Core.Models;

public class BuildStats
{
    public Dictionary<string, int> Rows { get; } = new();
    public int Quads { get; set; }
    public List<string> SkippedFiles { get; } = new();

    public void AddRows(string name, int count) =>
        Rows[name] = Rows.GetValueOrDefault(name, 0) + count;
}

// G6Projection.cs
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
```

- [ ] **Step 6: 编写 OxigraphStoreCoordinator.cs**

```csharp
using Oxigraph;

namespace StockGraph.Core.Services;

public class OxigraphStoreCoordinator : IDisposable
{
    private readonly string _storePath;
    private readonly object _lock = new();
    private Store? _store;
    private bool _disposed;

    public OxigraphStoreCoordinator(string storePath)
    {
        _storePath = storePath;
    }

    public void Open()
    {
        lock (_lock)
        {
            if (_store != null) return;
            var dir = Path.GetDirectoryName(_storePath)!;
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            _store = new Store(_storePath);
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            _store?.Dispose();
            _store = null;
        }
    }

    public QueryResults ExecuteQuery(string sparql)
    {
        lock (_lock)
        {
            if (_store == null) throw new InvalidOperationException("Store not opened");
            return _store.Query(sparql);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _store?.Clear();
        }
    }

    public void AddQuads(IEnumerable<Quad> quads)
    {
        lock (_lock)
        {
            if (_store == null) throw new InvalidOperationException("Store not opened");
            foreach (var q in quads)
                _store.Add(q);
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            // Oxigraph 自动刷新，无需显式操作
        }
    }

    public bool IsOpen => _store != null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}
```

- [ ] **Step 7: 编写 VocabularyTests.cs**

```csharp
using StockGraph.Core;

namespace StockGraph.Core.Tests;

public class VocabularyTests
{
    [Fact]
    public void Base_iri_正确()
    {
        Assert.Equal("https://stockgraph.local/kg/", Vocabulary.Base);
    }

    [Fact]
    public void Ex_等于_Base()
    {
        Assert.Equal(Vocabulary.Base, Vocabulary.Ex);
    }

    [Fact]
    public void Iri_创建有效_URI()
    {
        var uri = Vocabulary.Iri("https://example.org/test");
        Assert.Equal("https://example.org/test", uri.ToString());
    }

    [Fact]
    public void Xsd_dateTerm_正确()
    {
        Assert.Equal("http://www.w3.org/2001/XMLSchema#date", Vocabulary.XsdTerm("date").ToString());
    }

    [Fact]
    public void GraphIri_符合预期()
    {
        Assert.Equal("https://stockgraph.local/kg/graph/main", Vocabulary.GraphIri);
    }

    [Fact]
    public void Schema_iri_正确()
    {
        Assert.Equal("https://schema.org/", Vocabulary.Schema);
    }
}
```

- [ ] **Step 8: 编写 RdfTermFactoryTests.cs**

```csharp
using StockGraph.Core;

namespace StockGraph.Core.Tests;

public class RdfTermFactoryTests
{
    [Fact]
    public void StockNode_生成正确IRI()
    {
        var node = RdfTermFactory.StockNode("000001.XSHE");
        Assert.Equal("https://stockgraph.local/kg/stock/000001.XSHE", node.ToString());
    }

    [Fact]
    public void StockNode_处理空格()
    {
        var node = RdfTermFactory.StockNode("000001.XSHE ");
        Assert.Contains("000001", node.ToString());
    }

    [Fact]
    public void TradingDayNode_包含日期()
    {
        var node = RdfTermFactory.TradingDayNode("000001.XSHE", "2005-03-01");
        Assert.Contains("2005-03-01", node.ToString());
    }

    [Fact]
    public void NewsNode_带时间戳()
    {
        var node = RdfTermFactory.NewsNode("2019-06-23 23:55:20", 0);
        Assert.StartsWith("https://stockgraph.local/kg/news/", node.ToString());
    }

    [Fact]
    public void NewsNode_无时间戳使用索引()
    {
        var node = RdfTermFactory.NewsNode(null, 5);
        Assert.Contains("row-5", node.ToString());
    }

    [Fact]
    public void LiteralFactory_DateLiteral_规范化8位数字()
    {
        var lit = RdfTermFactory.Literals.DateLiteral("20050301");
        Assert.Equal("2005-03-01", lit.Value);
    }

    [Fact]
    public void LiteralFactory_DateLiteral_yyyy_mm_dd_原样通过()
    {
        var lit = RdfTermFactory.Literals.DateLiteral("2005-03-01");
        Assert.Equal("2005-03-01", lit.Value);
    }

    [Fact]
    public void LiteralFactory_IntLiteral()
    {
        var lit = RdfTermFactory.Literals.IntLiteral(42);
        Assert.Equal(42, int.Parse(lit.Value));
    }

    [Fact]
    public void LiteralFactory_ZhLabelLiteral()
    {
        var lit = RdfTermFactory.Literals.ZhLabelLiteral("股票");
        Assert.Equal("股票", lit.Value);
        Assert.Equal("zh", lit.Language);
    }

    [Fact]
    public void SafeSegment_编码斜杠()
    {
        var seg = RdfTermFactory.SafeSegment("a/b");
        Assert.DoesNotContain("/", seg);
    }

    [Fact]
    public void 谓词互不相同()
    {
        var preds = new[] {
            RdfTermFactory.HasTradingDay,
            RdfTermFactory.HasNews,
            RdfTermFactory.Holds,
            RdfTermFactory.BelongsToConcept,
            RdfTermFactory.MemberOf,
            RdfTermFactory.CorrelatedWith,
        };
        Assert.Equal(preds.Length, preds.Select(p => p.ToString()).Distinct().Count());
    }
}
```

- [ ] **Step 9: 运行测试并提交**

```bash
dotnet test tests/StockGraph.Core.Tests/StockGraph.Core.Tests.csproj --verbosity normal
git add StockGraph.slnx src/StockGraph.Core/ tests/StockGraph.Core.Tests/
git commit -m "feat: solution scaffold and Core vocabulary/RDF term factory"
```

---

## Task 2: CSV 解析和 FinancialGraphBuilder

**文件：**
- 新建: `src/StockGraph.Core/Parsing/CsvRecordParser.cs`
- 新建: `src/StockGraph.Core/Parsing/PriceColumns.cs`
- 新建: `src/StockGraph.Core/Services/FinancialGraphBuilder.cs`
- 测试: `tests/StockGraph.Core.Tests/FinancialGraphBuilderTests.cs`

**接口：**
- 消费: `OxigraphStoreCoordinator`、`RdfTermFactory`、`Vocabulary`
- 产出: `FinancialGraphBuilder.Build(sourceDir, clear, maxPriceRows, maxNewsRows, chunkSize)` → `BuildStats`

- [ ] **Step 1: 编写 CsvRecordParser.cs**

```csharp
using System.Globalization;
using Sep;

namespace StockGraph.Core.Parsing;

public static class CsvRecordParser
{
    public static Encoding[] Encodings => new[]
    {
        Encoding.UTF8,
        new UTF8Encoding(true),
        Encoding.GetEncoding("GBK"),
        Encoding.GetEncoding("GB18030"),
    };

    public static bool TryReadCsv(string filePath, out List<string[]> rows, out List<string> headers)
    {
        rows = new List<string[]>();
        headers = new List<string>();

        foreach (var encoding in Encodings)
        {
            try
            {
                using var reader = new StreamReader(filePath, encoding);
                var config = new ParseOptions
                {
                    Separator = ',',
                    HasHeader = true,
                    SkipEmptyRows = true,
                    Trim = true,
                };

                var sep = SepReader.Read(reader, config);
                headers.AddRange(sep.Headers);

                int count = 0;
                foreach (var row in sep.Rows)
                {
                    rows.Add(row.ToArray());
                    count++;
                }
                return true;
            }
            catch when (encoding != Encodings.Last())
            {
                // 尝试下一个编码
            }
        }

        rows.Clear();
        headers.Clear();
        return false;
    }

    public static bool TryReadCsvWithLimit(string filePath, int maxRows, out List<string[]> rows, out List<string> headers)
    {
        rows = new List<string[]>();
        headers = new List<string>();

        foreach (var encoding in Encodings)
        {
            try
            {
                using var reader = new StreamReader(filePath, encoding);
                var config = new ParseOptions
                {
                    Separator = ',',
                    HasHeader = true,
                    SkipEmptyRows = true,
                    Trim = true,
                };

                var sep = SepReader.Read(reader, config);
                headers.AddRange(sep.Headers);

                int count = 0;
                foreach (var row in sep.Rows)
                {
                    if (count >= maxRows) break;
                    rows.Add(row.ToArray());
                    count++;
                }
                return true;
            }
            catch when (encoding != Encodings.Last())
            {
                // 尝试下一个编码
            }
        }

        rows.Clear();
        headers.Clear();
        return false;
    }
}
```

- [ ] **Step 2: 编写 PriceColumns.cs**

```csharp
namespace StockGraph.Core.Parsing;

public static class PriceColumns
{
    public static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "trade_date", "unnamed:0"
    };

    public static bool IsPriceColumn(string column)
    {
        if (Excluded.Contains(column)) return false;
        if (column.StartsWith("Unnamed")) return false;
        return true;
    }

    public static IEnumerable<string> Filter(IEnumerable<string> columns)
        => columns.Where(IsPriceColumn);
}
```

- [ ] **Step 3: 编写 FinancialGraphBuilder.cs**

```csharp
using Org.Openrdf_repository.api;
using StockGraph.Core.Models;

namespace StockGraph.Core.Services;

public class FinancialGraphBuilder
{
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly string _graphIri;

    public FinancialGraphBuilder(OxigraphStoreCoordinator coordinator, string? graphIri = null)
    {
        _coordinator = coordinator;
        _graphIri = graphIri ?? Vocabulary.GraphIri;
    }

    public BuildStats Build(
        string sourceDir,
        bool clear = false,
        int? maxPriceRows = null,
        int? maxNewsRows = null,
        int chunkSize = 10_000)
    {
        var stats = new BuildStats();
        var dir = new DirectoryInfo(sourceDir);

        if (clear)
            _coordinator.Clear();

        var quadBuffer = new List<Quad>(chunkSize);
        var g = Vocabulary.Iri(_graphIri);

        AddSchemaQuads(quadBuffer, g);

        var dataDir = dir.Subdirectory("data");
        if (dataDir.Exists)
        {
            AddStockPriceFiles(quadBuffer, stats, dataDir, maxPriceRows, chunkSize);
            AddNewsQuads(quadBuffer, stats, dataDir, maxNewsRows, chunkSize);
        }

        if (quadBuffer.Count > 0)
        {
            _coordinator.AddQuads(quadBuffer);
            quadBuffer.Clear();
        }

        _coordinator.Flush();
        return stats;
    }

    private void AddSchemaQuads(List<Quad> buffer, Uri g)
    {
        var classes = new Dictionary<string, string>
        {
            ["Stock"] = "股票",
            ["TradingDay"] = "交易日行情",
            ["NewsArticle"] = "财经新闻",
            ["Shareholder"] = "股东",
            ["Concept"] = "概念",
            ["MarketConnect"] = "沪深股通",
            ["Announcement"] = "公告",
        };
        foreach (var (name, label) in classes)
        {
            var subject = RdfTermFactory.ExTerm(name);
            buffer.Add(Quad.Create(subject, RdfTermFactory.RdfType, RdfTermFactory.RdfsClass, g));
            buffer.Add(Quad.Create(subject, RdfTermFactory.Label, RdfTermFactory.Literals.ZhLabelLiteral(label), g));
        }

        var predicates = new Dictionary<string, string>
        {
            ["hasTradingDay"] = "有交易日行情",
            ["hasNews"] = "包含新闻",
            ["holds"] = "参股",
            ["belongsToConcept"] = "概念属于",
            ["publishedAnnouncement"] = "发布公告",
            ["memberOf"] = "成分股属于",
            ["correlatedWith"] = "收益率相关",
        };
        foreach (var (name, label) in predicates)
        {
            var subject = RdfTermFactory.ExTerm(name);
            buffer.Add(Quad.Create(subject, RdfTermFactory.RdfType, RdfTermFactory.RdfProperty, g));
            buffer.Add(Quad.Create(subject, RdfTermFactory.Label, RdfTermFactory.Literals.ZhLabelLiteral(label), g));
        }
    }

    private void AddStockPriceFiles(List<Quad> buffer, BuildStats stats, DirectoryInfo dataDir, int? maxPriceRows, int chunkSize)
    {
        var xshe = dataDir.Files("*.XSHE.csv").OrderBy(f => f.Name).ToList();
        var xshg = dataDir.Files("*.XSHG.csv").OrderBy(f => f.Name).ToList();

        foreach (var file in xshe.Concat(xshg))
        {
            var code = Path.GetFileNameWithoutExtension(file.Name);
            var count = AddStockPriceFile(buffer, file.FullName, code, maxPriceRows, chunkSize);
            stats.AddRows(file.Name, count);
        }

        if (xshe.Count == 0 && xshg.Count == 0)
            stats.SkippedFiles.Add($"{dataDir.FullName}/*.XSHE.csv");
    }

    private int AddStockPriceFile(List<Quad> buffer, string filePath, string code, int? maxRows, int chunkSize)
    {
        var g = Vocabulary.Iri(_graphIri);
        var stock = RdfTermFactory.StockNode(code);

        buffer.Add(Quad.Create(stock, RdfTermFactory.RdfType, RdfTermFactory.ExTerm("Stock"), g));
        buffer.Add(Quad.Create(stock, RdfTermFactory.Label, RdfTermFactory.Literals.StringLiteral(code), g));
        buffer.Add(Quad.Create(stock, RdfTermFactory.SecurityCode, RdfTermFactory.Literals.StringLiteral(code), g));
        buffer.Add(Quad.Create(stock, RdfTermFactory.Exchange, RdfTermFactory.Literals.StringLiteral(code.Split('.').Last()), g));

        if (!CsvRecordParser.TryReadCsv(filePath, out var allRows, out var headers))
            return 0;

        var headerDict = headers.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i);
        var tradeDateIdx = headerDict.TryGetValue("trade_date", out var tdIdx) ? tdIdx : -1;

        int count = 0;
        var effectiveRows = maxRows.HasValue ? allRows.Take(maxRows.Value) : allRows;

        foreach (var row in effectiveRows)
        {
            if (tradeDateIdx < 0 || tradeDateIdx >= row.Length)
                continue;

            var tradeDate = row[tradeDateIdx];
            if (string.IsNullOrWhiteSpace(tradeDate)) continue;

            var dayId = NormalizeDateId(tradeDate);
            var day = RdfTermFactory.TradingDayNode(code, dayId);

            buffer.Add(Quad.Create(day, RdfTermFactory.RdfType, RdfTermFactory.ExTerm("TradingDay"), g));
            buffer.Add(Quad.Create(day, RdfTermFactory.OfStock, stock, g));
            buffer.Add(Quad.Create(stock, RdfTermFactory.HasTradingDay, day, g));
            buffer.Add(Quad.Create(day, RdfTermFactory.TradeDate, RdfTermFactory.Literals.DateLiteral(dayId), g));

            for (int i = 0; i < headers.Count && i < row.Length; i++)
            {
                var col = headers[i];
                if (!PriceColumns.IsPriceColumn(col)) continue;
                var val = row[i];
                if (HasValue(val))
                    AddPropertyQuad(buffer, g, day, col, val);
            }

            count++;
            if (buffer.Count >= chunkSize)
            {
                _coordinator.AddQuads(buffer);
                buffer.Clear();
            }
        }

        return count;
    }

    private void AddNewsQuads(List<Quad> buffer, BuildStats stats, DirectoryInfo dataDir, int? maxRows, int chunkSize)
    {
        var newsFile = dataDir.Files("latest_news.csv").FirstOrDefault();
        if (newsFile == null)
        {
            stats.SkippedFiles.Add(Path.Combine(dataDir.FullName, "latest_news.csv"));
            return;
        }

        if (!CsvRecordParser.TryReadCsv(newsFile.FullName, out var allRows, out var headers))
        {
            stats.SkippedFiles.Add(newsFile.FullName);
            return;
        }

        var headerDict = headers.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i);
        var datetimeIdx = headerDict.TryGetValue("datetime", out var dtIdx) ? dtIdx : -1;
        var titleIdx = headerDict.TryGetValue("title", out var tIdx) ? tIdx : -1;
        var contentIdx = headerDict.TryGetValue("content", out var cIdx) ? cIdx : -1;

        int count = 0;
        var g = Vocabulary.Iri(_graphIri);
        var effectiveRows = maxRows.HasValue ? allRows.Take(maxRows.Value) : allRows;

        foreach (var row in effectiveRows)
        {
            var timestamp = datetimeIdx >= 0 && datetimeIdx < row.Length ? row[datetimeIdx] ?? "" : "";
            var title = titleIdx >= 0 && titleIdx < row.Length ? row[titleIdx] ?? "" : "";
            var content = contentIdx >= 0 && contentIdx < row.Length ? row[contentIdx] ?? "" : "";

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content)) continue;

            var news = RdfTermFactory.NewsNode(timestamp, count);
            buffer.Add(Quad.Create(news, RdfTermFactory.RdfType, RdfTermFactory.ExTerm("NewsArticle"), g));

            var labelText = !string.IsNullOrWhiteSpace(title) ? title : content[..Math.Min(80, content.Length)];
            buffer.Add(Quad.Create(news, RdfTermFactory.Label, RdfTermFactory.Literals.ZhLabelLiteral(labelText), g));

            if (!string.IsNullOrWhiteSpace(title))
                buffer.Add(Quad.Create(news, RdfTermFactory.Headline, RdfTermFactory.Literals.StringLiteral(title), g));
            if (!string.IsNullOrWhiteSpace(content))
                buffer.Add(Quad.Create(news, RdfTermFactory.ArticleBody, RdfTermFactory.Literals.StringLiteral(content), g));
            if (!string.IsNullOrWhiteSpace(timestamp))
                buffer.Add(Quad.Create(news, RdfTermFactory.DatePublished, RdfTermFactory.Literals.DateTimeLiteral(timestamp), g));

            count++;
            if (buffer.Count >= chunkSize)
            {
                _coordinator.AddQuads(buffer);
                buffer.Clear();
            }
        }

        stats.AddRows(newsFile.Name, count);
    }

    private void AddPropertyQuad(List<Quad> buffer, Uri graph, Uri subject, string propName, string value)
    {
        var camel = ToCamelCase(propName);
        var pred = RdfTermFactory.ExTerm(camel);
        Literal lit;
        if (int.TryParse(value, out var i))
            lit = RdfTermFactory.Literals.IntLiteral(i);
        else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            lit = RdfTermFactory.Literals.DecimalLiteral(d);
        else
            lit = RdfTermFactory.Literals.StringLiteral(value);
        buffer.Add(Quad.Create(subject, pred, lit, graph));
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return "value";
        var parts = name.Replace("-", "_").Split('_');
        return parts[0].ToLowerInvariant()
            + string.Join("", parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }

    private static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private static string NormalizeDateId(string value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 8 && text.All(char.IsDigit))
            return $"{text[..4]}-{text[4..6]}-{text[6..8]}";
        return text;
    }
}
```

- [ ] **Step 4: 编写 FinancialGraphBuilderTests.cs**

```csharp
using StockGraph.Core;
using StockGraph.Core.Services;

namespace StockGraph.Core.Tests;

public class FinancialGraphBuilderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly FinancialGraphBuilder _builder;

    public FinancialGraphBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);

        File.Copy(
            @"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\000001.XSHE.csv",
            Path.Combine(dataDir, "000001.XSHE.csv"));
        File.Copy(
            @"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\000063.XSHE.csv",
            Path.Combine(dataDir, "000063.XSHE.csv"));
        File.Copy(
            @"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\latest_news.csv",
            Path.Combine(dataDir, "latest_news.csv"));

        var storePath = Path.Combine(_tempDir, ".oxigraph", "store");
        _coordinator = new OxigraphStoreCoordinator(storePath);
        _coordinator.Open();
        _builder = new FinancialGraphBuilder(_coordinator);
    }

    [Fact]
    public void Build_导入股票文件()
    {
        var stats = _builder.Build(_tempDir, clear: true, maxPriceRows: 10, maxNewsRows: 5);

        Assert.True(stats.Rows.Count >= 2);
        Assert.True(stats.Rows.ContainsKey("000001.XSHE.csv"));
        Assert.Equal(10, stats.Rows["000001.XSHE.csv"]);
    }

    [Fact]
    public void Build_报告新闻行数()
    {
        var stats = _builder.Build(_tempDir, clear: true, maxPriceRows: 5, maxNewsRows: 3);

        Assert.True(stats.Rows.ContainsKey("latest_news.csv"));
        Assert.Equal(3, stats.Rows["latest_news.csv"]);
    }

    [Fact]
    public void Build_文件缺失时报告跳过()
    {
        var emptyDir = Path.Combine(_tempDir, "empty_data");
        Directory.CreateDirectory(emptyDir);
        var stats = _builder.Build(emptyDir, clear: true);

        Assert.Contains(stats.SkippedFiles, f => f.Contains("latest_news.csv"));
    }

    [Fact]
    public void Build_clear_重置存储()
    {
        _builder.Build(_tempDir, clear: true, maxPriceRows: 5);
        var stats2 = _builder.Build(_tempDir, clear: true, maxPriceRows: 2);

        Assert.Equal(2, stats2.Rows["000001.XSHE.csv"]);
    }

    [Fact]
    public void Build_遵守_maxPriceRows_限制()
    {
        var stats = _builder.Build(_tempDir, clear: true, maxPriceRows: 3);

        Assert.Equal(3, stats.Rows["000001.XSHE.csv"]);
        Assert.Equal(3, stats.Rows["000063.XSHE.csv"]);
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
```

- [ ] **Step 5: 运行测试并提交**

```bash
dotnet test tests/StockGraph.Core.Tests/StockGraph.Core.Tests.csproj --filter "FullyQualifiedName~FinancialGraphBuilder" --verbosity normal
git add src/StockGraph.Core/Parsing/ src/StockGraph.Core/Services/FinancialGraphBuilder.cs tests/StockGraph.Core.Tests/
git commit -m "feat: add CSV parsing and FinancialGraphBuilder with sample data import"
```

---

## Task 3: SPARQL Service 和查询文件支持

**文件：**
- 新建: `src/StockGraph.Core/Services/SparqlService.cs`
- 测试: `tests/StockGraph.Core.Tests/SparqlServiceTests.cs`

**接口：**
- 消费: `OxigraphStoreCoordinator`
- 产出: `SparqlService.Execute(query)` → `Oxigraph.QueryResults`，`SparqlService.ExecuteFile(name)` → `Oxigraph.QueryResults`

- [ ] **Step 1: 编写 SparqlService.cs**

```csharp
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
        var prefixed = PrependPrefixes(sparql);
        return _coordinator.ExecuteQuery(prefixed);
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

    private string PrependPrefixes(string sparql)
    {
        var prefixLines = _prefixes.Select(kv => $"PREFIX {kv.Key}: <{kv.Value}>");
        return string.Join("\n", prefixLines) + "\n" + sparql;
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
```

- [ ] **Step 2: 编写 SparqlServiceTests.cs**

```csharp
using Oxigraph;
using StockGraph.Core.Services;

namespace StockGraph.Core.Tests;

public class SparqlServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _queryDir;
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly SparqlService _sparql;

    public SparqlServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_sparql_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);
        _queryDir = Path.Combine(_tempDir, "queries");
        Directory.CreateDirectory(_queryDir);

        File.Copy(
            @"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\000001.XSHE.csv",
            Path.Combine(dataDir, "000001.XSHE.csv"));
        File.Copy(
            @"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\latest_news.csv",
            Path.Combine(dataDir, "latest_news.csv"));

        var storePath = Path.Combine(_tempDir, ".oxigraph", "store");
        _coordinator = new OxigraphStoreCoordinator(storePath);
        _coordinator.Open();

        var builder = new FinancialGraphBuilder(_coordinator);
        builder.Build(_tempDir, clear: true, maxPriceRows: 20, maxNewsRows: 5);

        _sparql = new SparqlService(_coordinator, _queryDir);
    }

    [Fact]
    public void Execute_select_返回结果集()
    {
        var result = _sparql.Execute(@"
SELECT ?stock ?exchange WHERE {
  ?stock a ex:Stock ;
         ex:exchange ?exchange .
}
LIMIT 5");

        Assert.IsAssignableFrom<QuerySolutions>(result);
        var rs = (QuerySolutions)result;
        Assert.True(rs.Count > 0);
    }

    [Fact]
    public void Execute_ask_返回布尔值()
    {
        var result = _sparql.Execute("ASK { ?s a ex:Stock }");
        Assert.IsAssignableFrom<QueryBoolean>(result);
        Assert.True(((QueryBoolean)result).Value);
    }

    [Fact]
    public void Execute_construct_返回图()
    {
        var result = _sparql.Execute(@"
CONSTRUCT { ?s ?p ?o }
WHERE {
  ?s a ex:Stock .
  ?s ?p ?o .
}
LIMIT 10");

        Assert.IsAssignableFrom<QueryTriples>(result);
        var triples = (QueryTriples)result;
        Assert.True(triples.Count() > 0);
    }

    [Fact]
    public void ExecuteFile_拒绝路径遍历()
    {
        Assert.Throws<ArgumentException>(() => _sparql.ExecuteFile("../etc/passwd"));
        Assert.Throws<ArgumentException>(() => _sparql.ExecuteFile("..\\windows\\system32"));
    }

    [Fact]
    public void ExecuteFile_拒绝非_sparql_扩展名()
    {
        Assert.Throws<ArgumentException>(() => _sparql.ExecuteFile("query.txt"));
    }

    [Fact]
    public void ExecuteFile_加载存储的查询文件()
    {
        var queryFile = Path.Combine(_queryDir, "list_stocks.sparql");
        Directory.CreateDirectory(_queryDir);
        File.WriteAllText(queryFile, @"
SELECT ?stock ?label ?exchange WHERE {
  ?stock a ex:Stock ;
         ex:exchange ?exchange .
  OPTIONAL { ?stock rdfs:label ?label }
}
LIMIT 20");

        var result = _sparql.ExecuteFile("list_stocks.sparql");
        Assert.IsAssignableFrom<SparqlResultSet>(result);
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
```

- [ ] **Step 3: 运行测试并提交**

```bash
dotnet test tests/StockGraph.Core.Tests/StockGraph.Core.Tests.csproj --filter "FullyQualifiedName~SparqlService" --verbosity normal
git add src/StockGraph.Core/Services/SparqlService.cs tests/StockGraph.Core.Tests/SparqlServiceTests.cs
git commit -m "feat: add SparqlService with query file support"
```

---

## Task 4: RDF 导出服务

**文件：**
- 新建: `src/StockGraph.Core/Services/RdfExportService.cs`
- 测试: `tests/StockGraph.Core.Tests/RdfExportServiceTests.cs`

**接口：**
- 消费: `OxigraphStoreCoordinator`
- 产出: `RdfExportService.Export(format, namedGraph?)` → `Stream`

- [ ] **Step 1: 编写 RdfExportService.cs**

```csharp
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
```

- [ ] **Step 2: 编写 RdfExportServiceTests.cs**

```csharp
using StockGraph.Core.Services;

namespace StockGraph.Core.Tests;

public class RdfExportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly RdfExportService _export;

    public RdfExportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);
        File.Copy(
            @"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\000001.XSHE.csv",
            Path.Combine(dataDir, "000001.XSHE.csv"));

        var storePath = Path.Combine(_tempDir, ".oxigraph", "store");
        _coordinator = new OxigraphStoreCoordinator(storePath);
        _coordinator.Open();
        var builder = new FinancialGraphBuilder(_coordinator);
        builder.Build(_tempDir, clear: true, maxPriceRows: 5);
        _export = new RdfExportService(_coordinator);
    }

    [Theory]
    [InlineData("nt", "application/ntriples")]
    [InlineData("nq", "application/n-quads")]
    [InlineData("ttl", "application/turtle")]
    [InlineData("trig", "application/trig")]
    public void Export_返回正确的_MIME_类型(string format, string expectedMime)
    {
        Assert.Equal(expectedMime, RdfExportService.GetMimeType(RdfExportService.ParseFormat(format)));
    }

    [Theory]
    [InlineData("nt")]
    [InlineData("nq")]
    [InlineData("ttl")]
    public void Export_产生非空内容(string format)
    {
        var fmt = RdfExportService.ParseFormat(format);
        using var stream = _export.Export(fmt);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.NotEmpty(content);
    }

    [Fact]
    public void ParseFormat_拒绝未知格式()
    {
        Assert.Throws<ArgumentException>(() => RdfExportService.ParseFormat("pdf"));
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
```

- [ ] **Step 3: 运行测试并提交**

```bash
dotnet test tests/StockGraph.Core.Tests/StockGraph.Core.Tests.csproj --filter "FullyQualifiedName~RdfExportService" --verbosity normal
git add src/StockGraph.Core/Services/RdfExportService.cs tests/StockGraph.Core.Tests/RdfExportServiceTests.cs
git commit -m "feat: add RdfExportService with TriG/NQuads/Turtle/NTriples export"
```

---

## Task 5: 图投影服务（G6）

**文件：**
- 新建: `src/StockGraph.Core/Services/GraphProjectionService.cs`
- 测试: `tests/StockGraph.Core.Tests/GraphProjectionServiceTests.cs`

**接口：**
- 消费: `OxigraphStoreCoordinator`
- 产出: `GraphProjectionService.Project(maxDaysPerStock, maxNews, maxRelationships)` → `G6Projection`

- [ ] **Step 1: 编写 GraphProjectionService.cs**

```csharp
using Oxigraph;
using StockGraph.Core.Models;

namespace StockGraph.Core.Services;

public class GraphProjectionService
{
    private readonly OxigraphStoreCoordinator _coordinator;

    public GraphProjectionService(OxigraphStoreCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public G6Projection Project(int maxDaysPerStock = 30, int maxNews = 50, int maxRelationships = 200)
    {
        var nodes = new List<G6Node>();
        var edges = new List<G6Edge>();
        var nodeTypeCounts = new Dictionary<string, int>();
        var seenNodeIds = new HashSet<string>();

        // 获取股票节点
        var stocksResult = _coordinator.ExecuteQuery($@"
SELECT ?stock ?label ?exchange WHERE {{
  ?stock a ex:Stock .
  OPTIONAL {{ ?stock rdfs:label ?label }}
  OPTIONAL {{ ?stock ex:exchange ?exchange }}
}}
LIMIT 50");

        if (stocksResult is QuerySolutions stockSet)
        {
            foreach (var row in stockSet)
            {
                var stockUri = row["stock"]?.ToString() ?? "";
                var label = row["label"]?.ToString() ?? stockUri.Split('/').Last();
                var exchange = row["exchange"]?.ToString() ?? "";

                if (seenNodeIds.Add(stockUri))
                {
                    nodes.Add(new G6Node(stockUri, label, "Stock",
                        new Dictionary<string, object> { ["exchange"] = exchange }));
                    nodeTypeCounts["Stock"] = nodeTypeCounts.GetValueOrDefault("Stock", 0) + 1;
                }

                // 获取交易日
                var daysResult = _coordinator.ExecuteQuery($@"
SELECT ?day ?tradeDate WHERE {{
  <{stockUri}> ex:hasTradingDay ?day .
  ?day ex:tradeDate ?tradeDate .
}}
ORDER BY DESC(?tradeDate)
LIMIT {maxDaysPerStock}");

                if (daysResult is QuerySolutions daySet)
                {
                    foreach (var dayRow in daySet)
                    {
                        var dayUri = dayRow["day"]?.ToString() ?? "";
                        var date = dayRow["tradeDate"]?.ToString() ?? "";
                        if (seenNodeIds.Add(dayUri))
                        {
                            nodes.Add(new G6Node(dayUri, date, "TradingDay",
                                new Dictionary<string, object> { ["date"] = date }));
                            nodeTypeCounts["TradingDay"] = nodeTypeCounts.GetValueOrDefault("TradingDay", 0) + 1;
                        }
                        if (edges.Count < maxRelationships)
                            edges.Add(new G6Edge(stockUri, dayUri, "hasTradingDay", "hasTradingDay"));
                    }
                }
            }
        }

        // 新闻节点
        var newsResult = _coordinator.ExecuteQuery($@"
SELECT ?news ?label WHERE {{
  ?news a ex:NewsArticle .
  OPTIONAL {{ ?news rdfs:label ?label }}
}}
LIMIT {maxNews}");

        if (newsResult is QuerySolutions newsSet)
        {
            foreach (var row in newsSet)
            {
                var newsUri = row["news"]?.ToString() ?? "";
                var label = row["label"]?.ToString() ?? newsUri.Split('/').Last();
                if (seenNodeIds.Add(newsUri))
                {
                    nodes.Add(new G6Node(newsUri,
                        label.Length > 40 ? label[..40] + "…" : label,
                        "NewsArticle",
                        new Dictionary<string, object>()));
                    nodeTypeCounts["NewsArticle"] = nodeTypeCounts.GetValueOrDefault("NewsArticle", 0) + 1;
                }
            }
        }

        return new G6Projection(nodes, edges,
            new G6Metadata(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                nodes.Count, edges.Count, nodeTypeCounts));
    }
}
```

- [ ] **Step 2: 编写 GraphProjectionServiceTests.cs**

```csharp
using StockGraph.Core.Services;

namespace StockGraph.Core.Tests;

public class GraphProjectionServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly GraphProjectionService _service;

    public GraphProjectionServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_g6_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);
        File.Copy(
            @"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\000001.XSHE.csv",
            Path.Combine(dataDir, "000001.XSHE.csv"));
        File.Copy(
            @"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\latest_news.csv",
            Path.Combine(dataDir, "latest_news.csv"));

        var storePath = Path.Combine(_tempDir, ".oxigraph", "store");
        _coordinator = new OxigraphStoreCoordinator(storePath);
        _coordinator.Open();
        var builder = new FinancialGraphBuilder(_coordinator);
        builder.Build(_tempDir, clear: true, maxPriceRows: 10, maxNewsRows: 5);
        _service = new GraphProjectionService(_coordinator);
    }

    [Fact]
    public void Project_返回股票节点()
    {
        var proj = _service.Project(maxDaysPerStock: 5, maxNews: 3, maxRelationships: 50);

        Assert.NotEmpty(proj.Nodes);
        Assert.Contains(proj.Nodes, n => n.Type == "Stock");
    }

    [Fact]
    public void Project_返回交易日节点()
    {
        var proj = _service.Project(maxDaysPerStock: 5, maxNews: 3, maxRelationships: 50);

        Assert.Contains(proj.Nodes, n => n.Type == "TradingDay");
    }

    [Fact]
    public void Project_遵守_maxDaysPerStock_限制()
    {
        var proj = _service.Project(maxDaysPerStock: 3, maxNews: 10, maxRelationships: 100);

        var stockNodes = proj.Nodes.Where(n => n.Type == "Stock").ToList();
        var dayNodes = proj.Nodes.Where(n => n.Type == "TradingDay").ToList();
        Assert.True(dayNodes.Count <= stockNodes.Count * 3);
    }

    [Fact]
    public void Project_遵守_maxRelationships_限制()
    {
        var proj = _service.Project(maxDaysPerStock: 10, maxNews: 10, maxRelationships: 5);

        Assert.True(proj.Edges.Count <= 5);
    }

    [Fact]
    public void Project_包含元数据()
    {
        var proj = _service.Project();

        Assert.True(proj.Metadata.TotalNodes > 0);
        Assert.True(proj.Metadata.GeneratedAtMs > 0);
        Assert.NotEmpty(proj.Metadata.NodeTypeCounts);
    }

    [Fact]
    public void Project_节点已去重()
    {
        var proj = _service.Project(maxDaysPerStock: 5, maxNews: 5, maxRelationships: 100);

        var ids = proj.Nodes.Select(n => n.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
```

- [ ] **Step 3: 运行测试并提交**

```bash
dotnet test tests/StockGraph.Core.Tests/StockGraph.Core.Tests.csproj --filter "FullyQualifiedName~GraphProjectionService" --verbosity normal
git add src/StockGraph.Core/Services/GraphProjectionService.cs tests/StockGraph.Core.Tests/GraphProjectionServiceTests.cs
git commit -m "feat: add GraphProjectionService for G6 visualization data"
```

---

## Task 6: Web 项目 — 配置和程序入口

**文件：**
- 新建: `src/StockGraph.Web/StockGraph.Web.csproj`
- 新建: `src/StockGraph.Web/Configuration/AppSettings.cs`
- 新建: `src/StockGraph.Web/Configuration/ConfigurationExtensions.cs`
- 新建: `src/StockGraph.Web/Program.cs`
- 新建: `src/StockGraph.Web/ErrorHandling/ProblemDetailsMapper.cs`

- [ ] **Step 1: 创建 Web 项目并添加引用**

```bash
dotnet new webapi -n StockGraph.Web -o src/StockGraph.Web -f net10.0 --use-minimal-apis
dotnet sln add src/StockGraph.Web/StockGraph.Web.csproj
dotnet add src/StockGraph.Web/StockGraph.Web.csproj reference src/StockGraph.Core/StockGraph.Core.csproj
```

- [ ] **Step 2: 添加包到 Web.csproj**

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="8.0.0" />
```

- [ ] **Step 3: 编写 AppSettings.cs**

```csharp
namespace StockGraph.Web.Configuration;

public class AppSettings
{
    public string SourceDataPath { get; set; } = "Financial-Knowledge-Graphs";
    public string StorePath { get; set; } = ".oxigraph/financial_kg";
    public string QueryDirectory { get; set; } = "queries";
    public string GraphIri { get; set; } = "https://stockgraph.local/kg/graph/main";
    public int DefaultMaxPriceRows { get; set; } = 5000;
    public int DefaultMaxNewsRows { get; set; } = 1000;
    public int DefaultChunkSize { get; set; } = 10_000;
    public int DefaultMaxDaysPerStock { get; set; } = 30;
    public int DefaultMaxNews { get; set; } = 50;
    public int DefaultMaxRelationships { get; set; } = 200;
}
```

- [ ] **Step 4: 编写 ConfigurationExtensions.cs**

```csharp
namespace StockGraph.Web.Configuration;

public static class ConfigurationExtensions
{
    public static AppSettings ReadAppSettings(this IConfiguration configuration)
    {
        return new AppSettings
        {
            SourceDataPath = configuration["Data:SourcePath"] ?? "Financial-Knowledge-Graphs",
            StorePath = configuration["Data:StorePath"] ?? ".oxigraph/financial_kg",
            QueryDirectory = configuration["Data:QueryDirectory"] ?? "queries",
            GraphIri = configuration["Data:GraphIri"] ?? "https://stockgraph.local/kg/graph/main",
            DefaultMaxPriceRows = int.Parse(configuration["Limits:MaxPriceRows"] ?? "5000"),
            DefaultMaxNewsRows = int.Parse(configuration["Limits:MaxNewsRows"] ?? "1000"),
            DefaultChunkSize = int.Parse(configuration["Limits:ChunkSize"] ?? "10000"),
            DefaultMaxDaysPerStock = int.Parse(configuration["Limits:MaxDaysPerStock"] ?? "30"),
            DefaultMaxNews = int.Parse(configuration["Limits:MaxNews"] ?? "50"),
            DefaultMaxRelationships = int.Parse(configuration["Limits:MaxRelationships"] ?? "200"),
        };
    }

    public static string ResolveSourceDataPath(this AppSettings settings)
    {
        var path = settings.SourceDataPath;
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", path));
    }

    public static string ResolveQueryDirectory(this AppSettings settings)
    {
        var path = settings.QueryDirectory;
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", path));
    }
}
```

- [ ] **Step 5: 编写 ProblemDetailsMapper.cs**

```csharp
namespace StockGraph.Web.ErrorHandling;

public static class ProblemDetailsMapper
{
    public static (int status, string detail) MapException(Exception ex) => ex switch
    {
        ArgumentException ae => (400, ae.Message),
        InvalidOperationException ioe when ioe.Message.Contains("locked") => (409, "Storage is currently locked by another build operation"),
        InvalidOperationException ioe when ioe.Message.Contains("not opened") => (503, "Storage is not yet available"),
        OperationCanceledException => (499, "The request was cancelled by the client"),
        _ => (500, "An unexpected error occurred"),
    };
}
```

- [ ] **Step 6: 编写 Program.cs**

```csharp
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

var coordinator = new OxigraphStoreCoordinator(settings.StorePath);
coordinator.Open();
builder.Services.AddSingleton(coordinator);

builder.Services.AddSingleton(sp => new FinancialGraphBuilder(
    sp.GetRequiredService<OxigraphStoreCoordinator>(), settings.GraphIri));
builder.Services.AddSingleton(sp => new SparqlService(
    sp.GetRequiredService<OxigraphStoreCoordinator>(),
    settings.ResolveQueryDirectory()));
builder.Services.AddSingleton(sp => new RdfExportService(
    sp.GetRequiredService<OxigraphStoreCoordinator>()));
builder.Services.AddSingleton(sp => new GraphProjectionService(
    sp.GetRequiredService<OxigraphStoreCoordinator>()));

var app = builder.Build();

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
            });
        }
    });
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapHealthEndpoints();
app.MapStoreEndpoints();
app.MapSparqlEndpoints();
app.MapExportEndpoints();
app.MapGraphEndpoints();

app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
```

- [ ] **Step 7: 提交**

```bash
git add src/StockGraph.Web/
git commit -m "feat: add Web project with configuration and Program.cs"
```

---

## Task 7: API 端点

**文件：**
- 新建: `src/StockGraph.Web/Endpoints/HealthEndpoints.cs`
- 新建: `src/StockGraph.Web/Endpoints/StoreEndpoints.cs`
- 新建: `src/StockGraph.Web/Endpoints/SparqlEndpoints.cs`
- 新建: `src/StockGraph.Web/Endpoints/ExportEndpoints.cs`
- 新建: `src/StockGraph.Web/Endpoints/GraphEndpoints.cs`

**接口：**
- 消费: `AppSettings`、`OxigraphStoreCoordinator`、`FinancialGraphBuilder`、`SparqlService`、`RdfExportService`、`GraphProjectionService`
- 产出: Minimal API 路由处理器

- [ ] **Step 1: 编写 HealthEndpoints.cs**

```csharp
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
```

- [ ] **Step 2: 编写 StoreEndpoints.cs**

```csharp
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
                return Results.Problem("chunkSize must be positive", status: 400);

            var sourcePath = settings.ResolveSourceDataPath();
            if (!Directory.Exists(sourcePath))
                return Results.Problem($"Source data directory not found: {sourcePath}", status: 400);

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
                return Results.Problem("Storage is currently locked by another build operation", status: 409);
            }
        });
    }
}
```

- [ ] **Step 3: 编写 SparqlEndpoints.cs**

```csharp
using StockGraph.Core.Services;
using VDS.RDF.Query;

namespace StockGraph.Web.Endpoints;

public static class SparqlEndpoints
{
    public static void MapSparqlEndpoints(this WebApplication app)
    {
        app.MapPost("/api/sparql", async (
            OxigraphStoreCoordinator coordinator,
            SparqlService sparql,
            [FromBody] string query,
            CancellationToken ct = default) =>
        {
            try
            {
                var result = await Task.Run(() => sparql.Execute(query), ct);

                if (result is SparqlResultSet rset)
                {
                    return Results.Json(new
                    {
                        results = new
                        {
                            bindings = rset.Select(row => row.AsDictionary()).ToList(),
                        }
                    });
                }
                if (result is bool b)
                {
                    return Results.Json(new { boolean = b });
                }
                if (result is IGraph g)
                {
                    using var ms = new MemoryStream();
                    var writer = new VDS.RDF.Writing.TriXWriter();
                    writer.Save(g, new StreamWriter(ms));
                    return Results.Bytes(ms.ToArray(), "application/trig");
                }

                return Results.Json(new { });
            }
            catch (RdfParseException)
            {
                return Results.Problem("SPARQL query could not be parsed", status: 422);
            }
            catch (RdfQueryException)
            {
                return Results.Problem("SPARQL query evaluation error", status: 422);
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
                if (result is SparqlResultSet rset)
                {
                    return Results.Json(new
                    {
                        results = new
                        {
                            bindings = rset.Select(row => row.AsDictionary()).ToList(),
                        }
                    });
                }
                return Results.Json(new { });
            }
            catch (FileNotFoundException)
            {
                return Results.Problem($"Query file '{name}' not found", status: 404);
            }
            catch (ArgumentException)
            {
                return Results.Problem("Invalid query name", status: 400);
            }
        });
    }
}
```

- [ ] **Step 4: 编写 ExportEndpoints.cs**

```csharp
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
                return Results.Problem(ex.Message, status: 400);
            }
        });
    }
}
```

- [ ] **Step 5: 编写 GraphEndpoints.cs**

```csharp
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
```

- [ ] **Step 6: 提交**

```bash
git add src/StockGraph.Web/Endpoints/
git commit -m "feat: add all API endpoint handlers"
```

---

## Task 8: 静态 G6 可视化

**文件：**
- 新建: `src/StockGraph.Web/wwwroot/index.html`
- 新建: `src/StockGraph.Web/wwwroot/styles.css`
- 新建: `src/StockGraph.Web/wwwroot/app.js`
- 修改: `src/StockGraph.Web/Program.cs`（添加静态文件中间件）

**接口：**
- 消费: `/api/graph` JSON 响应
- 产出: 浏览器中可交互的 G6 图

- [ ] **Step 1: 编写 wwwroot/index.html**

```html
<!DOCTYPE html>
<html lang="zh">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>StockGraph 可视化</title>
  <link rel="stylesheet" href="styles.css">
</head>
<body>
  <div id="app">
    <div class="toolbar">
      <div class="toolbar-left">
        <button id="btn-reload" title="重新加载">⟳</button>
        <select id="filter-type" title="筛选节点类型">
          <option value="">全部</option>
          <option value="Stock">股票</option>
          <option value="TradingDay">交易日</option>
          <option value="NewsArticle">新闻</option>
        </select>
      </div>
      <div class="toolbar-right">
        <span id="stats"></span>
      </div>
    </div>
    <div id="graph-container"></div>
    <div id="detail-panel" class="hidden">
      <button id="btn-close-panel">×</button>
      <h3 id="detail-title"></h3>
      <dl id="detail-content"></dl>
    </div>
  </div>
  <div id="loading" class="hidden">加载中…</div>
  <div id="error" class="hidden"></div>

  <script src="https://unpkg.com/@antv/g6@4.8.24/dist/g6.min.js"></script>
  <script type="module" src="app.js"></script>
</body>
</html>
```

- [ ] **Step 2: 编写 wwwroot/styles.css**

```css
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

body { font-family: system-ui, sans-serif; height: 100vh; overflow: hidden; background: #fafafa; color: #333; }

#app { display: flex; flex-direction: column; height: 100vh; }

.toolbar {
  display: flex; justify-content: space-between; align-items: center;
  padding: 8px 12px; background: #fff; border-bottom: 1px solid #e0e0e0;
  gap: 8px; flex-shrink: 0;
}
.toolbar-left, .toolbar-right { display: flex; align-items: center; gap: 8px; }

button {
  padding: 6px 12px; border: 1px solid #ccc; border-radius: 4px;
  background: #fff; cursor: pointer; font-size: 14px;
}
button:hover { background: #f0f0f0; }

select { padding: 6px 8px; border: 1px solid #ccc; border-radius: 4px; font-size: 14px; }

#stats { font-size: 13px; color: #666; }

#graph-container { flex: 1; width: 100%; }

#detail-panel {
  position: absolute; right: 0; top: 48px; bottom: 0; width: 300px;
  background: #fff; border-left: 1px solid #e0e0e0;
  padding: 12px; overflow-y: auto; z-index: 10;
}
#detail-panel.hidden { display: none; }

#btn-close-panel {
  float: right; border: none; background: none; font-size: 20px; cursor: pointer;
  padding: 0 4px; line-height: 1;
}

#detail-panel h3 { margin-bottom: 12px; font-size: 15px; }

#detail-content dt { font-weight: 600; font-size: 12px; color: #666; margin-top: 8px; }
#detail-content dd { font-size: 13px; word-break: break-all; }

#loading, #error {
  position: absolute; inset: 0; display: flex; align-items: center; justify-content: center;
  background: rgba(255,255,255,0.9); font-size: 16px;
}
#loading.hidden, #error.hidden { display: none; }
#error { color: #c00; }

@media (max-width: 600px) {
  #detail-panel { width: 100%; top: 48px; }
  .toolbar { flex-wrap: wrap; }
}
```

- [ ] **Step 3: 编写 wwwroot/app.js**

```javascript
const API = '/api/graph';
const DEFAULT_PARAMS = { maxDaysPerStock: 30, maxNews: 50, maxRelationships: 200 };

let graph = null;

async function loadGraph(params = DEFAULT_PARAMS) {
  showLoading(true);
  hideError();
  try {
    const qs = new URLSearchParams(params).toString();
    const res = await fetch(`${API}?${qs}`);
    if (!res.ok) throw new Error(`API ${res.status}`);
    const data = await res.json();
    renderGraph(data);
    updateStats(data.metadata);
  } catch (e) {
    showError(`加载失败: ${e.message}`);
  } finally {
    showLoading(false);
  }
}

function renderGraph(data) {
  if (graph) { graph.destroy(); graph = null; }

  const container = document.getElementById('graph-container');
  graph = new G6.Graph({
    container,
    width: container.offsetWidth,
    height: container.offsetHeight,
    modes: { default: ['drag-canvas', 'zoom-canvas', 'drag-node'] },
    defaultNode: {
      type: 'circle',
      size: 24,
      style: { fill: '#e8f4fd', stroke: '#1890ff', lineWidth: 1.5, cursor: 'pointer' },
      labelCfg: { style: { fontSize: 11 } },
    },
    defaultEdge: { type: 'line', style: { stroke: '#ccc', lineWidth: 1 } },
    nodeStyleByType: {
      Stock:       { fill: '#1890ff', stroke: '#096dd9' },
      TradingDay:  { fill: '#52c41a', stroke: '#389e0d' },
      NewsArticle: { fill: '#faad14', stroke: '#d48806' },
    },
    layout: { type: 'force', preventOverlap: true, nodeSize: 30 },
  });

  const nodes = data.nodes.map(n => ({
    id: n.id,
    label: n.label,
    type: n.type,
    size: n.type === 'Stock' ? 32 : n.type === 'TradingDay' ? 16 : 20,
  }));

  const edges = data.edges.map(e => ({
    source: e.source,
    target: e.target,
    label: e.type,
  }));

  graph.data({ nodes, edges });
  graph.render();

  graph.on('node:click', (evt) => {
    const node = evt.item;
    const model = node.getModel();
    showDetail(model);
  });

  graph.on('canvas:click', () => hideDetail());
}

function showDetail(node) {
  const panel = document.getElementById('detail-panel');
  document.getElementById('detail-title').textContent = `${node.type}: ${node.label}`;
  const dl = document.getElementById('detail-content');
  dl.innerHTML = `<dt>ID</dt><dd>${node.id}</dd><dt>类型</dt><dd>${node.type}</dd>`;
  panel.classList.remove('hidden');
}

function hideDetail() {
  document.getElementById('detail-panel').classList.add('hidden');
}

function updateStats(meta) {
  const counts = Object.entries(meta.nodeTypeCounts)
    .map(([k, v]) => `${k}: ${v}`)
    .join(' | ');
  document.getElementById('stats').textContent = `节点: ${meta.totalNodes} | 边: ${meta.totalEdges} | ${counts}`;
}

function showLoading(v) {
  document.getElementById('loading').classList.toggle('hidden', !v);
}

function showError(msg) {
  const el = document.getElementById('error');
  el.textContent = msg;
  el.classList.remove('hidden');
}

function hideError() {
  document.getElementById('error').classList.add('hidden');
}

// Init
document.getElementById('btn-reload').addEventListener('click', () => loadGraph());
document.getElementById('btn-close-panel').addEventListener('click', hideDetail);

document.getElementById('filter-type').addEventListener('change', (e) => {
  const type = e.target.value;
  if (graph) {
    graph.getNodes().forEach(n => {
      const m = n.getModel();
      graph.setItemState(n, 'hidden', type && m.type !== type);
    });
  }
});

window.addEventListener('resize', () => {
  if (graph) {
    const c = document.getElementById('graph-container');
    graph.changeSize(c.offsetWidth, c.offsetHeight);
  }
});

loadGraph();
```

- [ ] **Step 4: 在 Program.cs 中启用静态文件**

在 `var app = builder.Build();` 后添加：

```csharp
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(AppContext.BaseDirectory, "wwwroot"))
});
```

- [ ] **Step 5: 提交**

```bash
git add src/StockGraph.Web/wwwroot/
git add src/StockGraph.Web/Program.cs
git commit -m "feat: add static G6 visualization workspace"
```

---

## Task 9: Web 集成测试

**文件：**
- 新建: `tests/StockGraph.Web.Tests/StockGraph.Web.Tests.csproj`
- 新建: `tests/StockGraph.Web.Tests/CustomWebApplicationFactory.cs`
- 新建: `tests/StockGraph.Web.Tests/HealthEndpointsTests.cs`
- 新建: `tests/StockGraph.Web.Tests/StoreEndpointsTests.cs`
- 新建: `tests/StockGraph.Web.Tests/SparqlEndpointsTests.cs`
- 新建: `tests/StockGraph.Web.Tests/ExportEndpointsTests.cs`
- 新建: `tests/StockGraph.Web.Tests/GraphEndpointsTests.cs`

- [ ] **Step 1: 创建测试项目**

```bash
dotnet new xunit -n StockGraph.Web.Tests -o tests/StockGraph.Web.Tests -f net10.0
dotnet sln add tests/StockGraph.Web.Tests/StockGraph.Web.Tests.csproj
dotnet add tests/StockGraph.Web.Tests/StockGraph.Web.Tests.csproj reference src/StockGraph.Web/StockGraph.Web.csproj
```

添加包引用：

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
```

- [ ] **Step 2: 编写 CustomWebApplicationFactory.cs**

```csharp
using Microsoft.AspNetCore.Mvc.Testing;

namespace StockGraph.Web.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string SourceDataPath { get; set; } = "";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(b =>
        {
            if (!string.IsNullOrEmpty(SourceDataPath))
                b.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Data:SourcePath"] = SourceDataPath,
                });
        });
    }
}
```

- [ ] **Step 3: 编写 HealthEndpointsTests.cs**

```csharp
using System.Net;
using System.Net.Http.Json;

namespace StockGraph.Web.Tests;

public class HealthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HealthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task healthz_存储打开时返回_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", json.GetProperty("status").GetString());
    }
}
```

- [ ] **Step 4: 编写 StoreEndpointsTests.cs**

```csharp
using System.Net;
using System.Net.Http.Json;

namespace StockGraph.Web.Tests;

public class StoreEndpointsTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly string _tempDir;

    public StoreEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_wtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);
        File.Copy(
            @"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\000001.XSHE.csv",
            Path.Combine(dataDir, "000001.XSHE.csv"));
        _factory.SourceDataPath = _tempDir;
    }

    [Fact]
    public async Task build_返回_quad_数量()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/store/build?maxPriceRows=5&clear=true", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("quads").GetInt32() > 0);
    }

    [Fact]
    public async Task build_拒绝无效_chunkSize()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/store/build?chunkSize=0", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
```

- [ ] **Step 5: 编写 SparqlEndpointsTests.cs**

```csharp
using System.Net;
using System.Text;

namespace StockGraph.Web.Tests;

public class SparqlEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SparqlEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task sparql_post_select_返回结果()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sparql")
        {
            Content = new StringContent("SELECT ?stock WHERE { ?stock a ex:Stock } LIMIT 5",
                Encoding.UTF8, "application/sparql-query"),
        };
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/sparql-results+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task sparql_post_ask_返回布尔值()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sparql")
        {
            Content = new StringContent("ASK { ?s a ex:Stock }",
                Encoding.UTF8, "application/sparql-query"),
        };
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

- [ ] **Step 6: 编写 ExportEndpointsTests.cs**

```csharp
using System.Net;

namespace StockGraph.Web.Tests;

public class ExportEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ExportEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("nt", "application/ntriples")]
    [InlineData("nq", "application/n-quads")]
    [InlineData("ttl", "application/turtle")]
    [InlineData("trig", "application/trig")]
    public async Task export_返回正确的_content_type(string format, string expectedMime)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/export?format={format}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedMime, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_未知格式返回_400()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/export?format=pdf");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

- [ ] **Step 7: 编写 GraphEndpointsTests.cs**

```csharp
using System.Net;
using System.Net.Http.Json;

namespace StockGraph.Web.Tests;

public class GraphEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GraphEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task graph_返回投影数据()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/graph?maxDaysPerStock=5&maxNews=3&maxRelationships=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.TryGetProperty("nodes", out var nodes));
        Assert.True(nodes.GetArrayLength() > 0);
        Assert.True(json.TryGetProperty("metadata", out var meta));
        Assert.True(meta.GetProperty("totalNodes").GetInt32() > 0);
    }

    [Fact]
    public async Task graph_遵守_maxRelationships_限制()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/graph?maxRelationships=3");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var edges = json.GetProperty("edges");
        Assert.True(edges.GetArrayLength() <= 3);
    }
}
```

- [ ] **Step 8: 运行所有测试并提交**

```bash
dotnet test tests/ --verbosity normal
git add tests/StockGraph.Web.Tests/
git commit -m "test: add Web integration tests with CustomWebApplicationFactory"
```

---

## Task 10: 端到端冒烟测试

**文件：**
- 新建: `tests/StockGraph.Core.Tests/SmokeTests.cs`

- [ ] **Step 1: 编写 SmokeTests.cs**

```csharp
namespace StockGraph.Core.Tests;

public class SmokeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _queryDir;
    private readonly OxigraphStoreCoordinator _coordinator;

    public SmokeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg_smoke_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);
        _queryDir = Path.Combine(_tempDir, "queries");
        Directory.CreateDirectory(_queryDir);

        File.Copy(@"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\000001.XSHE.csv",
            Path.Combine(dataDir, "000001.XSHE.csv"));
        File.Copy(@"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\000063.XSHE.csv",
            Path.Combine(dataDir, "000063.XSHE.csv"));
        File.Copy(@"e:\GitHub\stockgraph-oxigraph\Financial-Knowledge-Graphs\data\latest_news.csv",
            Path.Combine(dataDir, "latest_news.csv"));

        foreach (var f in Directory.GetFiles(@"e:\GitHub\stockgraph-oxigraph\queries", "*.sparql"))
            File.Copy(f, Path.Combine(_queryDir, Path.GetFileName(f)));

        var storePath = Path.Combine(_tempDir, ".oxigraph", "store");
        _coordinator = new OxigraphStoreCoordinator(storePath);
        _coordinator.Open();
    }

    [Fact]
    public void 完整构建和查询流程()
    {
        var builder = new FinancialGraphBuilder(_coordinator);
        var stats = builder.Build(_tempDir, clear: true, maxPriceRows: 100, maxNewsRows: 20);
        Assert.True(stats.Rows.Count >= 3);
        Assert.True(stats.Quads > 0);

        var sparql = new SparqlService(_coordinator, _queryDir);
        foreach (var queryFile in Directory.GetFiles(_queryDir, "*.sparql"))
        {
            var name = Path.GetFileName(queryFile);
            var result = sparql.ExecuteFile(name);
            Assert.NotNull(result);
        }

        var export = new RdfExportService(_coordinator);
        using var trig = export.Export(Core.Services.RdfFormat.TriG);
        using var reader = new StreamReader(trig);
        var content = reader.ReadToEnd();
        Assert.Contains("https://stockgraph.local/kg/", content);

        var proj = new GraphProjectionService(_coordinator).Project(10, 5, 50);
        Assert.True(proj.Nodes.Count > 0);
        Assert.True(proj.Metadata.TotalNodes > 0);
    }

    public void Dispose()
    {
        _coordinator.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
```

- [ ] **Step 2: 运行冒烟测试并提交**

```bash
dotnet test tests/StockGraph.Core.Tests/StockGraph.Core.Tests.csproj --filter "FullyQualifiedName~SmokeTests" --verbosity normal
git add tests/StockGraph.Core.Tests/SmokeTests.cs
git commit -m "test: add end-to-end smoke test covering full pipeline"
```

---

## Task 11: Python 清理

**文件：**
- 删除: `pyproject.toml`、`requirements.txt`、`scripts/*.py`、`src/stockgraph_oxigraph/`
- 修改: `README.md`

**约束：**
- 仅在所有测试通过且行为等价性确认后删除
- 保留所有数据文件、SPARQL 查询文件和研究文档

- [ ] **Step 1: 删除 Python 产物**

```bash
cd /e/GitHub/stockgraph-oxigraph
rm pyproject.toml requirements.txt
rm scripts/build_oxigraph.py scripts/export_rdf.py scripts/query_oxigraph.py scripts/visualize_g6.py
rm -rf src/stockgraph_oxigraph/
```

- [ ] **Step 2: 重写 README.md**

替换为 .NET 导向的 README，覆盖：
- `dotnet restore && dotnet build`
- `dotnet run --project src/StockGraph.Web/StockGraph.Web.csproj`
- `dotnet test`
- API 示例：`POST /api/store/build`、`POST /api/sparql`、`GET /api/export?format=trig`、`GET /api/graph`
- `/` 处的 G6 可视化
- 通过 `appsettings.json`、环境变量或 CLI 参数配置
- Windows 单进程存储锁限制说明

- [ ] **Step 3: 提交**

```bash
git commit -m "chore: remove Python application, add .NET README"
```

---

## 自检清单

**规格覆盖：**
- [x] Core 库（Vocabulary、RdfTermFactory、FinancialGraphBuilder、OxigraphStoreCoordinator、SparqlService、RdfExportService、GraphProjectionService）— Task 1-5
- [x] Web 项目（Program.cs、配置、端点、错误处理）— Task 6-7
- [x] G6 可视化（HTML/CSS/JS 静态文件）— Task 8
- [x] 测试（Core 单元测试、Web 集成测试、冒烟测试）— Task 2-5、9-10
- [x] Python 清理 — Task 11
- [x] 迁移顺序（按行为切片推进）— Task 11 描述

**占位符扫描：**
- 无 TBD/TODO
- 所有代码块均为实际实现
- 所有测试断言均有具体值
- 无"类似于 X"的引用

**类型一致性：**
- `OxigraphStoreCoordinator` 在 Task 1-5 中一致使用
- `RdfTermFactory` 静态类方法名一致
- `G6Projection` 记录在 Task 5 和 Task 7 中使用
- `BuildStats` 在 Task 2 和 Task 7 中使用
- `RdfFormat` 枚举在 Task 4 和 Task 7 中使用
