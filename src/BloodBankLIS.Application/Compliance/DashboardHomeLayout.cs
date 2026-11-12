using BloodBankLIS.Domain.Rules;
using static BloodBankLIS.Application.Compliance.DashboardQueueCatalog;

namespace BloodBankLIS.Application.Compliance;

/// <summary>Which desk the home screen leads with. Everything shows every permitted queue in the main grid.</summary>
public enum WorkFocus
{
    Everything = 0,
    Bench = 1,
    IssueDesk = 2,
    Supervisor = 3
}

/// <summary>A dashboard card with what its worklist returned. A null count means the load failed, not zero.</summary>
public sealed record DashboardCardState(Card Card, int? Count, string? Note);

public sealed record DashboardHomeLayout(
    DashboardCardState? StartHere,
    IReadOnlyList<DashboardCardState> Attention,
    IReadOnlyList<DashboardCardState> Queues,
    IReadOnlyList<DashboardCardState> Clear,
    IReadOnlyList<DashboardCardState> Unavailable,
    IReadOnlyList<DashboardCardState> OtherDesks);

/// <summary>
/// Arranges the permitted dashboard cards for the signed-in user's desk.
/// Overdue and safety items always lead, whatever the focus. A queue that failed to load is never shown as clear.
/// Only the order and grouping change: every card the user may open stays reachable on the page.
/// </summary>
public static class DashboardHome
{
    /// <summary>Worth attention whenever the count is above zero, even without an overdue note.</summary>
    /// <summary>
    /// Interface failures count too: a failed instrument message is a patient result that did not file, and an HL7
    /// error is an order, result, or admission that did not reach its destination.
    /// </summary>
    private static readonly string[] UrgentWhenNonZero = [TemperatureExcursions, Reactions, Handoff, InstrumentFailures, Hl7Errors, ExpiredOnShelf, RecalledToPull, OutageOpen];

    /// <summary>
    /// Most urgent first: storage safety, patient harm, then emergency follow-up and lost units, then reserved units
    /// blocked from routine issue with no second ABO/Rh ordered.
    /// </summary>
    private static readonly string[] UrgencyOrder =
        [TemperatureExcursions, Reactions, Retrospective, InTransit, SecondAbo, ReagentQc, InstrumentFailures, Hl7Errors, Handoff, Deviations, RecalledToPull, ExpiredOnShelf, LowStock, NearExpiry, Disposition, Supervisor];

    private static readonly Dictionary<WorkFocus, string[]> Desks = new()
    {
        [WorkFocus.Bench] = [PendingSpecimens, PendingTests, ToVerify, BloodOrders, AntibodyWorkups, OutsideAntibodies, ReagentQc, PanelLotsToReplace, InstrumentFailures, SecondAbo, Supervisor, TubesDueForDiscard],
        [WorkFocus.IssueDesk] = [Ready, SecondAbo, InTransit, Outstanding, Retrospective, Disposition, NearExpiry, RecalledToPull, ExpiredOnShelf, LowStock, OutageOpen],
        [WorkFocus.Supervisor] = [Supervisor, ToVerify, AntibodyWorkups, OutsideAntibodies, Reactions, TemperatureExcursions, Retrospective, Disposition, Handoff, Deviations, Hl7Errors, Hl7OutboundWaiting, UnmappedInterfaceValues, InstrumentFailures, NearExpiry, RecalledToPull, ExpiredOnShelf, LowStock, Unidentified, OutageOpen, SettingsToEnter, PanelLotsToReplace]
    };

    public static IReadOnlyList<WorkFocus> Choices { get; } =
        [WorkFocus.Everything, WorkFocus.Bench, WorkFocus.IssueDesk, WorkFocus.Supervisor];

    public static string Label(WorkFocus focus) => focus switch
    {
        WorkFocus.Bench => "Bench",
        WorkFocus.IssueDesk => "Issue desk",
        WorkFocus.Supervisor => "Supervisor",
        _ => "Everything"
    };

    /// <summary>
    /// A starting desk from the permissions held: the one desk the user can work, otherwise Everything
    /// (an administrator or a user who covers several desks). The user can change it.
    /// </summary>
    public static WorkFocus InferFocus(Func<string, bool> has)
    {
        ArgumentNullException.ThrowIfNull(has);
        var bench = has(PermissionCodes.ResultEnter) || has(PermissionCodes.SpecimenAccession);
        var issue = has(PermissionCodes.IssueCreate);
        var supervisor = has(PermissionCodes.ImmunoOverride) || has(PermissionCodes.ResultVerify) || has(PermissionCodes.InventoryRelease);
        var desks = new[] { (bench, WorkFocus.Bench), (issue, WorkFocus.IssueDesk), (supervisor, WorkFocus.Supervisor) }
            .Where(d => d.Item1)
            .Select(d => d.Item2)
            .ToList();
        return desks.Count == 1 ? desks[0] : WorkFocus.Everything;
    }

    public static bool InFocus(string key, WorkFocus focus) =>
        focus == WorkFocus.Everything || (Desks.TryGetValue(focus, out var keys) && keys.Contains(key));

    public static DashboardHomeLayout Arrange(IEnumerable<DashboardCardState> cards, WorkFocus focus)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var all = cards.ToList();

        var unavailable = all.Where(c => c.Count is null).ToList();
        var attention = all
            .Where(c => c.Count > 0 && (!string.IsNullOrWhiteSpace(c.Note) || UrgentWhenNonZero.Contains(c.Card.Key)))
            .OrderBy(c => Rank(c.Card.Key))
            .ToList();
        var rest = all.Where(c => c.Count is not null && !attention.Contains(c)).ToList();
        var queues = rest.Where(c => c.Count > 0 && InFocus(c.Card.Key, focus)).ToList();
        var clear = rest.Where(c => c.Count == 0 && InFocus(c.Card.Key, focus)).ToList();
        var other = rest.Where(c => !InFocus(c.Card.Key, focus)).ToList();

        return new DashboardHomeLayout(
            attention.FirstOrDefault() ?? queues.FirstOrDefault(),
            attention,
            queues,
            clear,
            unavailable,
            other);
    }

    private static int Rank(string key)
    {
        var index = Array.IndexOf(UrgencyOrder, key);
        return index < 0 ? UrgencyOrder.Length : index;
    }
}
