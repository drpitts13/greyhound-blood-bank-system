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

    /// <summary>
    /// When a lot's latest QC failed, the results to review are those recorded with it after its last passed QC
    /// before that failure (all of them when it never passed). Null when the latest QC passed or there is none.
    /// </summary>
    public static DateTime? ReviewSinceUtc(IEnumerable<ReagentDailyQc> lotQc, out bool failed)
    {
        var rows = lotQc.OrderBy(q => q.PerformedUtc).ThenBy(q => q.Id).ToList();
        var latest = rows.LastOrDefault();
        failed = latest is { Passed: false };
        if (!failed)
        {
            return null;
        }

        return rows.LastOrDefault(q => q.Passed && q.PerformedUtc < latest!.PerformedUtc)?.PerformedUtc ?? DateTime.MinValue;
    }

    public const string NoOpenLotCode = "RES-REAGENT-NO-LOT";
    public const string QcNotPassedCode = "RES-REAGENT-QC";

    /// <summary>
    /// Information for the person entering a manual result, never a stop: a reagent used for the test has no open lot
    /// (so the result records none for it), or a recorded lot has no passed QC today. The Reagent QC worklist and the
    /// facility policy that can block resulting are unchanged.
    /// </summary>
    public static IReadOnlyList<RuleResult> EntryWarnings(
        string? testCode,
        IEnumerable<Reagent> reagents,
        IReadOnlyList<ReagentLot> lotsInUse,
        IReadOnlySet<long> lotsWithPassedQcToday)
    {
        var results = new List<RuleResult>();
        foreach (var reagent in reagents.Where(r => UsedFor(r, testCode)).OrderBy(r => r.Code, StringComparer.OrdinalIgnoreCase))
        {
            var lots = lotsInUse.Where(l => l.ReagentId == reagent.Id).ToList();
            if (lots.Count == 0)
            {
                results.Add(RuleResult.Warning(NoOpenLotCode,
                    $"No open lot of {reagent.Code} is recorded for {testCode!.Trim().ToUpperInvariant()}, so this result records no {reagent.Code} lot. Open the lot in use on Reagent QC."));
                continue;
            }

            foreach (var lot in lots.Where(l => !lotsWithPassedQcToday.Contains(l.Id)))
            {
                results.Add(RuleResult.Warning(QcNotPassedCode, $"{reagent.Code} lot {lot.LotNumber} has no passed QC recorded today."));
            }
        }

        return results;
    }

    public static IReadOnlyList<ReagentLot> LotsInUse(string? testCode, IEnumerable<Reagent> reagents, IEnumerable<ReagentLot> lots, DateTime nowUtc)
    {
        var reagentIds = reagents.Where(r => UsedFor(r, testCode)).Select(r => r.Id).ToHashSet();
        return lots
            .Where(l => reagentIds.Contains(l.ReagentId) && l.State == ReagentLotState.Opened && l.ExpiresUtc > nowUtc)
            .OrderBy(l => l.ReagentId).ThenBy(l => l.LotNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
