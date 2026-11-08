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
}
