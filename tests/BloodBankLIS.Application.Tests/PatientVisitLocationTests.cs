using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-481: the suggested issue destination is the reservation's open visit location, else the latest open visit
// with a location; discharged, cancelled, or location-less visits suggest nothing.
public class PatientVisitLocationTests
{
    private static readonly DateTime T = new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);

    private static Encounter Visit(long id, string? location, int hoursAgo, EncounterStatus status = EncounterStatus.Active, bool discharged = false) =>
        new() { Id = id, PatientId = 1, VisitNumber = $"V{id}", CurrentLocation = location, AdmitUtc = T.AddHours(-hoursAgo), Status = status, DischargeUtc = discharged ? T : null };

    [Fact]
    public void ReservationVisit_WinsWhenOpen_ElseLatestOpenVisit()
    {
        var visits = new[] { Visit(1, "Ward 5", 48), Visit(2, " ICU 4 ", 2), Visit(3, "ED", 1, EncounterStatus.Discharged, discharged: true) };

        Assert.Equal("Ward 5", PatientVisitLocation.For(1, visits));
        Assert.Equal("ICU 4", PatientVisitLocation.For(null, visits));
        Assert.Equal("ICU 4", PatientVisitLocation.For(3, visits));
    }

    [Fact]
    public void NoOpenVisitWithALocation_SuggestsNothing()
    {
        Assert.Null(PatientVisitLocation.For(null, []));
        Assert.Null(PatientVisitLocation.For(1, [Visit(1, null, 2), Visit(2, "OR", 1, EncounterStatus.Cancelled)]));
    }
}
