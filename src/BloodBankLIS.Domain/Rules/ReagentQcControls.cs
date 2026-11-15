namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A reagent's daily QC controls (end-user testing pass 93: anti-A was QC'd with one reading a day, so nothing showed
/// whether both its positive control, A1 cells, and its negative control, B cells, had been read). The controls are the
/// facility's, from each package insert; none is built in.
/// </summary>
public static class ReagentQcControls
{
    public const string Code = "QC-CONTROL";
    public const int MaxLength = 300;

    public static IReadOnlyList<string> Parse(string? controls) =>
        (controls ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string? Normalize(string? controls) =>
        Parse(controls) is { Count: > 0 } list ? string.Join(", ", list) : null;

    /// <summary>The control as written on the reagent, when the reading names one of them; null when none matches.</summary>
    public static string? Match(string? controls, string? control) =>
        string.IsNullOrWhiteSpace(control)
            ? null
            : Parse(controls).FirstOrDefault(c => string.Equals(c, control.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>A reagent with controls needs each reading to name one of them.</summary>
    public static RuleResult Evaluate(string? controls, string? control)
    {
        var list = Parse(controls);
        if (list.Count == 0 || Match(controls, control) is not null)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(Code, $"Choose which control this reading is for: {string.Join(", ", list)}.");
    }
}
