using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class ExpiredUnitsAlertTests
{
    private static Alert Raised() => new()
    {
        Source = AlertRule.UnitsExpired,
        Key = "EXPIRED:20261110150814",
        Severity = AlertSeverity.Warning,
        Title = "8 unit(s) expired",
        Message = "The scheduled sweep moved 8 unit(s) to Expired at 2026-11-10 15:08 UTC. Remove them from storage.",
        Link = "/inventory?status=Expired"
    };

    // TEST-BB-1109 (end-user testing pass 95): the expired-units alert stays as raised while every unit is still on a
    // shelf, says how many are left once some are removed (read from the count the sweep wrote, so it stays right on later
    // scans), and closes when none of that sweep's units is still marked Expired.
    [Fact]
    public void Now_CountsUnitsStillToRemove_AndClosesWhenNone()
    {
        Assert.Equal("8 unit(s) expired", ExpiredUnitsAlert.Now(Raised(), new Dictionary<string, int> { ["20261110150814"] = 8 })!.Title);
        var partly = ExpiredUnitsAlert.Now(Raised(), new Dictionary<string, int> { ["20261110150814"] = 2 })!;
        Assert.Equal("2 of 8 expired unit(s) still to remove", partly.Title);
        var later = Raised();
        later.Title = partly.Title;
        Assert.Equal("1 of 8 expired unit(s) still to remove", ExpiredUnitsAlert.Now(later, new Dictionary<string, int> { ["20261110150814"] = 1 })!.Title);
        Assert.Null(ExpiredUnitsAlert.Now(Raised(), new Dictionary<string, int> { ["20261110150814"] = 0 }));
        Assert.Null(ExpiredUnitsAlert.Now(Raised(), new Dictionary<string, int> { ["20261111150000"] = 3 }));
    }
    // TEST-BB-1253 (end-user testing pass 229): an expired-units alert opens Discard expired; one raised with the old
    // Inventory link moves to it on the next scan.
    [Fact]
    public void Now_MovesTheOldLinkToDiscardExpired()
    {
        Assert.Equal("/inventory/discard-expired",
            ExpiredUnitsAlert.Now(Raised(), new Dictionary<string, int> { ["20261110150814"] = 8 })!.Link);
        Assert.Equal("/inventory/discard-expired", AlertScanner.UnitsExpiredAlert(1, new DateTime(2026, 11, 17, 7, 1, 0, DateTimeKind.Utc), 0, null).Link);
    }
}
