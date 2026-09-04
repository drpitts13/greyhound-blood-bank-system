using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PrintAuthorizationRuleTests
{
    [Fact]
    public void Label_WithoutPrintLabel_IsHardStop()
    {
        var result = PrintAuthorizationRule.EvaluateLabel(hasPrintLabel: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PrintAuthorizationRule.LabelCode, result.Code);
    }

    [Fact]
    public void Label_WithPrintLabel_Passes()
    {
        var result = PrintAuthorizationRule.EvaluateLabel(hasPrintLabel: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Reprint_WithoutPrintReprint_IsHardStop()
    {
        var result = PrintAuthorizationRule.EvaluateReprint(hasPrintReprint: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PrintAuthorizationRule.ReprintCode, result.Code);
    }

    [Fact]
    public void Reprint_WithPrintReprint_Passes()
    {
        var result = PrintAuthorizationRule.EvaluateReprint(hasPrintReprint: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
