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

    // TEST-BB-793: open antibody workups are a routine handoff item linked to the antibody ID worklist, naming those to
    // review, and are in the summary line; with none there is no item.
    [Fact]
    public void AntibodyWorkups_AreAHandoffItem_WithThoseToReview()
    {
        var handoff = Handoff() with { AntibodyWorkupsOpen = 2, AntibodyWorkupsToReview = 1 };
        var item = Assert.Single(ShiftHandoffItems.For(handoff));
        Assert.Equal(("Antibody workups", 2, "/antibody-id", "1 to review", false), (item.Label, item.Count, item.Href, item.Detail, item.Urgent));
        Assert.Contains("antibody workups 2, 1 to review", handoff.QueueSummary);
        Assert.Empty(ShiftHandoffItems.For(Handoff()));
        Assert.DoesNotContain("antibody workups", Handoff().QueueSummary);
    }

    // TEST-BB-841: outbound HL7 messages not sent are a handoff item linked to the HL7 page and in the summary line;
    // none, no item.
    [Fact]
    public void Hl7OutboundNotSent_IsAHandoffItem()
    {
        var handoff = Handoff() with { Hl7OutboundNotSent = 7 };
        var item = Assert.Single(ShiftHandoffItems.For(handoff));
        Assert.Equal(("HL7 outbound not sent", 7, "/hl7"), (item.Label, item.Count, item.Href));
        Assert.Contains("HL7 outbound not sent 7", handoff.QueueSummary);
        Assert.DoesNotContain("outbound", Handoff().QueueSummary);
    }

    // TEST-BB-850: an outage still open is an urgent handoff item linked to Downtime Snapshot and in the summary line;
    // none, no item.
    [Fact]
    public void OutageOpen_IsAnUrgentHandoffItem()
    {
        var handoff = Handoff() with { OutagesOpen = 1 };
        var item = Assert.Single(ShiftHandoffItems.For(handoff));
        Assert.Equal(("Outage still open", 1, "/downtime", "end it when the system is back", true), (item.Label, item.Count, item.Href, item.Detail, item.Urgent));
        Assert.Contains("OUTAGE STILL OPEN", handoff.QueueSummary);
        Assert.DoesNotContain("OUTAGE", Handoff().QueueSummary);
    }

    // TEST-BB-889 (Cycle 679): overdue final dispositions are an urgent item opening that board; outside antibodies to
    // confirm are a routine item opening the list on Antibody ID; both are in the summary; none, no items.
    [Fact]
    public void DispositionOverdue_AndOutsideAntibodies_AreHandoffItems()
    {
        var handoff = Handoff() with { DispositionOverdue = 2, OutsideAntibodiesToConfirm = 1 };
        var items = ShiftHandoffItems.For(handoff);
        var disposition = items.Single(i => i.Label == "Final disposition overdue");
        Assert.Equal((2, "/issuing?desk=disposition", true, "record the outcome"), (disposition.Count, disposition.Href, disposition.Urgent, disposition.Detail));
        var outside = items.Single(i => i.Label == "Outside antibodies to confirm");
        Assert.Equal((1, "/antibody-id#outside-reports", false), (outside.Count, outside.Href, outside.Urgent));
        Assert.Contains("final disposition overdue 2", handoff.QueueSummary);
        Assert.Contains("outside antibodies to confirm 1", handoff.QueueSummary);
        Assert.Empty(ShiftHandoffItems.For(Handoff()));
    }

    // TEST-BB-615: unidentified patients still unmerged lead the handoff (urgent, linked to Patients); tubes due for discard
    // are a routine item linked to Store tubes; both are in the summary line.
    [Fact]
    public void UnidentifiedPatientsAndTubesDue_AreHandoffItems()
    {
        var handoff = Handoff() with { UnidentifiedPatients = 2, TubesDueForDiscard = 5, RhigOpen = 1 };
        var items = ShiftHandoffItems.For(handoff);
        Assert.Equal(("Unidentified patients", 2, "/patients", "to merge once identified", true), (items[0].Label, items[0].Count, items[0].Href, items[0].Detail, items[0].Urgent));
        var tubes = Assert.Single(items, i => i.Label == "Tubes due for discard");
        Assert.Equal(("/specimens/store", false), (tubes.Href, tubes.Urgent));
        Assert.Contains("UNIDENTIFIED PATIENTS 2, to merge once identified", handoff.QueueSummary);
        Assert.Contains("tubes due for discard 5", handoff.QueueSummary);
    }

    // TEST-BB-634: blood orders to prepare are a handoff item linked to the worklist, urgent when a needed-by time has
    // passed, and in the summary line.
    [Fact]
    public void BloodOrdersToPrepare_AreAHandoffItem_UrgentWhenPastNeededBy()
    {
        var routine = Assert.Single(ShiftHandoffItems.For(Handoff() with { BloodOrdersToPrepare = 3 }));
        Assert.Equal(("Blood orders to prepare", 3, "/blood-orders", (string?)null, false), (routine.Label, routine.Count, routine.Href, routine.Detail, routine.Urgent));
        var late = Handoff() with { BloodOrdersToPrepare = 3, BloodOrdersPastNeededBy = 1 };
        var item = Assert.Single(ShiftHandoffItems.For(late));
        Assert.Equal(("1 past needed-by", true), (item.Detail, item.Urgent));
        Assert.Contains("blood orders to prepare 3, 1 PAST NEEDED-BY", late.QueueSummary);

        // TEST-BB-649: orders needed soon (OCD-075 window) make the item urgent too, named beside those past their time.
        var soon = Handoff() with { BloodOrdersToPrepare = 3, BloodOrdersNeededSoon = 2 };
        var soonItem = Assert.Single(ShiftHandoffItems.For(soon));
        Assert.Equal(("2 needed soon", true), (soonItem.Detail, soonItem.Urgent));
        Assert.Contains("blood orders to prepare 3, 2 needed soon", soon.QueueSummary);
        var both = Assert.Single(ShiftHandoffItems.For(late with { BloodOrdersNeededSoon = 2 }));
        Assert.Equal("1 past needed-by, 2 needed soon", both.Detail);
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

    // TEST-BB-758: a reaction investigation whose patient still has units out or reserved makes the Reaction
    // investigations item urgent and named, on the item and the summary line; without one it stays an ordinary count.
    [Fact]
    public void ReactionsWithUnitsOut_MakeTheReactionItemUrgent()
    {
        var plain = Assert.Single(ShiftHandoffItems.For(Handoff() with { OpenReactions = 2 }));
        Assert.Equal(("Reaction investigations", false, (string?)null), (plain.Label, plain.Urgent, plain.Detail));

        var withUnits = Handoff() with { OpenReactions = 2, ReactionsWithUnitsOut = 1 };
        var item = Assert.Single(ShiftHandoffItems.For(withUnits));
        Assert.Equal((true, "1 with units out or reserved"), (item.Urgent, item.Detail));
        Assert.Contains("reactions 2, 1 with units out or reserved", withUnits.QueueSummary);
    }

    // TEST-BB-759: the count takes each open investigation once, whether its patient has units issued, reserved, or both;
    // units without one do not count.
    [Fact]
    public void ReactionsWithUnitsOut_CountsEachInvestigationOnce()
    {
        static Issuing.OutstandingIssueWorkItemDto Out(long? reaction) => new(
            1, 5, "MRN", "P", "1980-01-01", 9, "W1", "O+", false, null, null, false, DateTime.UtcNow, null, "n", null, "4W",
            BloodBankLIS.Domain.Enums.IssueStatus.Issued) { OpenReactionInvestigationId = reaction };
        static Issuing.ReadyToIssueWorkItemDto Ready(long? reaction) => new(
            AllocationId: 1, PatientId: 5, MedicalRecordNumber: "MRN", PatientDisplayName: "P", DateOfBirth: "1980-01-01",
            BloodUnitId: 2, UnitNumber: "W2", CurrentBloodType: "O+", HasAntibodyHistory: false, AntibodySummary: null,
            SpecimenExpiresUtc: null, SpecimenExpired: false, AllocatedUtc: DateTime.UtcNow,
            DisplayStatus: BloodBankLIS.Domain.Enums.ProductAllocationDisplayStatus.ReadyForIssue,
            LatestCrossmatchResult: BloodBankLIS.Domain.Enums.CrossmatchResult.Compatible) { OpenReactionInvestigationId = reaction };

        Assert.Equal(0, ShiftHandoffService.ReactionsWithUnitsOut([Out(null)], [Ready(null)]));
        Assert.Equal(1, ShiftHandoffService.ReactionsWithUnitsOut([Out(3), Out(3)], [Ready(3)]));
        Assert.Equal(2, ShiftHandoffService.ReactionsWithUnitsOut([Out(3)], [Ready(4), Ready(null)]));
    }
}
