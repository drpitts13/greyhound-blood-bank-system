using System.Text;

namespace BloodBankLIS.Domain.Reporting;

/// <summary>
/// RFC 4180 CSV with a UTF-8 byte-order mark so spreadsheet programs read accented
/// text. A cell that a spreadsheet would run as a formula (starting with =, +, -, @,
/// tab, or carriage return) gets a leading apostrophe, so exported clinical text is
/// never executed when the file is opened.
/// </summary>
public static class CsvFormat
{
    public const string ContentType = "text/csv; charset=utf-8";

    private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];

    public static byte[] Write(IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        var text = new StringBuilder();
        AppendLine(text, columns);
        foreach (var row in rows)
        {
            AppendLine(text, row);
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(text.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    public static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var safe = FormulaStarts.Contains(value[0]) && !IsPlainNumber(value) ? "'" + value : value;
        var needsQuotes = safe.IndexOfAny([',', '"', '\r', '\n']) >= 0 || safe != safe.Trim();
        return needsQuotes ? "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : safe;
    }

    private static void AppendLine(StringBuilder text, IReadOnlyList<string?> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0)
            {
                text.Append(',');
            }

            text.Append(Cell(cells[i]));
        }

        text.Append("\r\n");
    }

    /// <summary>A negative number such as -18.5 stays a number; "-1+1" or "-cmd" does not.</summary>
    private static bool IsPlainNumber(string value) =>
        decimal.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out _);
}
