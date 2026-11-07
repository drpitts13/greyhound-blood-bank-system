using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-487: the alert inbox groups open alerts by kind (help-page names, most severe kind first) and says how long
// each has been open.
public class AlertInboxViewTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private static AlertDto Alert(long id, string source, AlertSeverity severity) =>
        new(id, source, severity, "t", "m", null, Now.AddHours(-1), null, null, null, null);

    [Fact]
    public void Kinds_AreNamedAsTheHelpPage_MostSevereFirst()
    {
        var kinds = AlertInboxView.Kinds([
            Alert(1, AlertRule.NearExpiry, AlertSeverity.Info),
            Alert(2, AlertRule.NearExpiry, AlertSeverity.Warning),
            Alert(3, AlertRule.TemperatureExcursion, AlertSeverity.Critical),
            Alert(4, AlertRule.MinimumStockShortage, AlertSeverity.Warning),
            Alert(5, "something-new", AlertSeverity.Info)
        ]);

        Assert.Equal(
            ["Temperature excursion (1)", "Below minimum stock (1)", "Near expiry (2)", "something-new (1)"],
            kinds.Select(k => $"{k.Label} ({k.Count})"));
        Assert.Empty(AlertInboxView.Kinds([]));
    }

    [Fact]
    public void OpenFor_UsesMinutesThenHoursThenDays()
    {
        Assert.Equal("just now", AlertInboxView.OpenFor(Now.AddSeconds(-20), Now));
        Assert.Equal("45 min", AlertInboxView.OpenFor(Now.AddMinutes(-45), Now));
        Assert.Equal("5 h", AlertInboxView.OpenFor(Now.AddHours(-5).AddMinutes(-30), Now));
        Assert.Equal("47 h", AlertInboxView.OpenFor(Now.AddHours(-47), Now));
        Assert.Equal("3 d", AlertInboxView.OpenFor(Now.AddDays(-3).AddHours(-2), Now));
    }
}
