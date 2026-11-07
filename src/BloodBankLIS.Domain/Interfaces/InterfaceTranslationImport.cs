using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Interfaces;

/// <summary>
/// Reads translation rows pasted from a spreadsheet or loaded from a CSV file: local value, external value, and an
/// optional direction (Both when blank), separated by tabs (a spreadsheet paste) or commas, with an optional header
/// line. Each problem names its line and the line is skipped; nothing here is saved. The rows then go through the same
/// save as rows typed by hand, so value lists, the reason, and the audit still apply.
/// </summary>
public static class InterfaceTranslationImport
{
    public sealed record Row(string LocalValue, string ExternalValue, InterfaceTranslationDirection Direction);

    public sealed record Problem(int Line, string Message);

    public sealed record Result(IReadOnlyList<Row> Rows, IReadOnlyList<Problem> Problems, int Duplicates);

    public static readonly string[] Columns = ["Local value", "External value", "Direction"];

    private static readonly char[] FormulaStarts = ['=', '+', '-', '@'];

    public static Result Parse(string? text)
    {
        var rows = new List<Row>();
        var problems = new List<Problem>();
        var seen = new HashSet<(string, string, InterfaceTranslationDirection)>();
        var duplicates = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Result(rows, problems, 0);
        }

        var lines = SplitLines(text);
        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = Split(line);
            if (rows.Count == 0 && problems.Count == 0 && IsHeader(cells))
            {
                continue;
            }

            var local = Clean(cells.ElementAtOrDefault(0));
            var external = Clean(cells.ElementAtOrDefault(1));
            var directionText = Clean(cells.ElementAtOrDefault(2));
            if (cells.Skip(3).Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                problems.Add(new Problem(number, "More than three columns: expected local value, external value, direction."));
                continue;
            }

            if (local.Length == 0 || external.Length == 0)
            {
                problems.Add(new Problem(number, local.Length == 0 ? "The local value is blank." : "The external value is blank."));
                continue;
            }

            if (ParseDirection(directionText) is not { } direction)
            {
                problems.Add(new Problem(number, $"'{directionText}' is not a direction: use Inbound, Outbound, or Both."));
                continue;
            }

            if (!seen.Add((local.ToUpperInvariant(), external.ToUpperInvariant(), direction)))
            {
                duplicates++;
                continue;
            }

            rows.Add(new Row(local, external, direction));
        }

        return new Result(rows, problems, duplicates);
    }

    /// <summary>An instrument translation row: what the instrument sends, the LIS value, and an optional test limit.</summary>
    public sealed record InstrumentRow(string InstrumentValue, string LocalValue, string? Context);

    public sealed record InstrumentResult(IReadOnlyList<InstrumentRow> Rows, IReadOnlyList<Problem> Problems, int Duplicates);

    public static readonly string[] InstrumentColumns = ["Instrument value", "Local value", "Test limit"];

    /// <summary>
    /// Instrument rows for one field: instrument value, local value, and an optional LIS test the row is limited to.
    /// Same separators, header, and problem reporting as <see cref="Parse"/>.
    /// </summary>
    public static InstrumentResult ParseInstrument(string? text)
    {
        var rows = new List<InstrumentRow>();
        var problems = new List<Problem>();
        var seen = new HashSet<(string, string, string)>();
        var duplicates = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new InstrumentResult(rows, problems, 0);
        }

        var lines = SplitLines(text);
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var cells = Split(lines[i]);
            var first = Clean(cells.ElementAtOrDefault(0)).ToUpperInvariant();
            if (rows.Count == 0 && problems.Count == 0 && (first.Contains("INSTRUMENT", StringComparison.Ordinal) || first.Contains("LOCAL", StringComparison.Ordinal)))
            {
                continue;
            }

            var instrument = Clean(cells.ElementAtOrDefault(0));
            var local = Clean(cells.ElementAtOrDefault(1));
            var context = Clean(cells.ElementAtOrDefault(2));
            if (cells.Skip(3).Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                problems.Add(new Problem(i + 1, "More than three columns: expected instrument value, local value, test limit."));
                continue;
            }

            if (instrument.Length == 0 || local.Length == 0)
            {
                problems.Add(new Problem(i + 1, instrument.Length == 0 ? "The instrument value is blank." : "The local value is blank."));
                continue;
            }

            if (!seen.Add((instrument.ToUpperInvariant(), local.ToUpperInvariant(), context.ToUpperInvariant())))
            {
                duplicates++;
                continue;
            }

            rows.Add(new InstrumentRow(instrument, local, context.Length == 0 ? null : context));
        }

        return new InstrumentResult(rows, problems, duplicates);
    }

    public static IEnumerable<IReadOnlyList<string?>> InstrumentCells(IEnumerable<InstrumentRow> rows) =>
        rows.Select(r => (IReadOnlyList<string?>)new[] { r.InstrumentValue, r.LocalValue, r.Context });

    /// <summary>Blank is Both; also accepts In/Out, I/O/B, and the enum names, in any case.</summary>
    public static InterfaceTranslationDirection? ParseDirection(string? text) => (text ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "" or "BOTH" or "B" or "IN/OUT" or "BIDIRECTIONAL" => InterfaceTranslationDirection.Both,
        "INBOUND" or "IN" or "I" => InterfaceTranslationDirection.Inbound,
        "OUTBOUND" or "OUT" or "O" => InterfaceTranslationDirection.Outbound,
        _ => null
    };

    /// <summary>The rows as cells for <c>CsvFormat.Write</c> with <see cref="Columns"/>, in a form <see cref="Parse"/> reads back.</summary>
    public static IEnumerable<IReadOnlyList<string?>> Cells(IEnumerable<Row> rows) =>
        rows.Select(r => (IReadOnlyList<string?>)new[] { r.LocalValue, r.ExternalValue, r.Direction.ToString() });

    private static bool IsHeader(IReadOnlyList<string> cells)
    {
        var first = Clean(cells.ElementAtOrDefault(0)).ToUpperInvariant();
        var second = Clean(cells.ElementAtOrDefault(1)).ToUpperInvariant();
        return first.Contains("LOCAL", StringComparison.Ordinal) || first.Contains("INTERNAL", StringComparison.Ordinal)
            || second.Contains("EXTERNAL", StringComparison.Ordinal);
    }

    // A CSV export puts an apostrophe before a cell a spreadsheet would run as a formula; take it off again.
    private static string Clean(string? cell)
    {
        var value = (cell ?? string.Empty).Trim();
        return value.Length > 1 && value[0] == '\'' && FormulaStarts.Contains(value[1]) ? value[1..] : value;
    }

    private static string[] SplitLines(string text) =>
        text.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    /// <summary>Tab-separated when the line has a tab (a spreadsheet paste), else comma-separated with quoted cells.</summary>
    private static List<string> Split(string line)
    {
        if (line.Contains('\t', StringComparison.Ordinal))
        {
            return line.Split('\t').ToList();
        }

        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"' && current.ToString().Trim().Length == 0)
            {
                current.Clear();
                quoted = true;
            }
            else if (c == ',')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
