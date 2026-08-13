using System.Globalization;
using System.IO;
using System.Text;
using nietras.SeparatedValues;

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
                var options = new SepReaderOptions
                {
                    HasHeader = true,
                    Trim = SepTrim.Outer,
                };

                var sep = SepReaderExtensions.From(in options, reader);
                headers.AddRange(sep.Header.ToString().Split(','));

                foreach (var row in sep)
                {
                    var arr = new string[row.ColCount];
                    for (int i = 0; i < row.ColCount; i++)
                    {
                        arr[i] = row[i].ToString();
                    }
                    rows.Add(arr);
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
                var options = new SepReaderOptions
                {
                    HasHeader = true,
                    Trim = SepTrim.Outer,
                };

                var sep = SepReaderExtensions.From(in options, reader);
                headers.AddRange(sep.Header.ToString().Split(','));

                int count = 0;
                foreach (var row in sep)
                {
                    if (count >= maxRows) break;
                    var arr = new string[row.ColCount];
                    for (int i = 0; i < row.ColCount; i++)
                    {
                        arr[i] = row[i].ToString();
                    }
                    rows.Add(arr);
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