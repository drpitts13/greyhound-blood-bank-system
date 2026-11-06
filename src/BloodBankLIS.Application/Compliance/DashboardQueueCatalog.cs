using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

/// <summary>
/// Which bench queues the dashboard may show. A card is omitted when the
/// signed-in user cannot open that queue. The counts stay on the existing
/// worklists and do not change issue, receipt, result, or investigation gates.
/// </summary>
public static class DashboardQueueCatalog
{
    public sealed record Card(string Key, string Label, string Href);

    public const string Ready = "ready";
    public const string SecondAbo = "second-abo";
    public const string Retrospective = "retrospective";
    public const string InTransit = "in-transit";
    public const string Outstanding = "outstanding";
    public const string Disposition = "disposition";
    public const string PendingTests = "pending-tests";
    public const string ToVerify = "to-verify";
    public const string PendingSpecimens = "pending-specimens";
    public const string Reactions = "reactions";
    public const string Supervisor = "supervisor";
    public const string Handoff = "handoff";
    public const string TemperatureExcursions = "temp-excursions";
    public const string Hl7Errors = "hl7-errors";
    public const string InstrumentFailures = "instrument-failures";
    public const string NearExpiry = "near-expiry";
    public const string ExpiredOnShelf = "expired-on-shelf";
    public const string Deviations = "deviations";
    public const string ReagentQc = "reagent-qc";

    public static IReadOnlyList<Card> Select(Func<string, bool> has)
    {
        ArgumentNullException.ThrowIfNull(has);
        var cards = new List<Card>();
        var canIssue = has(PermissionCodes.IssueCreate);
        var canIssuedBoard = canIssue
            || has(PermissionCodes.IssueReturn)
            || has(PermissionCodes.TransfusionDocument);

        Add(cards, canIssue, Ready, "Ready to issue", "/issuing?desk=ready");
        Add(cards, canIssue, SecondAbo, "Second ABO/Rh", "/issuing?desk=second");
        Add(cards, canIssue, Retrospective, "Retrospective crossmatch", "/issuing?desk=retro");
        Add(cards, canIssue, InTransit, "In transit", "/issuing?desk=transit");
        Add(cards, canIssuedBoard, Outstanding, "Outstanding issued", "/issuing?desk=issued");
        Add(cards, canIssuedBoard, Disposition, "Final disposition", "/issuing?desk=disposition");
        Add(cards, has(PermissionCodes.ResultEnter), PendingTests, "Pending tests", "/test-worklist");
        Add(cards, has(PermissionCodes.ResultVerify), ToVerify, "Results to verify", "/test-worklist?show=verify");
        Add(cards, has(PermissionCodes.SpecimenAccession), PendingSpecimens, "Pending specimens", "/test-worklist?tab=specimens");
        // Daily reagent QC is bench work: whoever enters results records and reviews it.
        Add(cards, has(PermissionCodes.ResultEnter), ReagentQc, "Reagent QC", "/reagents");
        Add(cards, has(PermissionCodes.ReactionInvestigate), Reactions, "Reaction investigations", "/reactions");
        Add(cards, has(PermissionCodes.ImmunoOverride)
            || has(PermissionCodes.ResultVerify)
            || has(PermissionCodes.InventoryRelease) || has(PermissionCodes.OverrideApprove), Supervisor, "Supervisor queue", "/supervisor");
        Add(cards, has(PermissionCodes.InventoryReceive)
            || has(PermissionCodes.DeviationManage), TemperatureExcursions, "Temperature excursions", "/storage-temps");
        Add(cards, has(PermissionCodes.InventoryReceive) || has(PermissionCodes.InventoryDiscard)
            || canIssue || has(PermissionCodes.CompatibilityAllocate), NearExpiry, "Near expiry", "/inventory?board=near");
        // Expired units still recorded on a shelf: pull and discard them (inventory.discard).
        Add(cards, has(PermissionCodes.InventoryDiscard), ExpiredOnShelf, "Expired on shelf", "/inventory/discard-expired");
        Add(cards, has(PermissionCodes.InstrumentManage), InstrumentFailures, "Instrument failures", "/instruments");
        Add(cards, has(PermissionCodes.Hl7Manage), Hl7Errors, "HL7 errors", "/hl7");
        Add(cards, has(PermissionCodes.DeviationManage), Deviations, "Open deviations", "/deviations");
        Add(cards, has(PermissionCodes.ShiftHandoff), Handoff, "Shift handoff", "/handoff");
        return cards;
    }

    /// <summary>
    /// Extra line when a stored overdue or follow-up count is already above zero.
    /// Zero stays silent so a routine queue looks like a count alone.
    /// </summary>
    /// <summary>
    /// The Results to verify card: lines this user can verify now (the worklist's To verify view), and of those how
    /// many must be verified before their specimen expires.
    /// </summary>
    public static (int Count, string? Note) ToVerifyCard(IEnumerable<TestWorkItemDto> pending, string? userName)
    {
        var mine = TestWorklistView.ToVerify(pending, userName);
        var dueSoon = PendingTestAttentionRule.CountVerifyBeforeExpiry(mine);
        return (mine.Count, AttentionNote(dueSoon, "before its specimen expires", "before their specimens expire"));
    }

    /// <summary>
    /// The Pending tests card note: results to verify before their specimen expires, and lines whose tube was
    /// rejected and need a new specimen. Either brings the card under Needs attention.
    /// </summary>
    public static string? PendingTestsNote(IEnumerable<TestWorkItemDto> pending)
    {
        var items = pending.ToList();
        var parts = new[]
        {
            AttentionNote(PendingTestAttentionRule.CountVerifyBeforeExpiry(items), "to verify before its specimen expires", "to verify before their specimens expire"),
            AttentionNote(TestWorklistView.Recollect(items).Count, "needs a new specimen", "need a new specimen"),
            AttentionNote(TestWorklistView.RushOrdersWithoutSpecimen(items), "STAT/urgent order waiting for a specimen", "STAT/urgent orders waiting for a specimen")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    public static string? AttentionNote(int count, string singular, string plural)
    {
        if (count <= 0)
        {
            return null;
        }

        return count == 1 ? $"1 {singular}" : $"{count} {plural}";
    }

    /// <summary>
    /// The In transit card note: units past the arrival window, and coolers out that were not checked before they left
    /// or were checked with differences. Either brings the card under Needs attention. No cooler list, no cooler note.
    /// </summary>
    public static string? InTransitNote(int overdue, IEnumerable<OpenCoolerDto>? coolers)
    {
        var open = coolers?.ToList() ?? [];
        var parts = new[]
        {
            AttentionNote(overdue, "past the arrival window", "past the arrival window"),
            AttentionNote(open.Count(c => c.LastCheckedUtc is null), "cooler not checked", "coolers not checked"),
            AttentionNote(open.Count(c => c.LastCheckedUtc is not null && c.LastCheckReady != true), "cooler checked with differences", "coolers checked with differences")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The Reactions card note: fatality follow-ups, open investigations still waiting for their post-transfusion
    /// specimen (it should be drawn promptly after the reaction), and open investigations whose workup tests are not all
    /// verified yet. Closed cases count only for fatality follow-up.
    /// </summary>
    public static string? ReactionsNote(IEnumerable<ReactionInvestigationDto> investigations)
    {
        var all = investigations.ToList();
        var open = all.Where(r => OpenReactionInvestigationRule.NeedsAttention(r.Status)).ToList();
        var parts = new[]
        {
            AttentionNote(OpenReactionInvestigationRule.CountFatalityFollowUpDue(all), "fatality follow-up", "fatality follow-ups"),
            AttentionNote(open.Count(r => r.PostWorkup is null), "waiting for the post-transfusion specimen", "waiting for the post-transfusion specimen"),
            AttentionNote(open.Count(r => (r.PreWorkup?.Pending ?? 0) + (r.PostWorkup?.Pending ?? 0) > 0), "with workup tests to verify", "with workup tests to verify")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The Outstanding issued card note: transfusions started at the bedside and still not completed after the
    /// facility's reminder hours (the same flag as the shift handoff and the Issued units badge).
    /// </summary>
    public static string? OutstandingNote(IEnumerable<OutstandingIssueWorkItemDto> outstanding) =>
        AttentionNote(outstanding.Count(o => o.TransfusionCompletionOverdue), "transfusion completion overdue", "transfusion completions overdue");

    /// <summary>
    /// The Supervisor queue card note: results that must be verified before their specimen expires, missing or damaged
    /// units, and overrides waiting for a second-person review. Antibody ID reviews and quarantine releases stay in the
    /// count only; they have no deadline of their own here.
    /// </summary>
    public static string? SupervisorNote(IEnumerable<SupervisorQueueItemDto> items)
    {
        var all = items.ToList();
        var parts = new[]
        {
            AttentionNote(all.Count(i => i.Kind == SupervisorQueueRule.KindResultVerify && i.DueSoon), "to verify before its specimen expires", "to verify before their specimens expire"),
            AttentionNote(all.Count(i => i.Kind == SupervisorQueueRule.KindDiscrepancy), "missing or damaged unit", "missing or damaged units"),
            AttentionNote(all.Count(i => i.Kind == SupervisorQueueRule.KindOverrideReview), "override to review", "overrides to review")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The Open deviations card: every deviation not yet closed, naming the critical ones (which puts the card under
    /// Needs attention). Major and minor stay in the count.
    /// </summary>
    public static (int Count, string? Note) DeviationsCard(IEnumerable<DeviationDto> deviations)
    {
        var open = deviations.Where(d => d.Status != DeviationStatus.Closed).ToList();
        return (open.Count, AttentionNote(open.Count(d => d.Severity == DeviationSeverity.Critical), "critical", "critical"));
    }

    /// <summary>
    /// The Reagent QC card note: failed QC (results on that lot are at risk) and overdue daily QC. QC waiting for review
    /// stays in the count only.
    /// </summary>
    public static string? ReagentQcNote(IEnumerable<ReagentQcWorkItemDto> work)
    {
        var items = work.ToList();
        var parts = new[]
        {
            AttentionNote(items.Count(i => i.Kind == ReagentQcWorklistRule.Failed), "failed", "failed"),
            AttentionNote(items.Count(i => i.Kind == ReagentQcWorklistRule.Overdue), "overdue", "overdue")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>Near-expiry units reserved for a patient: issue them first or reserve a replacement.</summary>
    public static string? NearExpiryNote(int reservedCount) =>
        AttentionNote(reservedCount, "reserved for a patient", "reserved for patients");

    public static string? HandoffNote(bool open) =>
        open ? "Needs acknowledgment" : null;

    private static void Add(List<Card> cards, bool show, string key, string label, string href)
    {
        if (show)
        {
            cards.Add(new Card(key, label, href));
        }
    }
}
