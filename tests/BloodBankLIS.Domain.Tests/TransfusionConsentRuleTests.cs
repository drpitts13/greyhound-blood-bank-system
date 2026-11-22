using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class TransfusionConsentRuleTests
{
    // TEST-BB-1132: consent confirmation is required only when the facility turns it on; then only an explicit
    // confirmation passes (end-user testing pass 115).
    [Theory]
    [InlineData(false, null, false)]
    [InlineData(false, false, false)]
    [InlineData(true, null, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Evaluate_BlocksOnlyWhenRequiredAndNotConfirmed(bool required, bool? confirmed, bool blocked) =>
        Assert.Equal(blocked, TransfusionConsentRule.Evaluate(required, confirmed).Severity == RuleSeverity.HardStop);
}
