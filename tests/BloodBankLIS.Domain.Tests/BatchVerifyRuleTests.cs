using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-497: which results may join a batch (OCD-061).
public class BatchVerifyRuleTests
{
    private static readonly AboRh OPos = new(AboGroup.O, RhType.Positive);

    private static BatchVerifyRule.Facts Facts(
        string test = "ABSC", string? value = "Negative", ResultStatus status = ResultStatus.PendingVerification,
        string? enteredBy = "tech1", AboRh? current = null, bool antibodies = false, bool requirements = false) =>
        new(test, value, status, enteredBy, "tech2", current, antibodies, requirements);

    [Fact]
    public void PlainNegatives_AndMatchingTypes_Qualify()
    {
        Assert.Equal(RuleSeverity.Pass, BatchVerifyRule.Evaluate(Facts()).Severity);
        Assert.Equal(RuleSeverity.Pass, BatchVerifyRule.Evaluate(Facts(test: "dat", value: " negative ", status: ResultStatus.Entered)).Severity);
        Assert.Equal(RuleSeverity.Pass, BatchVerifyRule.Evaluate(Facts(test: "ABORH", value: AboRhResultValue.Format(OPos), current: OPos)).Severity);
    }

    [Fact]
    public void EverythingElse_IsVerifiedOnItsOwn()
    {
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(test: "XM")).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(value: "Positive")).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(value: "Negative, see comment")).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(status: ResultStatus.Corrected)).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(enteredBy: "TECH2")).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(enteredBy: null)).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(antibodies: true)).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(requirements: true)).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(test: "ABORH", value: AboRhResultValue.Format(OPos))).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(test: "ABORH", value: AboRhResultValue.Format(new AboRh(AboGroup.O, RhType.Negative)), current: OPos)).Severity);
        Assert.Equal(RuleSeverity.HardStop, BatchVerifyRule.Evaluate(Facts(test: "ABORH", value: "garbled", current: OPos)).Severity);
    }
}
