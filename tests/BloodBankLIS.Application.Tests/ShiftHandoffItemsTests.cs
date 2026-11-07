using BloodBankLIS.Application.Compliance;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-556: a handoff lists only queues with something in them, safety items first and marked urgent, each linked to
// the board where it is worked, with what needs attention.
public class ShiftHandoffItemsTests
{
    private static ShiftHandoffDto Handoff(
        int retro = 0, int retroOverdue = 0, int pendingTests = 0, int toVerify = 0, int reagentLots = 0, int reagentResults = 0,
        int instrumentFailures = 0, int held = 0, int expired = 0, int unmapped = 0) =>
        new(1, DateTime.UtcNow, "tech", null, "note", retro, retroOverdue, 0, 0, 0, pendingTests, 0, 0, 0, null, null,
            ResultsAwaitingVerification: toVerify, ExpiredOnShelf: expired, InstrumentFailuresOpen: instrumentFailures,
            InstrumentResultsHeld: held, ReagentLotsFailedQc: reagentLots, ReagentResultsToReview: reagentResults,
            UnmappedInterfaceValues: unmapped);

    [Fact]
    public void Items_AreTheNonEmptyQueues_UrgentFirst_LinkedToTheirBoards()
    {
        var items = ShiftHandoffItems.For(Handoff(retro: 3, retroOverdue: 1, pendingTests: 12, toVerify: 2, reagentLots: 1, reagentResults: 4,
            instrumentFailures: 2, held: 1, expired: 1, unmapped: 2));

        Assert.Equal(
            ["Reagent QC failed", "Retrospective crossmatch", "Expired on shelf", "Pending tests", "Instrument failures", "Unmapped interface values"],
            items.Select(i => i.Label));
        Assert.All(items.Take(3), i => Assert.True(i.Urgent));
        Assert.Equal(("/reagents", "4 results to review"), (items[0].Href, items[0].Detail));
        Assert.Equal(("/issuing?desk=retro", "1 overdue"), (items[1].Href, items[1].Detail));
        Assert.Equal(("/test-worklist", "2 to verify"), (items[3].Href, items[3].Detail));
        Assert.Equal(("/instruments?show=open", "1 held for review"), (items[4].Href, items[4].Detail));
        Assert.Equal("/admin/hl7/translations?tab=unmapped", items[5].Href);
    }

    // TEST-BB-577: RhIG evaluations still needing work are a handoff item linked to the RhIG list, naming those to give.
    [Fact]
    public void RhigEvaluations_AreAHandoffItem_WithThoseToGive()
    {
        var item = Assert.Single(ShiftHandoffItems.For(Handoff() with { RhigOpen = 3, RhigToGive = 1 }));
        Assert.Equal(("RhIG evaluations", 3, "/rhig", "1 to give", false), (item.Label, item.Count, item.Href, item.Detail, item.Urgent));
        Assert.Contains("RhIG evaluations 3, 1 to give", (Handoff() with { RhigOpen = 3, RhigToGive = 1 }).QueueSummary);
    }

    [Fact]
    public void Items_AreEmpty_WhenNothingIsOpen() =>
        Assert.Empty(ShiftHandoffItems.For(Handoff()));

    [Fact]
    public void RetrospectiveCrossmatch_IsUrgentOnlyWhenOverdue()
    {
        Assert.False(ShiftHandoffItems.For(Handoff(retro: 2)).Single().Urgent);
        Assert.Null(ShiftHandoffItems.For(Handoff(retro: 2)).Single().Detail);
        Assert.True(ShiftHandoffItems.For(Handoff(retro: 2, retroOverdue: 1)).Single().Urgent);
    }
}
