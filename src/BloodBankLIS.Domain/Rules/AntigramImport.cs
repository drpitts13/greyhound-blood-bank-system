using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// An antigram file for a panel lot, as a manufacturer's sheet saved in one documented layout: a header row whose first
/// cell is "Cell", an optional "Role" column (Panel, Selected, Autocontrol), and one column per antigen named by its
/// catalog code. Each value is + (present), 0 (absent), w (weak), +het, +hom, or blank for not tested. Title rows above
/// the header are skipped.
///
/// The import fills the New lot form; nothing is saved until the lot is saved there, with its usual checks and audit.
/// A value that cannot be read or a cell number that repeats is a problem that stops the import, so a typing slip never
/// becomes a silent "not tested". A column that names no antigen in the catalog is reported and left out.
/// </summary>
public static class AntigramImport
{
    public const int MaxCells = 60;
    public const string CellColumn = "Cell";
    public const string RoleColumn = "Role";

    public sealed record Antigen(long Id, string Code, string? AntibodyName = null);

    public sealed record CellRow(string CellNumber, PanelCellRole Role, IReadOnlyList<AntigenValue> Antigens);

    public sealed record AntigenValue(long AntigenId, AntigenExpression Expression);

    public sealed record Result(
        IReadOnlyList<CellRow> Cells,
        IReadOnlyList<long> AntigenIds,
        IReadOnlyList<string> Problems,
        IReadOnlyList<string> IgnoredColumns)
    {
        /// <summary>The cells can go into the form: at least one, and nothing that could not be read.</summary>
        public bool CanUse => Problems.Count == 0 && Cells.Count > 0;
    }

    public static Result Parse(IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<Antigen> catalog, PanelCellRole defaultRole = PanelCellRole.Panel)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(catalog);
        var problems = new List<string>();
        var headerIndex = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Count > 0 && IsCellHeading(rows[i][0]))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0)
        {
            return new Result([], [], ["No header row was found: the first column of the header must be \"Cell\"."], []);
        }

        var header = rows[headerIndex];
        var roleColumn = -1;
        var antigenColumns = new List<(int Column, Antigen Antigen)>();
        var ignored = new List<string>();
        for (var c = 1; c < header.Count; c++)
        {
            var name = header[c].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (string.Equals(name, RoleColumn, StringComparison.OrdinalIgnoreCase))
            {
                roleColumn = c;
                continue;
            }

            var antigen = Match(name, catalog);
            if (antigen is null)
            {
                ignored.Add(name);
            }
            else if (antigenColumns.Any(a => a.Antigen.Id == antigen.Id))
            {
                problems.Add($"Column \"{name}\" names {antigen.Code}, which already has a column.");
            }
            else
            {
                antigenColumns.Add((c, antigen));
            }
        }

        if (antigenColumns.Count == 0)
        {
            problems.Add("No column names an antigen in the catalog.");
        }

        var cells = new List<CellRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var r = headerIndex + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var cellNumber = row.Count > 0 ? row[0].Trim() : string.Empty;
            if (cellNumber.Length == 0)
            {
                continue;
            }

            var line = r + 1;
            if (!seen.Add(cellNumber))
            {
                problems.Add($"Line {line}: cell {cellNumber} appears more than once.");
                continue;
            }

            var role = defaultRole;
            if (roleColumn >= 0 && roleColumn < row.Count && row[roleColumn].Trim().Length > 0)
            {
                var parsedRole = ParseRole(row[roleColumn]);
                if (parsedRole is null)
                {
                    problems.Add($"Line {line}: role \"{row[roleColumn].Trim()}\" is not Panel, Selected or Autocontrol.");
                    continue;
                }

                role = parsedRole.Value;
            }

            var values = new List<AntigenValue>();
            if (role != PanelCellRole.Autocontrol)
            {
                foreach (var (column, antigen) in antigenColumns)
                {
                    var raw = column < row.Count ? row[column] : string.Empty;
                    var expression = ParseValue(raw);
                    if (expression is null)
                    {
                        problems.Add($"Line {line}, cell {cellNumber}, {antigen.Code}: \"{raw.Trim()}\" is not +, 0, w, +het, +hom or blank.");
                        continue;
                    }

                    values.Add(new AntigenValue(antigen.Id, expression.Value));
                }
            }

            cells.Add(new CellRow(cellNumber, role, values));
        }

        if (cells.Count == 0 && problems.Count == 0)
        {
            problems.Add("No cells were found below the header.");
        }

        if (cells.Count > MaxCells)
        {
            problems.Add($"The file has {cells.Count} cells; a lot holds at most {MaxCells}.");
        }

        return new Result(cells, antigenColumns.Select(a => a.Antigen.Id).ToList(), problems, ignored);
    }

    /// <summary>The header for an export or a blank template: Cell, Role, then the antigen codes in the order given.</summary>
    public static IReadOnlyList<string> Header(IEnumerable<Antigen> antigens) =>
        [CellColumn, RoleColumn, .. antigens.Select(a => a.Code)];

    /// <summary>One exported row: the cell, its role, and each antigen's value in the header's order.</summary>
    public static IReadOnlyList<string?> Row(string cellNumber, PanelCellRole role, IEnumerable<AntigenExpression> expressions) =>
        [cellNumber, role.ToString(), .. expressions.Select(e => (string?)ValueText(e))];

    public static string ValueText(AntigenExpression expression) => expression switch
    {
        AntigenExpression.Present => "+",
        AntigenExpression.Absent => "0",
        AntigenExpression.Weak => "w",
        AntigenExpression.Heterozygous => "+het",
        AntigenExpression.Homozygous => "+hom",
        _ => string.Empty
    };

    /// <summary>
    /// A typed value. A leading apostrophe (added by spreadsheets and by this system's CSV export to stop a formula) is
    /// ignored. Null when it cannot be read.
    /// </summary>
    public static AntigenExpression? ParseValue(string? raw)
    {
        var text = (raw ?? string.Empty).Trim().TrimStart('\'').Trim().ToLowerInvariant();
        return text switch
        {
            "" or "nt" or "/" => AntigenExpression.NotTested,
            "+" or "pos" or "positive" => AntigenExpression.Present,
            "0" or "-" or "neg" or "negative" => AntigenExpression.Absent,
            "w" or "+w" or "w+" or "weak" => AntigenExpression.Weak,
            "+het" or "het" => AntigenExpression.Heterozygous,
            "+hom" or "hom" => AntigenExpression.Homozygous,
            _ => null
        };
    }

    public static PanelCellRole? ParseRole(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "panel" or "" => PanelCellRole.Panel,
        "selected" or "selected cell" => PanelCellRole.Selected,
        "autocontrol" or "auto control" or "ac" => PanelCellRole.Autocontrol,
        _ => null
    };

    /// <summary>
    /// The catalog antigen a column names. The code exactly first (C and c are different antigens); otherwise, ignoring
    /// case, spaces, parentheses and an "anti-" prefix, the code or the antibody name, but only when exactly one antigen
    /// fits, so "Fy(a)" finds FYA while "c" never finds C.
    /// </summary>
    public static Antigen? Match(string column, IReadOnlyList<Antigen> catalog)
    {
        var exact = catalog.FirstOrDefault(a => string.Equals(a.Code, column, StringComparison.Ordinal));
        if (exact is not null)
        {
            return exact;
        }

        var key = Normalize(column);
        if (key.Length == 0)
        {
            return null;
        }

        var loose = catalog
            .Where(a => Normalize(a.Code) == key || (a.AntibodyName is not null && Normalize(a.AntibodyName) == key))
            .ToList();
        if (loose.Count != 1)
        {
            return null;
        }

        // A one-letter code whose case differs is a different antigen (C vs c, E vs e, K vs k, S vs s): never matched loosely.
        var candidate = loose[0];
        var trimmed = column.Trim();
        if (trimmed.Length == 1 && candidate.Code.Length == 1 && !string.Equals(trimmed, candidate.Code, StringComparison.Ordinal))
        {
            return null;
        }

        return candidate;
    }

    private static bool IsCellHeading(string? text)
    {
        var t = (text ?? string.Empty).Trim().TrimEnd('#', '.').Trim();
        return string.Equals(t, CellColumn, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "Cell no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "Cell number", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("anti-", StringComparison.OrdinalIgnoreCase))
        {
            t = t[5..];
        }

        return new string(t.Where(ch => !char.IsWhiteSpace(ch) && ch != '(' && ch != ')').ToArray()).ToUpperInvariant();
    }
}
