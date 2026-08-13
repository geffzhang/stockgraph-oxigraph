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