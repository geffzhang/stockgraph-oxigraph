namespace StockGraph.Core.Models;

public class BuildStats
{
    public Dictionary<string, int> Rows { get; } = new();
    public int Quads { get; set; }
    public List<string> SkippedFiles { get; } = new();

    public void AddRows(string name, int count) =>
        Rows[name] = Rows.GetValueOrDefault(name, 0) + count;
}