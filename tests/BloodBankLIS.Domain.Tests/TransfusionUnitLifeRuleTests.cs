using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-342
public class TransfusionUnitLifeRuleTests
{
    private static readonly DateTime Issued = new(2026, 11, 2, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Expires = new(2026, 11, 2, 23, 59, 0, DateTimeKind.Utc);

    [Fact]
    public void StartedInDate_Passes_EvenIfChartedAfterExpiry() =>
        Assert.False(TransfusionDocumentationRule.EvaluateAgainstUnit(Issued.AddHours(2), Issued, Expires).IsHardStopped);

    [Fact]
    public void StartedAfterExpiry_IsRefused()
    {
        var r = TransfusionDocumentationRule.EvaluateAgainstUnit(Expires.AddMinutes(1), Issued, Expires);
        Assert.Contains(r.HardStops, x => x.Code == IsbtErrorCodes.ComponentExpired);
    }

    [Fact]
    public void StartedBeforeIssue_IsRefused_BeyondTheClockAllowance()
    {
        Assert.True(TransfusionDocumentationRule.EvaluateAgainstUnit(Issued.AddMinutes(-30), Issued, Expires).IsHardStopped);
        Assert.False(TransfusionDocumentationRule.EvaluateAgainstUnit(Issued.AddMinutes(-3), Issued, Expires).IsHardStopped);
    }
}
