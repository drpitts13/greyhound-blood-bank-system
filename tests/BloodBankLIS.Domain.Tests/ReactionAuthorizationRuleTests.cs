using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReactionAuthorizationRuleTests
{
    [Fact]
    public void Investigate_WithoutPermission_IsHardStop()
    {
        var result = ReactionAuthorizationRule.EvaluateInvestigate(hasReactionInvestigate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ReactionAuthorizationRule.InvestigateCode, result.Code);
    }

    [Fact]
    public void Investigate_WithPermission_Passes()
    {
        var result = ReactionAuthorizationRule.EvaluateInvestigate(hasReactionInvestigate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
