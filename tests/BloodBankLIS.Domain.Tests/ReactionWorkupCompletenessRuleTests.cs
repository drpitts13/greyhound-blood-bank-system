using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReactionWorkupCompletenessRuleTests
{
    [Fact]
    public void CompleteNegativeDat_Passes()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, true, DatWorkupResult.Negative, null);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void MissingClericalCheck_IsHardStop()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(false, true, DatWorkupResult.Negative, null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ReactionWorkupCompletenessRule.Code, result.Code);
    }

    [Fact]
    public void MissingVisualInspection_IsHardStop()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, false, DatWorkupResult.Negative, null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void DatNotRecorded_IsHardStop()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, true, DatWorkupResult.NotRecorded, null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void PositiveDatWithoutElution_IsHardStop()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, true, DatWorkupResult.Positive, "  ");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void PositiveDatWithElution_Passes()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, true, DatWorkupResult.Positive, "No alloantibody recovered");
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
