using VDS.RDF;

namespace StockGraph.Core;

public static class RdfTermFactory
{
    // RDF 类类型
    public static IUriNode RdfType => new UriNode(Vocabulary.RdfTerm("type"));
    public static IUriNode RdfsClass => new UriNode(Vocabulary.RdfsTerm("Class"));
    public static IUriNode RdfProperty => new UriNode(Vocabulary.RdfTerm("Property"));

    // StockGraph 扩展谓词
    public static IUriNode HasTradingDay => new UriNode(Vocabulary.ExTerm("hasTradingDay"));
    public static IUriNode HasNews => new UriNode(Vocabulary.ExTerm("hasNews"));
    public static IUriNode Holds => new UriNode(Vocabulary.ExTerm("holds"));
    public static IUriNode BelongsToConcept => new UriNode(Vocabulary.ExTerm("belongsToConcept"));
    public static IUriNode PublishedAnnouncement => new UriNode(Vocabulary.ExTerm("publishedAnnouncement"));
    public static IUriNode MemberOf => new UriNode(Vocabulary.ExTerm("memberOf"));
    public static IUriNode CorrelatedWith => new UriNode(Vocabulary.ExTerm("correlatedWith"));
    public static IUriNode Exchange => new UriNode(Vocabulary.ExTerm("exchange"));
    public static IUriNode SecurityCode => new UriNode(Vocabulary.ExTerm("securityCode"));
    public static IUriNode OfStock => new UriNode(Vocabulary.ExTerm("ofStock"));
    public static IUriNode TradeDate => new UriNode(Vocabulary.ExTerm("tradeDate"));
    public static IUriNode TsCode => new UriNode(Vocabulary.ExTerm("tsCode"));
    public static IUriNode Symbol => new UriNode(Vocabulary.ExTerm("symbol"));
    public static IUriNode Name => new UriNode(Vocabulary.ExTerm("name"));
    public static IUriNode Industry => new UriNode(Vocabulary.ExTerm("industry"));
    public static IUriNode HoldAmount => new UriNode(Vocabulary.ExTerm("holdAmount"));
    public static IUriNode HoldRatio => new UriNode(Vocabulary.ExTerm("holdRatio"));
    public static IUriNode ConceptCode => new UriNode(Vocabulary.ExTerm("conceptCode"));
    public static IUriNode ConceptName => new UriNode(Vocabulary.ExTerm("conceptName"));
    public static IUriNode HolderName => new UriNode(Vocabulary.ExTerm("holderName"));
    public static IUriNode AnnouncementDate => new UriNode(Vocabulary.ExTerm("announcementDate"));
    public static IUriNode SourceStock => new UriNode(Vocabulary.ExTerm("sourceStock"));
    public static IUriNode TargetStock => new UriNode(Vocabulary.ExTerm("targetStock"));
    public static IUriNode Correlation => new UriNode(Vocabulary.ExTerm("correlation"));

    // schema.org 谓词
    public static IUriNode Headline => new UriNode(Vocabulary.SchemaTerm("headline"));
    public static IUriNode ArticleBody => new UriNode(Vocabulary.SchemaTerm("articleBody"));
    public static IUriNode DatePublished => new UriNode(Vocabulary.SchemaTerm("datePublished"));
    public static IUriNode Label => new UriNode(Vocabulary.RdfsTerm("label"));

    // 实体节点工厂
    public static IUriNode StockNode(string code) =>
        new UriNode(Vocabulary.ExTerm($"stock/{SafeSegment(code)}"));

    public static IUriNode TradingDayNode(string code, string dayId) =>
        new UriNode(Vocabulary.ExTerm($"stock/{SafeSegment(code)}/day/{SafeSegment(dayId)}"));

    public static IUriNode NewsNode(string? timestamp, int index) =>
        new UriNode(Vocabulary.ExTerm($"news/{SafeSegment(timestamp ?? $"row-{index}")}"));

    public static IUriNode ShareholderNode(string name, int index) =>
        new UriNode(Vocabulary.ExTerm($"shareholder/{SafeSegment(name)}-{index}"));

    public static IUriNode ConceptNode(string id) =>
        new UriNode(Vocabulary.ExTerm($"concept/{SafeSegment(id)}"));

    public static IUriNode MarketConnectNode(string market) =>
        new UriNode(Vocabulary.ExTerm($"market-connect/{SafeSegment(market)}"));

    public static IUriNode CorrelationNode(string left, string right) =>
        new UriNode(Vocabulary.ExTerm($"correlation/{SafeSegment(left)}/{SafeSegment(right)}"));

    public static IUriNode AnnouncementNode(string code, string? dateValue, int index) =>
        new UriNode(Vocabulary.ExTerm($"stock/{SafeSegment(code)}/announcement/{SafeSegment(dateValue ?? $"row-{index}")}"));

    // 字面量工厂
    public static LiteralFactory Literals { get; } = new();

    public static string SafeSegment(string value)
        => Uri.EscapeDataString(value.Trim().Replace("/", "_"));

    private static string NormalizeDateId(string value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 8 && text.All(char.IsDigit))
            return $"{text[..4]}-{text[4..6]}-{text[6..8]}";
        return text;
    }

    public class LiteralFactory
    {
        public ILiteralNode DateLiteral(string value)
        {
            var normalized = NormalizeDateId(value);
            return new LiteralNode(normalized, Vocabulary.XsdTerm("date"));
        }

        public ILiteralNode DateTimeLiteral(string value)
        {
            if (DateTime.TryParse(value.Trim(), out var dt))
                return new LiteralNode(dt.ToString("yyyy-MM-ddTHH:mm:ss"), Vocabulary.XsdTerm("dateTime"));
            return new LiteralNode(value.Trim());
        }

        public ILiteralNode IntLiteral(int value) => new LiteralNode(value.ToString(), Vocabulary.XsdTerm("integer"));
        public ILiteralNode DecimalLiteral(double value) => new LiteralNode(value.ToString("G", System.Globalization.CultureInfo.InvariantCulture), Vocabulary.XsdTerm("decimal"));
        public ILiteralNode StringLiteral(string? value) => new LiteralNode(value ?? "");
        public ILiteralNode BoolLiteral(bool value) => new LiteralNode(value.ToString().ToLower(), Vocabulary.XsdTerm("boolean"));
        public ILiteralNode ZhLabelLiteral(string value) => new LiteralNode(value, "zh", false);
    }
}