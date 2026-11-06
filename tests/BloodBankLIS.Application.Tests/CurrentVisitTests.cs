using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-503: the chart header shows the latest open visit, counts other open visits, and describes the visit with
// only what is recorded.
public class CurrentVisitTests
{
    private static readonly DateTime T = new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);

    private static EncounterDto Visit(long id, int hoursAgo, EncounterStatus status = EncounterStatus.Active, bool discharged = false,
        EncounterType type = EncounterType.Inpatient, string? location = "ICU 4", string? attending = "Dr. Smith") =>
        new(id, 1, $"V{id}", null, type, status, T.AddHours(-hoursAgo), discharged ? T : null, null, attending, "ED", location,
            null, null, null, null, T, "test");

    [Fact]
    public void Picks_TheLatestOpenVisit_AndCountsTheOthers()
    {
        var (visit, others) = CurrentVisit.For([Visit(1, 48), Visit(2, 5), Visit(3, 1, discharged: true), Visit(4, 2, EncounterStatus.Cancelled)]);
        Assert.Equal(2, visit!.Id);
        Assert.Equal(1, others);

        var (none, zero) = CurrentVisit.For([Visit(3, 1, EncounterStatus.Discharged, discharged: true)]);
        Assert.Null(none);
        Assert.Equal(0, zero);
    }

    [Fact]
    public void Describe_UsesOnlyWhatIsRecorded()
    {
        Assert.Equal("Inpatient · ICU 4 · Attending Dr. Smith", CurrentVisit.Describe(Visit(1, 1)));
        Assert.Equal("ED", CurrentVisit.Describe(Visit(1, 1, type: EncounterType.Unknown, location: null, attending: " ")));
    }
}
