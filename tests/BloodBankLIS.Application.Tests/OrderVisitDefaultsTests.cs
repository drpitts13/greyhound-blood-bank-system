using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-486: a new order takes its ordering location from the visit only when exactly one active location matches,
// and the attending provider is found for the offer.
public class OrderVisitDefaultsTests
{
    private static EncounterDto Visit(string? current, string? admission = null, long? attending = null) =>
        new(1, 1, "V1", null, EncounterType.Inpatient, EncounterStatus.Active, null, null, attending, null, admission, current,
            null, null, null, null, DateTime.UtcNow, "test");

    private static OrderingLocationRefDto Loc(long id, string code, string? name, bool active = true, string? hl7 = null) =>
        new(id, code, name, null, active, name ?? code, hl7);

    private static readonly OrderingLocationRefDto[] Locations =
    [
        Loc(1, "ED", "Emergency Department"),
        Loc(2, "ICU4", "ICU 4"),
        Loc(3, "OR", "Operating Room"),
        Loc(4, "ONC", "Oncology", active: false),
        Loc(5, "MED", "Ward 5"),
        Loc(6, "SURG", "Ward 5"),
        Loc(7, "4W", "Fourth West", hl7: "4W MED/SURG")
    ];

    [Fact]
    public void LocationFor_MatchesCodeOrName_OnlyWhenExactlyOneActiveLocation()
    {
        Assert.Equal(2, OrderVisitDefaults.LocationFor(Visit(" icu 4 "), Locations));
        Assert.Equal(1, OrderVisitDefaults.LocationFor(Visit("ED"), Locations));
        Assert.Equal(3, OrderVisitDefaults.LocationFor(Visit(null, admission: "OR"), Locations));
        Assert.Equal(7, OrderVisitDefaults.LocationFor(Visit("4W Med/Surg"), Locations));
        Assert.Null(OrderVisitDefaults.LocationFor(Visit("Ward 5"), Locations));
        Assert.Null(OrderVisitDefaults.LocationFor(Visit("Oncology"), Locations));
        Assert.Null(OrderVisitDefaults.LocationFor(Visit("Radiology"), Locations));
        Assert.Null(OrderVisitDefaults.LocationFor(Visit(null), Locations));
        Assert.Null(OrderVisitDefaults.LocationFor(null, Locations));
        Assert.Equal("ICU 4", OrderVisitDefaults.VisitLocation(Visit(" ", admission: " ICU 4 ")));
    }

    [Fact]
    public void AttendingFor_FindsTheVisitAttending_OrNothing()
    {
        OrderingProviderRefDto[] providers = [new(7, "P7", "Dr Seven", null, null), new(8, "P8", "Dr Eight", null, null)];

        Assert.Equal("Dr Eight", OrderVisitDefaults.AttendingFor(Visit("ED", attending: 8), providers)?.Name);
        Assert.Null(OrderVisitDefaults.AttendingFor(Visit("ED", attending: 99), providers));
        Assert.Null(OrderVisitDefaults.AttendingFor(Visit("ED"), providers));
    }
}
