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

    private static AlertDto Titled(long id, string source, AlertSeverity severity, string title, int minutesAgo, string? link = null) =>
        new(id, source, severity, title, "m", link, Now.AddMinutes(-minutesAgo), null, null, null, null);

    // TEST-BB-813: three or more alerts of one kind and severity raised together, sharing the title before ": ", are one
    // row naming each item, placed where the first was; two stay apart, as do the same kind raised hours apart, another
    // severity, and critical alerts.
    [Fact]
    public void Rows_GroupAlertsOfOneKindRaisedTogether()
    {
        var rows = AlertInboxView.Rows([
            Titled(1, AlertRule.MtpStillOpen, AlertSeverity.Warning, "Massive transfusion protocol still open: MRN MRN0021", 300),
            Titled(2, AlertRule.ReagentQc, AlertSeverity.Warning, "Reagent QC overdue: lot A1", 60, "/reagents?lot=A1"),
            Titled(3, AlertRule.ReagentQc, AlertSeverity.Warning, "Reagent QC overdue: lot B2", 60),
            Titled(4, AlertRule.ReagentQc, AlertSeverity.Warning, "Reagent QC overdue: lot C3", 58),
            Titled(5, AlertRule.ReagentQc, AlertSeverity.Warning, "Reagent QC overdue: lot D4", 600),
            Titled(6, AlertRule.NearExpiry, AlertSeverity.Info, "Near expiry: W1", 30),
            Titled(7, AlertRule.NearExpiry, AlertSeverity.Info, "Near expiry: W2", 30),
            Titled(8, AlertRule.TemperatureExcursion, AlertSeverity.Critical, "Temperature excursion: Fridge 1", 10),
            Titled(9, AlertRule.TemperatureExcursion, AlertSeverity.Critical, "Temperature excursion: Fridge 2", 10),
            Titled(10, AlertRule.TemperatureExcursion, AlertSeverity.Critical, "Temperature excursion: Fridge 3", 10)
        ]);

        Assert.Equal(
            ["Massive transfusion protocol still open: MRN MRN0021", "Reagent QC overdue (3)", "Reagent QC overdue: lot D4",
             "Near expiry: W1", "Near expiry: W2", "Temperature excursion: Fridge 1", "Temperature excursion: Fridge 2", "Temperature excursion: Fridge 3"],
            rows.Select(r => r.IsGroup ? $"{r.Heading} ({r.Alerts.Count})" : r.Heading));
        var group = rows[1];
        Assert.Equal(["lot A1", "lot B2", "lot C3"], group.Items.Select(i => i.Label));
        Assert.Equal(("/reagents?lot=A1", Now.AddMinutes(-60), AlertSeverity.Warning), (group.Items[0].Alert.Link, group.RaisedUtc, group.Severity));
        Assert.Empty(AlertInboxView.Rows([]));
    }

}
