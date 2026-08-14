using System.Globalization;
using System.IO;
using StockGraph.Core.Models;
using StockGraph.Core.Parsing;
using VDS.RDF;
using Oxigraph;
using Oxigraph.Extensions.DotNetRDF;

namespace StockGraph.Core.Services;

public class FinancialGraphBuilder
{
    private readonly OxigraphStoreCoordinator _coordinator;
    private readonly IGraphName _graphName;

    public FinancialGraphBuilder(OxigraphStoreCoordinator coordinator, string? graphIri = null)
    {
        _coordinator = coordinator;
        // oxigraph does not support FROM/FROM NAMED SPARQL clauses, so all quads
        // must be loaded into the default graph for queries to find them.
        _graphName = string.IsNullOrEmpty(graphIri) ? new DefaultGraph() : new NamedNode(graphIri);
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

        var quadBuffer = new List<Oxigraph.Quad>(chunkSize);

        AddSchemaQuads(quadBuffer, _graphName);

        var dataDir = new DirectoryInfo(Path.Combine(dir.FullName, "data"));
        if (dataDir.Exists)
        {
            AddStockPriceFiles(quadBuffer, stats, dataDir, maxPriceRows, chunkSize, _graphName);
            AddNewsQuads(quadBuffer, stats, dataDir, maxNewsRows, chunkSize, _graphName);
        }

        if (quadBuffer.Count > 0)
        {
            _coordinator.AddQuads(quadBuffer);
            stats.Quads += quadBuffer.Count;
            quadBuffer.Clear();
        }

        _coordinator.Flush();
        return stats;
    }

    private void AddSchemaQuads(List<Oxigraph.Quad> buffer, IGraphName graph)
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
            var subject = (INamedOrBlankNode)((INode)new UriNode(Vocabulary.ExTerm(name))).ToOxigraphTerm();
            var pred = (NamedNode)((INode)RdfTermFactory.RdfType).ToOxigraphTerm();
            var obj = (ITerm)((INode)RdfTermFactory.RdfsClass).ToOxigraphTerm();
            buffer.Add(new Oxigraph.Quad(subject, pred, obj, graph));

            var labelPred = (NamedNode)((INode)RdfTermFactory.Label).ToOxigraphTerm();
            var labelLit = (ITerm)((INode)RdfTermFactory.Literals.ZhLabelLiteral(label)).ToOxigraphTerm();
            buffer.Add(new Oxigraph.Quad(subject, labelPred, labelLit, graph));
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
            var subject = (INamedOrBlankNode)((INode)new UriNode(Vocabulary.ExTerm(name))).ToOxigraphTerm();
            var pred = (NamedNode)((INode)RdfTermFactory.RdfType).ToOxigraphTerm();
            var obj = (ITerm)((INode)RdfTermFactory.RdfProperty).ToOxigraphTerm();
            buffer.Add(new Oxigraph.Quad(subject, pred, obj, graph));

            var labelPred = (NamedNode)((INode)RdfTermFactory.Label).ToOxigraphTerm();
            var labelLit = (ITerm)((INode)RdfTermFactory.Literals.ZhLabelLiteral(label)).ToOxigraphTerm();
            buffer.Add(new Oxigraph.Quad(subject, labelPred, labelLit, graph));
        }
    }

    private void AddStockPriceFiles(List<Oxigraph.Quad> buffer, BuildStats stats, DirectoryInfo dataDir, int? maxPriceRows, int chunkSize, IGraphName graph)
    {
        var xshe = dataDir.GetFiles("*.XSHE.csv").OrderBy(f => f.Name).ToList();
        var xshg = dataDir.GetFiles("*.XSHG.csv").OrderBy(f => f.Name).ToList();

        foreach (var file in xshe.Concat(xshg))
        {
            var code = Path.GetFileNameWithoutExtension(file.Name);
            var count = AddStockPriceFile(buffer, file.FullName, code, maxPriceRows, chunkSize, graph);
            stats.AddRows(file.Name, count);
        }

        if (xshe.Count == 0 && xshg.Count == 0)
            stats.SkippedFiles.Add($"{dataDir.FullName}/*.XSHE.csv");
    }

    private int AddStockPriceFile(List<Oxigraph.Quad> buffer, string filePath, string code, int? maxRows, int chunkSize, IGraphName graph)
    {
        var stock = RdfTermFactory.StockNode(code);
        var stockNode = (INamedOrBlankNode)((INode)stock).ToOxigraphTerm();
        var stockTypeUri = new UriNode(Vocabulary.ExTerm("Stock"));
        var stockTypeNode = (ITerm)((INode)stockTypeUri).ToOxigraphTerm();

        AddQuad(buffer, graph, stockNode, RdfTermFactory.RdfType, stockTypeNode);
        AddQuad(buffer, graph, stockNode, RdfTermFactory.Label, (ITerm)((INode)RdfTermFactory.Literals.PlainLiteral(code)).ToOxigraphTerm());
        AddQuad(buffer, graph, stockNode, RdfTermFactory.SecurityCode, (ITerm)((INode)RdfTermFactory.Literals.PlainLiteral(code)).ToOxigraphTerm());
        AddQuad(buffer, graph, stockNode, RdfTermFactory.Exchange, (ITerm)((INode)RdfTermFactory.Literals.PlainLiteral(code.Split('.').Last())).ToOxigraphTerm());

        if (!CsvRecordParser.TryReadCsvWithLimit(filePath, maxRows ?? int.MaxValue, out var allRows, out var headers))
            return 0;

        var headerDict = headers.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i);
        var tradeDateIdx = headerDict.TryGetValue("trade_date", out var tdIdx) ? tdIdx : -1;

        int count = 0;

        foreach (var row in allRows)
        {
            if (tradeDateIdx < 0 || tradeDateIdx >= row.Length)
                continue;

            var tradeDate = row[tradeDateIdx];
            if (string.IsNullOrWhiteSpace(tradeDate)) continue;

            var dayId = NormalizeDateId(tradeDate);
            var day = RdfTermFactory.TradingDayNode(code, dayId);
            var dayNode = (INamedOrBlankNode)((INode)day).ToOxigraphTerm();
            var tradingDayUri = new UriNode(Vocabulary.ExTerm("TradingDay"));
            var tradingDayTypeNode = (ITerm)((INode)tradingDayUri).ToOxigraphTerm();

            AddQuad(buffer, graph, dayNode, RdfTermFactory.RdfType, tradingDayTypeNode);
            AddQuad(buffer, graph, dayNode, RdfTermFactory.OfStock, (ITerm)stockNode);
            AddQuad(buffer, graph, stockNode, RdfTermFactory.HasTradingDay, (ITerm)dayNode);
            AddQuad(buffer, graph, dayNode, RdfTermFactory.TradeDate, (ITerm)((INode)RdfTermFactory.Literals.DateLiteral(dayId)).ToOxigraphTerm());

            for (int i = 0; i < headers.Count && i < row.Length; i++)
            {
                var col = headers[i];
                if (!PriceColumns.IsPriceColumn(col)) continue;
                var val = row[i];
                if (HasValue(val))
                    AddPropertyQuad(buffer, graph, day, col, val);
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

    private void AddNewsQuads(List<Oxigraph.Quad> buffer, BuildStats stats, DirectoryInfo dataDir, int? maxRows, int chunkSize, IGraphName graph)
    {
        var newsFile = dataDir.GetFiles("latest_news.csv").FirstOrDefault();
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
        var effectiveRows = maxRows.HasValue ? allRows.Take(maxRows.Value) : allRows;

        foreach (var row in effectiveRows)
        {
            var timestamp = datetimeIdx >= 0 && datetimeIdx < row.Length ? row[datetimeIdx] ?? "" : "";
            var title = titleIdx >= 0 && titleIdx < row.Length ? row[titleIdx] ?? "" : "";
            var content = contentIdx >= 0 && contentIdx < row.Length ? row[contentIdx] ?? "" : "";

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content)) continue;

            var news = RdfTermFactory.NewsNode(timestamp, count);
            var newsNode = (INamedOrBlankNode)((INode)news).ToOxigraphTerm();
            var newsArticleUri = new UriNode(Vocabulary.ExTerm("NewsArticle"));
            var newsArticleTypeNode = (ITerm)((INode)newsArticleUri).ToOxigraphTerm();

            AddQuad(buffer, graph, newsNode, RdfTermFactory.RdfType, newsArticleTypeNode);

            var labelText = !string.IsNullOrWhiteSpace(title) ? title : content[..Math.Min(80, content.Length)];
            AddQuad(buffer, graph, newsNode, RdfTermFactory.Label, (ITerm)((INode)RdfTermFactory.Literals.ZhLabelLiteral(labelText)).ToOxigraphTerm());

            if (!string.IsNullOrWhiteSpace(title))
                AddQuad(buffer, graph, newsNode, RdfTermFactory.Headline, (ITerm)((INode)RdfTermFactory.Literals.PlainLiteral(title)).ToOxigraphTerm());
            if (!string.IsNullOrWhiteSpace(content))
                AddQuad(buffer, graph, newsNode, RdfTermFactory.ArticleBody, (ITerm)((INode)RdfTermFactory.Literals.PlainLiteral(content)).ToOxigraphTerm());
            if (!string.IsNullOrWhiteSpace(timestamp))
                AddQuad(buffer, graph, newsNode, RdfTermFactory.DatePublished, (ITerm)((INode)RdfTermFactory.Literals.DateTimeLiteral(timestamp)).ToOxigraphTerm());

            count++;
            if (buffer.Count >= chunkSize)
            {
                _coordinator.AddQuads(buffer);
                buffer.Clear();
            }
        }

        stats.AddRows(newsFile.Name, count);
    }

    private void AddQuad(List<Oxigraph.Quad> buffer, IGraphName graph, INamedOrBlankNode subject, IUriNode pred, ITerm obj)
    {
        buffer.Add(new Oxigraph.Quad(subject, (NamedNode)((INode)pred).ToOxigraphTerm(), obj, graph));
    }

    private void AddPropertyQuad(List<Oxigraph.Quad> buffer, IGraphName graph, IUriNode subject, string propName, string value)
    {
        var camel = ToCamelCase(propName);
        var pred = new UriNode(Vocabulary.ExTerm(camel));
        ILiteralNode lit;
        if (int.TryParse(value, out var i))
            lit = RdfTermFactory.Literals.IntLiteral(i);
        else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            lit = RdfTermFactory.Literals.DecimalLiteral(d);
        else
            lit = RdfTermFactory.Literals.StringLiteral(value);
        var subjNode = (INamedOrBlankNode)((INode)subject).ToOxigraphTerm();
        buffer.Add(new Oxigraph.Quad(subjNode, (NamedNode)((INode)pred).ToOxigraphTerm(), (ITerm)((INode)lit).ToOxigraphTerm(), graph));
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