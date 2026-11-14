using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class UnitsExpiredAlertTests
{
    private static readonly DateTime Swept = new(2026, 11, 9, 14, 30, 0, DateTimeKind.Utc);

    // TEST-BB-705: when the sweep ended reservations, the alert is critical, names each unit and patient (five, then how
    // many more), and opens Blood orders to prepare; without reservations it stays a warning about the expired units.
    [Fact]
    public void ReservationsEnded_NameUnitsAndPatients_AndOpenBloodOrders()
    {
        var alert = AlertScanner.UnitsExpiredAlert(2, Swept, 2, ["W0001 for MRN0001", "W0002 for MRN0007"]);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal("2 unit(s) expired, 2 reserved for patients", alert.Title);
        Assert.Contains("reserved for patients (W0001 for MRN0001, W0002 for MRN0007); those reservations have ended", alert.Message);
        Assert.Equal("/blood-orders", alert.Link);

        var many = AlertScanner.UnitsExpiredAlert(7, Swept, 7, Enumerable.Range(1, 7).Select(i => $"W{i} for MRN{i}").ToList());
        Assert.Contains("W5 for MRN5, and 2 more)", many.Message);
        Assert.DoesNotContain("W6 for MRN6", many.Message);

        var plain = AlertScanner.UnitsExpiredAlert(3, Swept, 0, []);
        Assert.Equal(AlertSeverity.Warning, plain.Severity);
        Assert.Equal("3 unit(s) expired", plain.Title);
        Assert.DoesNotContain("reserved", plain.Message);
        Assert.Equal("/inventory?status=Expired", plain.Link);
    }
}
