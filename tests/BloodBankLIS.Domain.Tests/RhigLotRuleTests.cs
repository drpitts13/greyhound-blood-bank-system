using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class RhigLotRuleTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly DateTime Given = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    // TEST-BB-1003: a lot is received with a lot number, product, expiry not passed and vials, once; with no lot
    // registered, the lot given is taken as typed; once lots are registered, the lot given must be registered, active and
    // in date on the day given, and giving more vials than received is a warning, not a stop.
    [Fact]
    public void Receive_AndUse()
    {
        Assert.Equal(RuleSeverity.HardStop, RhigLotRule.EvaluateReceive("L1", "", Today.AddYears(1), 10, Today, false).Severity);
        Assert.Contains("already registered", RhigLotRule.EvaluateReceive("L1", "RhIG 300 ug", Today.AddYears(1), 10, Today, true).Message);
        Assert.Contains("expired", RhigLotRule.EvaluateReceive("L1", "RhIG 300 ug", Today.AddDays(-1), 10, Today, false).Message);
        Assert.Equal(RuleSeverity.HardStop, RhigLotRule.EvaluateReceive("L1", "RhIG 300 ug", Today.AddYears(1), 0, Today, false).Severity);
        Assert.Equal(RuleSeverity.Pass, RhigLotRule.EvaluateReceive("L1", "RhIG 300 ug", Today.AddYears(1), 10, Today, false).Severity);

        Assert.Equal(RuleSeverity.Pass, RhigLotRule.EvaluateUse(false, "TYPED", null, Given, 1, 0).Severity);
        Assert.Contains("not in the RhIG lot register", RhigLotRule.EvaluateUse(true, "TYPO", null, Given, 1, 0).Message);
        Assert.Contains("retired", RhigLotRule.EvaluateUse(true, "L1", (false, Today.AddYears(1), 10), Given, 1, 0).Message);
        Assert.Contains("expired 2026-11-11, before it was given", RhigLotRule.EvaluateUse(true, "L1", (true, Today.AddDays(-1), 10), Given, 1, 0).Message);
        Assert.Equal(RuleSeverity.Pass, RhigLotRule.EvaluateUse(true, "L1", (true, Today, 10), Given, 1, 0).Severity);
        var over = RhigLotRule.EvaluateUse(true, "L1", (true, Today.AddYears(1), 2), Given, 2, 1);
        Assert.Equal(RuleSeverity.Warning, over.Severity);
        Assert.Contains("3 vials given from lot L1, more than the 2 received", over.Message);
    }
}
