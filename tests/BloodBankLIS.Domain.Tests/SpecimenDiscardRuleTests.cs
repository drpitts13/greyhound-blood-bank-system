using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-587: a tube may be discarded after its expiration plus the retention days; earlier, or with no period set, only
// with a reason (a warning); never while an order on it is unfinished or a reaction investigation on it is open.
public class SpecimenDiscardRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void KeepUntil_IsExpirationPlusRetentionDays_OrNotSet()
    {
        Assert.Equal(Now.AddDays(7), SpecimenDiscardRule.KeepUntil(Now.AddDays(-3), Now, 7));
        Assert.Equal(Now.AddDays(7), SpecimenDiscardRule.KeepUntil(Now, null, 7));
        Assert.Null(SpecimenDiscardRule.KeepUntil(Now, Now, 0));
        Assert.True(SpecimenDiscardRule.Due(Now, Now));
        Assert.False(SpecimenDiscardRule.Due(Now.AddMinutes(1), Now));
        Assert.False(SpecimenDiscardRule.Due(null, Now));
    }

    [Fact]
    public void Evaluate_KeepsANeededTube_AndAsksForAReasonWhenEarly()
    {
        Assert.Equal(RuleSeverity.Pass, SpecimenDiscardRule.Evaluate(null, [], false, Now.AddDays(-1), Now, null).Severity);

        var early = SpecimenDiscardRule.Evaluate(null, [], false, Now.AddDays(1), Now, null);
        Assert.Equal(RuleSeverity.HardStop, early.Severity);
        Assert.Contains("give a reason to discard it early", early.Message);
        Assert.Equal(RuleSeverity.Warning, SpecimenDiscardRule.Evaluate(null, [], false, Now.AddDays(1), Now, "Label damaged").Severity);

        Assert.Contains("No retention period is set", SpecimenDiscardRule.Evaluate(null, [], false, null, Now, " ").Message);
        Assert.Equal(RuleSeverity.Warning, SpecimenDiscardRule.Evaluate(null, [], false, null, Now, "Duplicate draw").Severity);

        var onOrder = SpecimenDiscardRule.Evaluate(null, ["ORD2", "ORD1"], false, Now.AddDays(-1), Now, "Clean-out");
        Assert.Equal(RuleSeverity.HardStop, onOrder.Severity);
        Assert.Contains("Orders ORD2, ORD1 on this tube are not finished", onOrder.Message);
        Assert.Contains("Order ORD1 on this tube is not finished", SpecimenDiscardRule.Evaluate(null, ["ORD1"], false, null, Now, "x").Message);

        Assert.Contains("reaction investigation", SpecimenDiscardRule.Evaluate(null, [], true, Now.AddDays(-1), Now, "x").Message);
        Assert.Contains("already discarded", SpecimenDiscardRule.Evaluate(Now, [], false, Now.AddDays(-1), Now, null).Message);
        Assert.Contains("at most 500", SpecimenDiscardRule.Evaluate(null, [], false, Now.AddDays(1), Now, new string('x', 501)).Message);
    }
}
