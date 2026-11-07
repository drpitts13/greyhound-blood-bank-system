using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-299: alert lifecycle and condition reconciliation.
public class AlertRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private static AlertCandidate Found(string key) =>
        new(AlertRule.MinimumStockShortage, key, AlertSeverity.Warning, key, "msg");

    private static Alert Active(string key, bool acknowledged = false) => new()
    {
        Source = AlertRule.MinimumStockShortage,
        Key = key,
        AcknowledgedUtc = acknowledged ? Now : null
    };

    [Fact]
    public void Reconcile_RaisesNewKeys_AndResolvesClearedOnes()
    {
        var (raise, resolve) = AlertRule.Reconcile(
            [Active("PAR:1"), Active("PAR:2")],
            [Found("PAR:2"), Found("PAR:3")]);

        Assert.Equal(["PAR:3"], raise.Select(c => c.Key));
        Assert.Equal(["PAR:1"], resolve.Select(a => a.Key));
    }

    [Fact]
    public void Reconcile_DoesNotReRaiseAnAcknowledgedConditionThatPersists()
    {
        var (raise, resolve) = AlertRule.Reconcile([Active("PAR:1", acknowledged: true)], [Found("PAR:1")]);
        Assert.Empty(raise);
        Assert.Empty(resolve);
    }

    [Fact]
    public void Reconcile_RaisesADuplicateFindingOnce()
    {
        var (raise, _) = AlertRule.Reconcile([], [Found("PAR:9"), Found("PAR:9")]);
        Assert.Single(raise);
    }

    [Fact]
    public void Reconcile_NothingFound_ResolvesEveryActiveAlert()
    {
        var (raise, resolve) = AlertRule.Reconcile([Active("PAR:1"), Active("PAR:2", acknowledged: true)], []);
        Assert.Empty(raise);
        Assert.Equal(2, resolve.Count);
    }

    [Fact]
    public void OpenMeansActiveAndUnacknowledged()
    {
        Assert.True(AlertRule.IsOpen(new Alert()));
        Assert.False(AlertRule.IsOpen(new Alert { AcknowledgedUtc = Now }));
        Assert.True(AlertRule.IsActive(new Alert { AcknowledgedUtc = Now }));
        Assert.False(AlertRule.IsActive(new Alert { ResolvedUtc = Now }));
        Assert.False(AlertRule.IsOpen(new Alert { ResolvedUtc = Now }));
    }

    // TEST-BB-511: an alert whose condition still holds but reads differently is refreshed with what is true now; an
    // unchanged one, and one whose condition cleared, are not.
    [Fact]
    public void Changed_ListsAlertsThatNowReadDifferently()
    {
        var same = Active("PAR:1");
        same.Severity = AlertSeverity.Warning;
        same.Title = "PAR:1";
        same.Message = "msg";
        var differs = Active("PAR:2", acknowledged: true);
        differs.Severity = AlertSeverity.Warning;
        differs.Title = "PAR:2";
        differs.Message = "old";
        var now = Found("PAR:2") with { Severity = AlertSeverity.Info };

        var changed = AlertRule.Changed([same, differs, Active("PAR:3")], [Found("PAR:1"), now]);

        var (alert, current) = Assert.Single(changed);
        Assert.Same(differs, alert);
        Assert.Equal((AlertSeverity.Info, "msg"), (current.Severity, current.Message));
    }
}
