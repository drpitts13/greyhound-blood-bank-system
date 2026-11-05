using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class IssueRunTests
{
    private static readonly DateTime Start = new(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc);

    private static ReadyToIssueWorkItemDto Row(long patientId, long unitId, int minutes) =>
        new(unitId, patientId, "MRN1", "Run, Pat", "1980-01-01", unitId, $"W{unitId:000}", "O+", false, null,
            Start.AddDays(2), false, Start.AddMinutes(minutes), ProductAllocationDisplayStatus.ReadyForIssue, null,
            ProductName: "Red Blood Cells");

    [Fact]
    public void NextForPatient_OffersThatPatientsOtherReadyUnits_OldestReservationFirst()
    {
        var ready = new[]
        {
            Row(1, 11, minutes: 30),
            Row(1, 12, minutes: 10),
            Row(1, 13, minutes: 20),
            Row(2, 21, minutes: 0)
        };

        var next = IssueRun.NextForPatient(ready, patientId: 1, unitOnForm: 13, issuedThisRun: [11]);

        Assert.Equal([12L], next.Select(r => r.BloodUnitId));
        Assert.Equal([12L, 13L, 11L], IssueRun.NextForPatient(ready, 1, null, []).Select(r => r.BloodUnitId));
    }

    [Fact]
    public void UnitsTheBoardWouldNotIssueRoutinely_AreNotOffered_ButCounted()
    {
        var ready = new[]
        {
            Row(1, 11, 0),
            Row(1, 12, 1) with { SpecialRequirementNotice = "Patient requires Irradiated red cells: the unit's product does not carry IRRAD." },
            Row(1, 13, 2) with { CrossmatchBlocksRoutineIssue = true },
            Row(1, 14, 3) with { SpecimenBlocksIssue = true },
            Row(1, 15, 4) with { NeedsSecondAbo = true },
            Row(1, 16, 5) with { UnitOutdated = true },
            Row(1, 17, 6) with { AboRhDiscrepancyBlocksIssue = true }
        };

        Assert.Equal([11L], IssueRun.NextForPatient(ready, 1, null, []).Select(r => r.BloodUnitId));
        Assert.Equal(6, IssueRun.HeldBackForPatient(ready, 1, null, []));
        Assert.True(IssueRun.OffersRoutineIssue(ready[0]));
        Assert.All(ready.Skip(1), r => Assert.False(IssueRun.OffersRoutineIssue(r)));
    }

    private static InTransitWorkItemDto Transit(long issueId, long patientId, string? cooler, string? destination, int minutes) =>
        new(issueId, patientId, "MRN1", "Run, Pat", issueId, $"W{issueId:000}", "O+", false, null, null, false,
            Start.AddMinutes(minutes), null, false, cooler, destination, IssueStatus.Issued, ProductName: "Red Blood Cells");

    [Fact]
    public void NextToReceive_OffersTheRestOfTheSameCooler_ForThatPatient_EarliestFirst()
    {
        var transit = new[]
        {
            Transit(1, 1, "C-7", "ICU", 0),
            Transit(2, 1, "c-7 ", "ICU", 5),
            Transit(3, 1, "C-7", "ICU", 2),
            Transit(4, 1, "C-9", "ICU", 1),
            Transit(5, 2, "C-7", "ICU", 1),
            Transit(6, 1, "C-7", "ICU", 3) with { DowntimeBackEntry = true }
        };

        var next = IssueRun.NextToReceive(transit, patientId: 1, coolerId: "C-7", destination: "ICU", receivedIssueIds: [1]);

        Assert.Equal([3L, 2L], next.Select(r => r.IssueId));
    }

    [Fact]
    public void NextToReceive_WithoutACooler_MatchesTheDestination_AndOnlyUncooledUnits()
    {
        var transit = new[]
        {
            Transit(1, 1, null, "4 West", 0),
            Transit(2, 1, null, "4 west", 1),
            Transit(3, 1, null, "ICU", 2),
            Transit(4, 1, "C-7", "4 West", 3)
        };

        Assert.Equal([2L], IssueRun.NextToReceive(transit, 1, null, "4 West", [1]).Select(r => r.IssueId));
        Assert.Empty(IssueRun.NextToReceive(transit, 1, null, "4 West", [1, 2]));
    }
    private static OutstandingIssueWorkItemDto Bedside(long issueId, long patientId, int minutes, bool received = true, bool started = false) =>
        new(issueId, patientId, "MRN1", "Run, Pat", "1980-01-01", issueId, $"W{issueId:000}", "O+", false, null, null, false,
            Start.AddMinutes(minutes), received ? Start.AddMinutes(minutes + 10) : null, received ? "RN" : null, "C-7", "ICU",
            IssueStatus.Issued, ProductName: "Red Blood Cells", TransfusionStartedUtc: started ? Start.AddMinutes(minutes + 20) : null);

    // TEST-BB-458: after a return, the rest of the same cooler (or destination, without one) for that patient is
    // offered, earliest first; a unit whose transfusion started is not.
    [Fact]
    public void NextToReturn_OffersTheRestOfTheCooler_ForThatPatient_NotOnesTransfusing()
    {
        var outstanding = new[]
        {
            Bedside(1, 1, 0),
            Bedside(2, 1, 5, received: false),
            Bedside(3, 1, 2),
            Bedside(4, 1, 1, started: true),
            Bedside(5, 2, 1),
            Bedside(6, 1, 3) with { CoolerId = "C-9" },
            Bedside(7, 1, 4) with { CoolerId = null, IssuedToLocation = "ICU" },
            Bedside(8, 1, 6) with { CoolerId = null, IssuedToLocation = "4 West" }
        };

        Assert.Equal([3L, 2L], IssueRun.NextToReturn(outstanding, 1, " c-7", "ICU", [1]).Select(r => r.IssueId));
        Assert.Equal([7L], IssueRun.NextToReturn(outstanding, 1, null, "icu", []).Select(r => r.IssueId));
        Assert.Empty(IssueRun.NextToReturn(outstanding, 1, "C-7", "ICU", [1, 2, 3]));
    }

    [Fact]
    public void NextAtBedside_PutsUnitsStillTransfusingFirst_ThenReceivedOnes_ForThatPatientOnly()
    {
        var outstanding = new[]
        {
            Bedside(1, 1, 0),
            Bedside(2, 1, 1, started: true),
            Bedside(3, 1, 2),
            Bedside(4, 1, 3, received: false),
            Bedside(5, 2, 0),
            Bedside(6, 1, 4)
        };

        var next = IssueRun.NextAtBedside(outstanding, patientId: 1, issueOnForm: 1, handledIssueIds: [6]);

        Assert.Equal([2L, 3L], next.Select(r => r.IssueId));
    }
    [Fact]
    public void APickedUnit_FillsItsReservedPatient_OrSaysWhoseItIs()
    {
        var ready = new[] { Row(1, 11, 0), Row(2, 21, 0) with { PatientDisplayName = "Other, Pat", MedicalRecordNumber = "MRN2" } };

        Assert.Equal(1, IssueRun.ForPickedUnit(ready, 11, patientOnForm: 0).Fill!.PatientId);
        Assert.Equal(11, IssueRun.ForPickedUnit(ready, 11, patientOnForm: 1).Fill!.BloodUnitId);

        var other = IssueRun.ForPickedUnit(ready, 21, patientOnForm: 1);
        Assert.Null(other.Fill);
        Assert.Equal("Unit W021 is reserved for Other, Pat (MRN2), not the patient on this form.", other.Notice);

        Assert.Equal((null, null), IssueRun.ForPickedUnit(ready, 99, 1));
        Assert.Equal((null, null), IssueRun.ForPickedUnit(ready, null, 1));
    }
    [Fact]
    public void RoutineIssueBlocks_NameWhatStopsRoutineIssue_AndAreEmptyWhenReady()
    {
        Assert.Empty(IssueRun.RoutineIssueBlocks(Row(1, 11, 0)));

        var blocked = Row(1, 12, 0) with
        {
            NeedsSecondAbo = true,
            CrossmatchBlocksRoutineIssue = true,
            CrossmatchNotice = "A compatible, unexpired crossmatch is required for this product.",
            UnitExpiresUtc = Start.AddDays(3)
        };
        Assert.Equal(
            [ReadyToIssueFormCue.SecondAbo, "A compatible, unexpired crossmatch is required for this product."],
            IssueRun.RoutineIssueBlocks(blocked));
        Assert.Equal(IssueRun.OffersRoutineIssue(blocked), IssueRun.RoutineIssueBlocks(blocked).Count == 0);
    }
    [Fact]
    public void AnIssuedUnitsNextStep_FollowsReceiptThenStartThenComplete_AndReturnStopsOnceStarted()
    {
        var inTransit = Bedside(1, 1, 0, received: false);
        var received = Bedside(2, 1, 0);
        var running = Bedside(3, 1, 0, started: true);

        Assert.Equal(IssuedNextStep.ReceiveOnWard, IssueRun.NextStepForIssued(inTransit));
        Assert.Equal(IssuedNextStep.StartTransfusion, IssueRun.NextStepForIssued(received));
        Assert.Equal(IssuedNextStep.CompleteTransfusion, IssueRun.NextStepForIssued(running));
        Assert.True(IssueRun.CanReturn(inTransit));
        Assert.True(IssueRun.CanReturn(received));
        Assert.False(IssueRun.CanReturn(running));

        // Started without a recorded receipt: the running transfusion still comes first.
        Assert.Equal(IssuedNextStep.CompleteTransfusion, IssueRun.NextStepForIssued(Bedside(4, 1, 0, received: false, started: true)));
    }
    [Fact]
    public void NearExpiry_OffersIssueForAReadyReservedUnit_AndAllocateWhenOrdersAreOpen()
    {
        var ready = Row(1, 11, 0);
        var blocked = ready with { NeedsSecondAbo = true };

        Assert.Equal(NearExpiryNextStep.Issue, IssueRun.ForNearExpiry(reserved: true, ready, openOrdersForProduct: 3));
        Assert.Equal(NearExpiryNextStep.None, IssueRun.ForNearExpiry(reserved: true, blocked, openOrdersForProduct: 3));
        Assert.Equal(NearExpiryNextStep.None, IssueRun.ForNearExpiry(reserved: true, readyRow: null, openOrdersForProduct: 3));
        Assert.Equal(NearExpiryNextStep.Allocate, IssueRun.ForNearExpiry(reserved: false, readyRow: null, openOrdersForProduct: 2));
        Assert.Equal(NearExpiryNextStep.None, IssueRun.ForNearExpiry(reserved: false, readyRow: null, openOrdersForProduct: 0));
    }
}
