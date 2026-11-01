using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Rules;
using static BloodBankLIS.Application.Compliance.DashboardQueueCatalog;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-310: role-focused home. Urgent work leads whatever the focus; a failed load is never shown as clear.
public class DashboardHomeTests
{
    private static readonly IReadOnlyList<Card> AllCards = Select(_ => true);

    private static DashboardCardState State(string key, int? count, string? note = null) =>
        new(AllCards.Single(c => c.Key == key), count, note);

    [Fact]
    public void InferFocus_OneDesk_PicksIt_SeveralOrNone_IsEverything()
    {
        Assert.Equal(WorkFocus.Bench, DashboardHome.InferFocus(Has(PermissionCodes.ResultEnter, PermissionCodes.SpecimenAccession)));
        Assert.Equal(WorkFocus.IssueDesk, DashboardHome.InferFocus(Has(PermissionCodes.IssueCreate)));
        Assert.Equal(WorkFocus.Supervisor, DashboardHome.InferFocus(Has(PermissionCodes.ResultVerify)));
        Assert.Equal(WorkFocus.Everything, DashboardHome.InferFocus(Has(PermissionCodes.ResultEnter, PermissionCodes.IssueCreate)));
        Assert.Equal(WorkFocus.Everything, DashboardHome.InferFocus(_ => true));
        Assert.Equal(WorkFocus.Everything, DashboardHome.InferFocus(_ => false));
    }

    [Fact]
    public void UrgentItems_LeadInUrgencyOrder_EvenOutsideTheFocus()
    {
        var layout = DashboardHome.Arrange(
        [
            State(PendingTests, 7),
            State(Retrospective, 2, "1 past due"),
            State(TemperatureExcursions, 1),
            State(Reactions, 3, "1 fatality follow-up")
        ], WorkFocus.Bench);

        Assert.Equal([TemperatureExcursions, Reactions, Retrospective], layout.Attention.Select(c => c.Card.Key));
        Assert.Equal(TemperatureExcursions, layout.StartHere!.Card.Key);
        Assert.Equal([PendingTests], layout.Queues.Select(c => c.Card.Key));
        Assert.Empty(layout.OtherDesks);
    }

    [Fact]
    public void WithoutUrgentItems_StartHere_IsTheFirstFocusQueueWithWork_InWorkflowOrder()
    {
        var layout = DashboardHome.Arrange(
        [
            State(Ready, 4),
            State(PendingSpecimens, 0),
            State(PendingTests, 5),
            State(SecondAbo, 1)
        ], WorkFocus.Bench);

        Assert.Empty(layout.Attention);
        Assert.Equal([PendingTests, SecondAbo], layout.Queues.Select(c => c.Card.Key));
        Assert.Equal(PendingTests, layout.StartHere!.Card.Key);
        Assert.Equal([PendingSpecimens], layout.Clear.Select(c => c.Card.Key));
        Assert.Equal([Ready], layout.OtherDesks.Select(c => c.Card.Key));
    }

    [Fact]
    public void FailedLoad_IsUnavailable_NeverClear_InAnyFocus()
    {
        foreach (var focus in DashboardHome.Choices)
        {
            var layout = DashboardHome.Arrange([State(InTransit, null), State(PendingTests, 0)], focus);

            Assert.Equal([InTransit], layout.Unavailable.Select(c => c.Card.Key));
            Assert.DoesNotContain(layout.Clear, c => c.Card.Key == InTransit);
            Assert.DoesNotContain(layout.OtherDesks, c => c.Card.Key == InTransit);
        }
    }

    [Fact]
    public void AllClear_HasNoStartHere()
    {
        var layout = DashboardHome.Arrange([State(PendingTests, 0), State(Ready, 0)], WorkFocus.Everything);

        Assert.Null(layout.StartHere);
        Assert.Equal(2, layout.Clear.Count);
        Assert.Empty(layout.OtherDesks);
    }

    [Fact]
    public void EveryPermittedCard_AppearsExactlyOnce_ForEveryFocus()
    {
        var states = AllCards.Select((c, i) => new DashboardCardState(c, i % 3 == 0 ? null : i % 3, i == 4 ? "1 overdue" : null)).ToList();
        foreach (var focus in DashboardHome.Choices)
        {
            var layout = DashboardHome.Arrange(states, focus);
            var shown = layout.Attention.Concat(layout.Queues).Concat(layout.Clear).Concat(layout.Unavailable).Concat(layout.OtherDesks)
                .Select(c => c.Card.Key).ToList();

            Assert.Equal(AllCards.Select(c => c.Key).OrderBy(k => k), shown.OrderBy(k => k));
        }
    }

    [Fact]
    public void EverythingFocus_HasNoOtherDesks()
    {
        var layout = DashboardHome.Arrange(AllCards.Select(c => new DashboardCardState(c, 1, null)), WorkFocus.Everything);

        Assert.Empty(layout.OtherDesks);
    }

    [Fact]
    public void RoutineNonZero_WithoutNote_IsNotAttention_ExceptForUrgentQueues()
    {
        var layout = DashboardHome.Arrange([State(Retrospective, 2), State(Handoff, 1)], WorkFocus.Supervisor);

        Assert.Equal([Handoff], layout.Attention.Select(c => c.Card.Key));
        Assert.Equal([Retrospective], layout.Queues.Select(c => c.Card.Key));
    }

    private static Func<string, bool> Has(params string[] codes) => code => codes.Contains(code);
}
