using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ResultAuthorizationRuleTests
{
    [Fact]
    public void Enter_WithoutPermission_IsHardStop()
    {
        var result = ResultAuthorizationRule.EvaluateEnter(hasResultEnter: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ResultAuthorizationRule.EnterCode, result.Code);
    }

    [Fact]
    public void Enter_WithPermission_Passes()
    {
        var result = ResultAuthorizationRule.EvaluateEnter(hasResultEnter: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Correct_WithoutPermission_IsHardStop()
    {
        var result = ResultAuthorizationRule.EvaluateCorrect(hasResultCorrect: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ResultAuthorizationRule.CorrectCode, result.Code);
    }

    [Fact]
    public void Correct_WithPermission_Passes()
    {
        var result = ResultAuthorizationRule.EvaluateCorrect(hasResultCorrect: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Verify_WithoutPermission_IsHardStop()
    {
        var result = ResultAuthorizationRule.EvaluateVerify(hasResultVerify: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ResultAuthorizationRule.VerifyCode, result.Code);
    }

    [Fact]
    public void Verify_WithPermission_Passes()
    {
        var result = ResultAuthorizationRule.EvaluateVerify(hasResultVerify: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Invalidate_WithoutPermission_IsHardStop()
    {
        var result = ResultAuthorizationRule.EvaluateInvalidate(hasResultInvalidate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ResultAuthorizationRule.InvalidateCode, result.Code);
    }

    [Fact]
    public void Invalidate_WithPermission_Passes()
    {
        var result = ResultAuthorizationRule.EvaluateInvalidate(hasResultInvalidate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
