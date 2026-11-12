using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Results;

/// <summary>
/// A stored result value as staff read it: a titer as its endpoint ("1:32") rather than its stored readings, an ABO/Rh
/// panel as its type and reactions ("O Pos · Anti-A:0 · …") and a subtest panel as its readings rather than the stored
/// JSON (end-user testing, 2026-11-12), and a number with its units ("0.45 %"). Other values are shown as stored.
/// </summary>
public static class ResultValueText
{
    public static string For(string? value, string? units = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        if (TiterResultValue.TryParse(value, out var titer))
        {
            return titer!.EndpointLabel;
        }

        // Only a value that carries an ABO is an ABO/Rh panel: a subtest panel parses as one too, with no type.
        if (value.Contains("\"abo\"", StringComparison.OrdinalIgnoreCase) && AboRhResultValue.TryParsePanel(value, out _))
        {
            return AboRhResultValue.FormatDisplay(value);
        }

        // A plain ABO/Rh value as stored ("O|Positive") reads as its type ("O+"): end-user testing pass 26, the
        // results to review for a failed Anti-D lot showed the stored form.
        if (value.Contains('|') && AboRhResultValue.TryParse(value, out var aboRh))
        {
            return aboRh.ToString();
        }

        if (PanelResultValue.TryParse(value, out var subtests) && subtests.Count > 0)
        {
            return PanelResultValue.FormatDisplay(value);
        }

        return string.IsNullOrWhiteSpace(units) ? value : $"{value} {units.Trim()}";
    }
}
