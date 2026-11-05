using BloodBankLIS.Application.Results;
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
        Add(cards, has(PermissionCodes.ReactionInvestigate), Reactions, "Reaction investigations", "/reactions");
        Add(cards, has(PermissionCodes.ImmunoOverride)
            || has(PermissionCodes.ResultVerify)
            || has(PermissionCodes.InventoryRelease) || has(PermissionCodes.OverrideApprove), Supervisor, "Supervisor queue", "/supervisor");
        Add(cards, has(PermissionCodes.InventoryReceive)
            || has(PermissionCodes.DeviationManage), TemperatureExcursions, "Temperature excursions", "/storage-temps");
        Add(cards, has(PermissionCodes.InventoryReceive) || has(PermissionCodes.InventoryDiscard)
            || canIssue || has(PermissionCodes.CompatibilityAllocate), NearExpiry, "Near expiry", "/inventory?board=near");
        Add(cards, has(PermissionCodes.InstrumentManage), InstrumentFailures, "Instrument failures", "/instruments");
        Add(cards, has(PermissionCodes.Hl7Manage), Hl7Errors, "HL7 errors", "/hl7");
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

    public static string? AttentionNote(int count, string singular, string plural)
    {
        if (count <= 0)
        {
            return null;
        }

        return count == 1 ? $"1 {singular}" : $"{count} {plural}";
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
