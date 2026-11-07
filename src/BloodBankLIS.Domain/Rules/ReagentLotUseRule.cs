using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Which reagent lots a result was tested with: the open, unexpired lots of every active reagent that lists the
/// result's test. Recorded when a manual result is entered so a lot that later fails QC can be traced to its results.
/// When two lots of one reagent are open, both are recorded (the bench may have used either).
/// </summary>
public static class ReagentLotUseRule
{
    public static IReadOnlyList<string> ParseTestCodes(string? codes) =>
        (codes ?? string.Empty)
            .Split([',', ';', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>The list as stored: upper-case codes separated by ", ", or null when empty.</summary>
    public static string? NormalizeTestCodes(string? codes)
    {
        var parsed = ParseTestCodes(codes);
        return parsed.Count == 0 ? null : string.Join(", ", parsed);
    }

    public static bool UsedFor(Reagent reagent, string? testCode) =>
        reagent.IsActive
        && !string.IsNullOrWhiteSpace(testCode)
        && ParseTestCodes(reagent.TestCodes).Contains(testCode.Trim().ToUpperInvariant());

    public static IReadOnlyList<ReagentLot> LotsInUse(string? testCode, IEnumerable<Reagent> reagents, IEnumerable<ReagentLot> lots, DateTime nowUtc)
    {
        var reagentIds = reagents.Where(r => UsedFor(r, testCode)).Select(r => r.Id).ToHashSet();
        return lots
            .Where(l => reagentIds.Contains(l.ReagentId) && l.State == ReagentLotState.Opened && l.ExpiresUtc > nowUtc)
            .OrderBy(l => l.ReagentId).ThenBy(l => l.LotNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
