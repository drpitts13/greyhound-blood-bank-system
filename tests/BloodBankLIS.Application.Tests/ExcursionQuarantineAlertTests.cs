using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class ExcursionQuarantineAlertTests
{
    private static Alert Raised(long locationId) => new()
    {
        Source = AlertRule.TemperatureExcursion,
        Key = $"TEMP-QUAR:{locationId}:202611121222",
        Severity = AlertSeverity.Critical,
        Title = "30 unit(s) quarantined at FRIDGE-1",
        Message = "Storage excursion at FRIDGE-1 lasted more than 30 minutes. Deviation DEV-3. Review each unit before release.",
        Link = "/inventory?board=quarantine"
    };

    // TEST-BB-1108 (end-user testing pass 94): the alert says how many units at its location are still held for an
    // excursion, and clears when none is.
    [Fact]
    public void Now_CountsUnitsStillHeld_AndClearsWhenNone()
    {
        var now = ExcursionQuarantineAlert.Now(Raised(7), new Dictionary<long, int> { [7] = 1, [9] = 4 });
        Assert.Equal("1 unit(s) still quarantined at FRIDGE-1", now!.Title);
        Assert.Equal(Raised(7).Message, now.Message);
        Assert.Null(ExcursionQuarantineAlert.Now(Raised(7), new Dictionary<long, int> { [9] = 4 }));
        Assert.Equal(7, ExcursionQuarantineAlert.LocationId("TEMP-QUAR:7:202611121222"));
        Assert.Null(ExcursionQuarantineAlert.LocationId("TEMP:DEV-3"));
    }

    // TEST-BB-1148: of two open alerts for one location (two quarantine runs in one excursion), the first is kept and the
    // other closed; another location keeps its own (end-user testing pass 127).
    [Fact]
    public void NowOncePerLocation_KeepsOneAlertPerLocation()
    {
        var held = new Dictionary<long, int> { [7] = 1, [9] = 2 };
        var seen = new HashSet<long>();
        Assert.NotNull(ExcursionQuarantineAlert.NowOncePerLocation(Raised(7), held, seen));
        Assert.Null(ExcursionQuarantineAlert.NowOncePerLocation(Raised(7), held, seen));
        Assert.NotNull(ExcursionQuarantineAlert.NowOncePerLocation(Raised(9), held, seen));
    }
}
