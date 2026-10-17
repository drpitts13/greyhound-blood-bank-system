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
    public const string PendingSpecimens = "pending-specimens";
    public const string Reactions = "reactions";
    public const string Supervisor = "supervisor";
    public const string Handoff = "handoff";

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
        Add(cards, has(PermissionCodes.SpecimenAccession), PendingSpecimens, "Pending specimens", "/test-worklist?tab=specimens");
        Add(cards, has(PermissionCodes.ReactionInvestigate), Reactions, "Reaction investigations", "/reactions");
        Add(cards, has(PermissionCodes.ImmunoOverride)
            || has(PermissionCodes.ResultVerify)
            || has(PermissionCodes.InventoryRelease), Supervisor, "Supervisor queue", "/supervisor");
        Add(cards, has(PermissionCodes.AuditRead), Handoff, "Shift handoff", "/handoff");
        return cards;
    }

    /// <summary>
    /// Extra line when a stored overdue or follow-up count is already above zero.
    /// Zero stays silent so a routine queue looks like a count alone.
    /// </summary>
    public static string? AttentionNote(int count, string singular, string plural)
    {
        if (count <= 0)
        {
            return null;
        }

        return count == 1 ? $"1 {singular}" : $"{count} {plural}";
    }

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
