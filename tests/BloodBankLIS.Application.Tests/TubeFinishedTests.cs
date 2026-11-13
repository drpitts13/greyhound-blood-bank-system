using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-586: after a save, the bench is offered to store the tube when its last line is done and it was never put away.
public class TubeFinishedTests
{
    private static TestWorkItemDto Line(long id, string accession, string? stored = null) =>
        new(id, 1, 1, "Bench, Pat", "MRN1", "ABSC", "Antibody screen", "ORD1", OrderPriority.Routine, DateTime.UtcNow, accession, 1,
            SpecimenStatus.Accepted, DateTime.UtcNow.AddDays(2), false, "O+", false, null, null, null, null, null,
            null, true, null, SpecimenStorageLocation: stored);

    [Fact]
    public void TubeFinished_OnlyWhenNoLineIsLeft_AndTheTubeIsNotStored()
    {
        var saved = Line(1, "ACC100");
        Assert.Equal("ACC100", TestWorklistView.TubeFinished(saved, [Line(2, "ACC200")]));
        Assert.Null(TestWorklistView.TubeFinished(saved, [Line(3, "acc100")]));
        Assert.Null(TestWorklistView.TubeFinished(Line(1, "ACC100", stored: "Rack 3"), []));
        Assert.Null(TestWorklistView.TubeFinished(null, []));
        Assert.Null(TestWorklistView.TubeFinished(Line(1, " "), []));
    }

    private static TestWorkItemDto Row(long id, long patientId, long specimenId) =>
        new(id, 1, patientId, "Bench, Pat", "MRN1", "DAT", "DAT", "ORD1", OrderPriority.Stat, DateTime.UtcNow, $"ACC{specimenId}", specimenId,
            SpecimenStatus.Accepted, DateTime.UtcNow.AddDays(2), false, "O+", false, null, null, null, null, null,
            null, true, null);

    // TEST-BB-1055: after a save the next test is one still open on the same tube, else for the same patient, else the
    // row below (end-user testing pass 39: a reaction workup's last row sent the bench to another patient).
    [Fact]
    public void NextToEnter_PrefersTheSameTube_ThenTheSamePatient()
    {
        // Shown: patient 1 tube 10 (rows 1, 2), patient 1 tube 11 (rows 3, 4), patient 2 (row 5).
        var rows = new[] { Row(1, 1, 10), Row(2, 1, 10), Row(3, 1, 11), Row(4, 1, 11), Row(5, 2, 20) };
        long[] shown = [1, 2, 3, 4, 5];
        Assert.Equal(3, TestWorklistView.NextToEnter(rows[3], shown, rows.Where(r => r.OrderLineId != 4).ToList())!.OrderLineId);
        Assert.Equal(1, TestWorklistView.NextToEnter(rows[3], shown, rows.Where(r => r.OrderLineId is 1 or 5).ToList())!.OrderLineId);
        Assert.Equal(5, TestWorklistView.NextToEnter(rows[3], shown, rows.Where(r => r.OrderLineId == 5).ToList())!.OrderLineId);
        Assert.Null(TestWorklistView.NextToEnter(rows[4], shown, []));
    }
}
