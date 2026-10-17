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

public readonly record struct ReagentQcLotFact(long LotId, string LotNumber, ReagentLotState State, DateTime ExpiresUtc);

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

        foreach (var row in qcRows)
        {
            if (!row.Passed)
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
