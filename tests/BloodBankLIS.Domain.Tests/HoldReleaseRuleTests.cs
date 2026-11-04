using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-407
public class HoldReleaseRuleTests
{
    [Fact]
    public void Release_NeedsAReason_AndAnInDateUnit()
    {
        Assert.Equal(RuleSeverity.Pass, HoldReleaseRule.Evaluate("Paperwork received", expired: false).Severity);
        Assert.Contains("how the hold was resolved", HoldReleaseRule.Evaluate("  ", expired: false).Message);
        Assert.Contains("expired", HoldReleaseRule.Evaluate("Paperwork received", expired: true).Message);
    }
}
