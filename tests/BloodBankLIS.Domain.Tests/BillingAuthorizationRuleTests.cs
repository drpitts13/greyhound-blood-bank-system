using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BillingAuthorizationRuleTests
{
    [Fact]
    public void Review_WithoutPermission_IsHardStop()
    {
        var result = BillingAuthorizationRule.EvaluateReview(hasBillingReview: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(BillingAuthorizationRule.ReviewCode, result.Code);
    }

    [Fact]
    public void Review_WithPermission_Passes()
    {
        var result = BillingAuthorizationRule.EvaluateReview(hasBillingReview: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Cancel_WithoutPermission_IsHardStop()
    {
        var result = BillingAuthorizationRule.EvaluateCancel(hasBillingCancel: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(BillingAuthorizationRule.CancelCode, result.Code);
    }

    [Fact]
    public void Cancel_WithPermission_Passes()
    {
        var result = BillingAuthorizationRule.EvaluateCancel(hasBillingCancel: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Export_WithoutPermission_IsHardStop()
    {
        var result = BillingAuthorizationRule.EvaluateExport(hasBillingExport: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(BillingAuthorizationRule.ExportCode, result.Code);
    }

    [Fact]
    public void Export_WithPermission_Passes()
    {
        var result = BillingAuthorizationRule.EvaluateExport(hasBillingExport: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
