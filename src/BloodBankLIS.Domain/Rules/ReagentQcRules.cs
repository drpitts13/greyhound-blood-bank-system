using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

public static class ReagentQcReviewRule
{
    public const string Code = "QC-REVIEW";

    public static RuleResult Evaluate(string performedBy, string? reviewer)
    {
        var review = reviewer?.Trim() ?? string.Empty;
        if (review.Length == 0)
        {
            return RuleResult.HardStop(Code, "A reviewer is required.");
        }

        if (string.Equals(performedBy.Trim(), review, StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(Code, "The reviewer must be a different person from the one who performed QC.");
        }

        return RuleResult.Pass(Code);
    }
}

public readonly record struct ReagentQcLotFact(
    long LotId,
    string LotNumber,
    ReagentLotState State,
    DateTime ExpiresUtc,
    long ReagentId = 0,
    string? ReagentCode = null);

public readonly record struct ReagentQcFact(
    long Id,
    long LotId,
    DateTime QcDateUtc,
    bool Passed,
    string? ReviewedBy);

public readonly record struct ReagentQcWorkItem(string Kind, long LotId, long? QcId, string Message);

public static class ReagentQcWorklistRule
{
    public const string Overdue = "Overdue";
    public const string Failed = "Failed";
    public const string Unreviewed = "Unreviewed";
    public const string Expired = "Expired";
    public const string Replace = "Replace";

    /// <summary>A lot whose QC failed and later passed, with results tested in the failed period not yet reviewed.</summary>
    public const string ReviewResults = "ReviewResults";

    public static IReadOnlyList<ReagentQcWorkItem> Evaluate(
        IReadOnlyList<ReagentQcLotFact> lots,
        IReadOnlyList<ReagentQcFact> qcRows,
        DateTime nowUtc)
    {
        var today = nowUtc.Date;
        var items = new List<ReagentQcWorkItem>();
        foreach (var lot in lots)
        {
            if (lot.State == ReagentLotState.Closed)
            {
                continue;
            }

            // An expired lot is closed and replaced, not given daily QC.
            if (lot.ExpiresUtc <= nowUtc)
            {
                items.Add(new ReagentQcWorkItem(
                    Expired,
                    lot.LotId,
                    null,
                    $"Lot {lot.LotNumber} expired {lot.ExpiresUtc:yyyy-MM-dd}: close it and open a new lot. Results do not record an expired lot."));
                continue;
            }

            // An open lot near its expiry with no later lot of the reagent on hand: receive the next lot before it runs out.
            if (lot.State == ReagentLotState.Opened
                && ReagentLotRule.ExpiringSoon(lot.ExpiresUtc, nowUtc)
                && !lots.Any(other => other.LotId != lot.LotId
                                      && other.ReagentId == lot.ReagentId
                                      && other.State != ReagentLotState.Closed
                                      && other.ExpiresUtc > lot.ExpiresUtc))
            {
                var reagent = string.IsNullOrWhiteSpace(lot.ReagentCode) ? "this reagent" : lot.ReagentCode;
                items.Add(new ReagentQcWorkItem(
                    Replace,
                    lot.LotId,
                    null,
                    $"Lot {lot.LotNumber} {ReagentLotRule.ExpiryText(lot.ExpiresUtc, nowUtc)} (expires {lot.ExpiresUtc:yyyy-MM-dd}) and no later lot of {reagent} is received: receive the next lot."));
            }

            var testedToday = qcRows.Any(row => row.LotId == lot.LotId && row.QcDateUtc.Date == today);
            if (!testedToday)
            {
                items.Add(new ReagentQcWorkItem(
                    Overdue,
                    lot.LotId,
                    null,
                    $"Lot {lot.LotNumber} has no QC for {today:yyyy-MM-dd}."));
            }
        }

        // A failure stands while it is the lot's latest QC and the lot is in use; a later pass or closing the lot ends it.
        var openLots = lots.Where(l => l.State != ReagentLotState.Closed).Select(l => l.LotId).ToHashSet();
        var latest = qcRows
            .GroupBy(row => row.LotId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.QcDateUtc).ThenByDescending(r => r.Id).First().Id);
        foreach (var row in qcRows)
        {
            if (!row.Passed && openLots.Contains(row.LotId) && latest[row.LotId] == row.Id)
            {
                items.Add(new ReagentQcWorkItem(Failed, row.LotId, row.Id, "QC failed."));
            }

            if (string.IsNullOrWhiteSpace(row.ReviewedBy))
            {
                items.Add(new ReagentQcWorkItem(Unreviewed, row.LotId, row.Id, "QC has not been reviewed."));
            }
        }

        return items;
    }
}

public static class ReagentQcResultGate
{
    public const string Code = "QC-RESULT-BLOCK";

    public static RuleResult Evaluate(bool policyOn, bool workOpen)
    {
        if (!policyOn || !workOpen)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(
            Code,
            "Patient resulting is blocked while reagent QC is overdue, failed, or unreviewed.");
    }
}
