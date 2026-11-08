using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
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

    // TEST-BB-401: an open instrument failure or HL7 error needs attention whenever there is one, after storage,
    // reaction, and emergency follow-up items; zero is clear.
    [Fact]
    public void InterfaceFailures_AreAttention_WhenNonZero_AfterPatientSafetyItems()
    {
        var layout = DashboardHome.Arrange(
        [
            State(Hl7Errors, 2),
            State(InstrumentFailures, 1),
            State(Retrospective, 1, "1 past due"),
            State(PendingTests, 4)
        ], WorkFocus.Bench);

        Assert.Equal([Retrospective, InstrumentFailures, Hl7Errors], layout.Attention.Select(c => c.Card.Key));

        var clear = DashboardHome.Arrange([State(Hl7Errors, 0), State(InstrumentFailures, 0)], WorkFocus.Bench);
        Assert.Empty(clear.Attention);
    }

    private static Func<string, bool> Has(params string[] codes) => code => codes.Contains(code);
    // TEST-BB-423: near-expiry units are a queue on the issue desk and supervisor focus; reserved ones need attention.
    [Fact]
    public void NearExpiry_IsAnIssueDeskAndSupervisorQueue_AndReservedOnesNeedAttention()
    {
        var plain = DashboardHome.Arrange([State(NearExpiry, 3)], WorkFocus.IssueDesk);
        Assert.Contains(plain.Queues, c => c.Card.Key == NearExpiry);
        Assert.Contains(DashboardHome.Arrange([State(NearExpiry, 3)], WorkFocus.Bench).OtherDesks, c => c.Card.Key == NearExpiry);

        var reserved = DashboardHome.Arrange(
            [State(NearExpiry, 3, NearExpiryNote(1)), State(Disposition, 2)],
            WorkFocus.Supervisor);
        Assert.Equal(NearExpiry, reserved.Attention[0].Card.Key);
    }

    // TEST-BB-473: Expired on shelf is a card for discarders, needs attention whenever above zero (before near expiry),
    // and belongs to the issue desk and supervisor views.
    [Fact]
    public void ExpiredOnShelf_IsForDiscarders_AndNeedsAttentionAboveZero()
    {
        Assert.Contains(DashboardQueueCatalog.Select(c => c == PermissionCodes.InventoryDiscard),
            card => card.Key == DashboardQueueCatalog.ExpiredOnShelf && card.Href == "/inventory/discard-expired");
        Assert.DoesNotContain(DashboardQueueCatalog.Select(c => c == PermissionCodes.InventoryReceive),
            card => card.Key == DashboardQueueCatalog.ExpiredOnShelf);
        Assert.True(DashboardHome.InFocus(DashboardQueueCatalog.ExpiredOnShelf, WorkFocus.IssueDesk));
        Assert.True(DashboardHome.InFocus(DashboardQueueCatalog.ExpiredOnShelf, WorkFocus.Supervisor));
        Assert.False(DashboardHome.InFocus(DashboardQueueCatalog.ExpiredOnShelf, WorkFocus.Bench));

        var layout = DashboardHome.Arrange([State(NearExpiry, 3, NearExpiryNote(1)), State(ExpiredOnShelf, 2)], WorkFocus.IssueDesk);
        Assert.Equal([ExpiredOnShelf, NearExpiry], layout.Attention.Select(c => c.Card.Key));
        Assert.DoesNotContain(DashboardHome.Arrange([State(ExpiredOnShelf, 0)], WorkFocus.IssueDesk).Attention, c => c.Card.Key == ExpiredOnShelf);
    }

    // TEST-BB-507: Below minimum stock is for whoever keeps or issues stock, belongs to the issue desk and supervisor
    // views, names the largest shortfalls with what is on hand, and needs attention whenever a product is short.
    [Fact]
    public void LowStock_NamesTheShortfalls_AndNeedsAttention()
    {
        Assert.Contains(DashboardQueueCatalog.Select(c => c == PermissionCodes.IssueCreate),
            card => card.Key == DashboardQueueCatalog.LowStock && card.Href == "/inventory/minimum-stock");
        Assert.DoesNotContain(DashboardQueueCatalog.Select(c => c == PermissionCodes.ResultEnter), card => card.Key == DashboardQueueCatalog.LowStock);
        Assert.True(DashboardHome.InFocus(DashboardQueueCatalog.LowStock, WorkFocus.IssueDesk));
        Assert.True(DashboardHome.InFocus(DashboardQueueCatalog.LowStock, WorkFocus.Supervisor));
        Assert.False(DashboardHome.InFocus(DashboardQueueCatalog.LowStock, WorkFocus.Bench));

        MinimumStockRowDto Row(string code, AboGroup? abo, int onHand, int min) =>
            new(1, 7, code, abo, onHand, min, Math.Max(0, min - onHand));
        var rows = new[]
        {
            Row("RBC", AboGroup.O, 2, 6), Row("RBC", AboGroup.A, 4, 4), Row("PLT", null, 0, 2),
            Row("FFP", AboGroup.AB, 1, 2), Row("CRYO", null, 3, 4), Row("", AboGroup.B, 0, 1)
        };
        var (count, note) = DashboardQueueCatalog.LowStockCard(rows);
        Assert.Equal(5, count);
        Assert.Equal("RBC O: 2 of 6; PLT any ABO: 0 of 2; Product 7 B: 0 of 1 and 2 more", note);
        Assert.Equal((0, null), DashboardQueueCatalog.LowStockCard([Row("RBC", AboGroup.A, 4, 4)]));
        Assert.Equal("Product 7 B: 0 of 1", DashboardQueueCatalog.LowStockCard([Row("", AboGroup.B, 0, 1)]).Note);

        var layout = DashboardHome.Arrange([State(NearExpiry, 3, NearExpiryNote(1)), State(DashboardQueueCatalog.LowStock, count, note)], WorkFocus.IssueDesk);
        Assert.Equal([DashboardQueueCatalog.LowStock, NearExpiry], layout.Attention.Select(c => c.Card.Key));
    }

    // TEST-BB-589: Tubes due for discard is a card for whoever stores tubes, on the bench view, and a routine count:
    // it does not need attention on its own.
    [Fact]
    public void TubesDueForDiscard_IsForTubeStorers_OnTheBench_AndRoutine()
    {
        Assert.Contains(Select(c => c == PermissionCodes.SpecimenAccession),
            card => card.Key == TubesDueForDiscard && card.Href == "/specimens/store" && card.Label == "Tubes due for discard");
        Assert.DoesNotContain(Select(c => c == PermissionCodes.ResultEnter), card => card.Key == TubesDueForDiscard);
        Assert.True(DashboardHome.InFocus(TubesDueForDiscard, WorkFocus.Bench));
        Assert.False(DashboardHome.InFocus(TubesDueForDiscard, WorkFocus.IssueDesk));

        var layout = DashboardHome.Arrange([State(TubesDueForDiscard, 4)], WorkFocus.Bench);
        Assert.DoesNotContain(layout.Attention, c => c.Card.Key == TubesDueForDiscard);
    }

    // TEST-BB-623: Blood orders to prepare is a card for those who allocate, on the bench view; its note names rush orders
    // and those waiting for a specimen.
    [Fact]
    public void BloodOrders_IsForAllocators_WithRushAndSpecimenNotes()
    {
        Assert.Contains(Select(c => c == PermissionCodes.CompatibilityAllocate), card => card.Key == BloodOrders && card.Href == "/blood-orders");
        Assert.DoesNotContain(Select(c => c == PermissionCodes.ResultEnter), card => card.Key == BloodOrders);
        Assert.True(DashboardHome.InFocus(BloodOrders, WorkFocus.Bench));

        BloodBankLIS.Application.PatientWorkspace.BloodOrderToPrepareDto Row(bool rush, bool specimen) => new(
            1, "O1", 1, "P", "M", null, [], OrderPriority.Routine, rush, DateTime.UtcNow, "1 × RBC", 1, 0, 0, 1, specimen, null, "x", "/");
        Assert.Equal("1 rush; 1 needs a specimen", BloodOrdersNote([Row(true, true), Row(false, false)]));
        Assert.Null(BloodOrdersNote([Row(false, true)]));
        // TEST-BB-648 (note): orders inside the needed-by warning window are counted.
        Assert.Equal("1 needed soon", BloodOrdersNote([Row(false, true) with { NeededBySoon = true, NeededInMinutes = 20 }]));
    }
}
