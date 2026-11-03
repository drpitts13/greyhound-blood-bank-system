using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// How many reservations on the ready-to-issue list need a new crossmatch because
/// their only compatible crossmatch has expired, and which rows the board offers for
/// routine issue. Neither changes the reservation or the issue gate.
/// </summary>
public static class ReadyToIssueAttentionRule
{
    public static int CountCrossmatchExpired(IEnumerable<ProductAllocationDisplayStatus> statuses) =>
        statuses.Count(static status => status == ProductAllocationDisplayStatus.CrossmatchExpired);

    /// <summary>Reserved units inside the near-expiry window: issue them first or reserve replacements.</summary>
    public static int CountExpiringSoon(IEnumerable<ReadyToIssueWorkItemDto> rows) =>
        rows.Count(static row => row.ExpiresSoon && !row.UnitOutdated);

    /// <summary>The Ready to issue card note: what needs a new crossmatch and what expires soon, or null.</summary>
    public static string? CardNote(IReadOnlyCollection<ReadyToIssueWorkItemDto> rows)
    {
        var parts = new[]
        {
            DashboardQueueCatalog.AttentionNote(CountCrossmatchExpired(rows.Select(r => r.DisplayStatus)), "needs a new crossmatch", "need a new crossmatch"),
            DashboardQueueCatalog.AttentionNote(CountExpiringSoon(rows), "expires soon", "expire soon")
        }.OfType<string>().ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>
    /// True when the board shows the Issue button: no mark on the row blocks routine issue.
    /// The issue gate still decides at issue.
    /// </summary>
    public static bool CanIssueRoutinely(ReadyToIssueWorkItemDto row) =>
        !row.SpecimenBlocksIssue
        && !row.UnitOutdated
        && string.IsNullOrWhiteSpace(row.SpecialRequirementNotice)
        && !row.AboRhDiscrepancyBlocksIssue
        && !row.NeedsSecondAbo
        && !row.CrossmatchBlocksRoutineIssue;

    /// <summary>Rows that can be issued now come first; each group keeps its order.</summary>
    public static IReadOnlyList<ReadyToIssueWorkItemDto> IssuableFirst(IEnumerable<ReadyToIssueWorkItemDto> rows) =>
        rows.OrderBy(static row => CanIssueRoutinely(row) ? 0 : 1).ToList();

    public static string? Summary(IReadOnlyCollection<ReadyToIssueWorkItemDto> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        var ready = rows.Count(CanIssueRoutinely);
        var blocked = rows.Count - ready;
        return blocked == 0
            ? $"All {rows.Count} can be issued now."
            : $"{ready} can be issued now. {blocked} need attention first and are listed after them.";
    }
}
