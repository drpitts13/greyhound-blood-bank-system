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

    /// <summary>
    /// The reservation's own end time has passed. The unit stays held for the patient until someone releases it or
    /// issues it; nothing releases it automatically.
    /// </summary>
    public static bool ReservationLapsed(ReadyToIssueWorkItemDto row, DateTime nowUtc) =>
        row.ReservationExpiresUtc is DateTime until && until <= nowUtc;

    public static int CountReservationLapsed(IEnumerable<ReadyToIssueWorkItemDto> rows, DateTime nowUtc) =>
        rows.Count(row => ReservationLapsed(row, nowUtc));

    /// <summary>
    /// Why a reserved unit is offered for release back to stock, or null: its reservation has ended, or its crossmatch
    /// has expired and so has the specimen it was done on (a new crossmatch needs a new specimen). A unit the ward has
    /// asked to issue is never offered. Offering is not releasing: someone chooses, with a reason.
    /// </summary>
    public static string? ReleaseReason(ReadyToIssueWorkItemDto row, DateTime nowUtc)
    {
        if (row.IssueRequestedUtc is not null)
        {
            return null;
        }

        if (ReservationLapsed(row, nowUtc))
        {
            return $"Reservation ended {row.ReservationExpiresUtc!.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
        }

        return row.DisplayStatus == ProductAllocationDisplayStatus.CrossmatchExpired && row.SpecimenExpired
            ? "Crossmatch and specimen expired"
            : null;
    }

    /// <summary>The rows offered for release to stock, oldest reservation first.</summary>
    public static IReadOnlyList<ReadyToIssueWorkItemDto> ReleaseCandidates(IEnumerable<ReadyToIssueWorkItemDto> rows, DateTime nowUtc) =>
        rows.Where(row => ReleaseReason(row, nowUtc) is not null)
            .OrderBy(row => row.AllocatedUtc)
            .ToList();

    /// <summary>The Ready to issue card note: what needs a new crossmatch, what expires soon, and what is past its reservation.</summary>
    public static string? CardNote(IReadOnlyCollection<ReadyToIssueWorkItemDto> rows, DateTime? nowUtc = null)
    {
        var parts = new[]
        {
            DashboardQueueCatalog.AttentionNote(CountRequested(rows), "requested by the ward", "requested by the ward"),
            DashboardQueueCatalog.AttentionNote(CountCrossmatchExpired(rows.Select(r => r.DisplayStatus)), "needs a new crossmatch", "need a new crossmatch"),
            DashboardQueueCatalog.AttentionNote(CountExpiringSoon(rows), "expires soon", "expire soon"),
            DashboardQueueCatalog.AttentionNote(rows.Count(static r => r.ExpiresBeforeNeeded), "crossmatch ends before it is needed", "crossmatches end before they are needed"),
            nowUtc is DateTime now
                ? DashboardQueueCatalog.AttentionNote(CountReservationLapsed(rows, now), "past its reservation, to issue or release", "past their reservation, to issue or release")
                : null
        }.OfType<string>().ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>
    /// True when the board shows the Issue button: no mark on the row blocks routine issue.
    /// The issue gate still decides at issue.
    /// </summary>
    public static bool CanIssueRoutinely(ReadyToIssueWorkItemDto row) => IssueRun.OffersRoutineIssue(row);

    /// <summary>
    /// Rows that can be issued now come first; within each group, units the ward has asked for come first (earliest
    /// request first), and the rest keep their order.
    /// </summary>
    /// <summary>
    /// Units that can be issued now first; within each group the ward's requests (oldest first), then the earliest
    /// needed-by time of the order the unit is reserved for; otherwise the order they were reserved in.
    /// </summary>
    public static IReadOnlyList<ReadyToIssueWorkItemDto> IssuableFirst(IEnumerable<ReadyToIssueWorkItemDto> rows) =>
        rows.OrderBy(static row => CanIssueRoutinely(row) ? 0 : 1)
            .ThenBy(static row => row.IssueRequestedUtc ?? DateTime.MaxValue)
            .ThenBy(static row => row.NeededByUtc ?? DateTime.MaxValue)
            .ToList();

    public static int CountRequested(IEnumerable<ReadyToIssueWorkItemDto> rows) =>
        rows.Count(static row => row.IssueRequestedUtc is not null);

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
