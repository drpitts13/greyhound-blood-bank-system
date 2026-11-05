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
}
