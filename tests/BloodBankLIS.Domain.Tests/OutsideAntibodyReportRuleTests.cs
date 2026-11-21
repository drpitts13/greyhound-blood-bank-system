using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-602: an outside antibody report names the facility; its date identified is not in the future and needs the
// facility; the source reads "Reported by …, identified …".
public class OutsideAntibodyReportRuleTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);

    [Fact]
    public void Evaluate_And_SourceLabel()
    {
        Assert.Equal(RuleSeverity.Pass, OutsideAntibodyReportRule.Evaluate(null, null, Today).Severity);
        Assert.Equal(RuleSeverity.Pass, OutsideAntibodyReportRule.Evaluate("St. Mary's", null, Today).Severity);
        Assert.Equal(RuleSeverity.Pass, OutsideAntibodyReportRule.Evaluate("St. Mary's", Today, Today).Severity);
        Assert.Contains("future", OutsideAntibodyReportRule.Evaluate("St. Mary's", Today.AddDays(1), Today).Message);
        Assert.Contains("Name the facility", OutsideAntibodyReportRule.Evaluate(" ", Today, Today).Message);
        Assert.Contains("at most 200", OutsideAntibodyReportRule.Evaluate(new string('x', 201), null, Today).Message);

        Assert.Equal("Reported by St. Mary's, identified 2019-04-02", OutsideAntibodyReportRule.SourceLabel(" St. Mary's ", new DateOnly(2019, 4, 2)));
        Assert.Equal("Reported by St. Mary's", OutsideAntibodyReportRule.SourceLabel("St. Mary's", null));
        Assert.Null(OutsideAntibodyReportRule.SourceLabel(" ", new DateOnly(2019, 4, 2)));
    }

    // TEST-BB-863 (OCD-073): every outside report says how it is documented and gives the detail; a local antibody needs
    // none. Only an active, unconfirmed outside report can be confirmed, with the testing here that confirmed it.
    [Fact]
    public void Documentation_And_Confirmation()
    {
        Assert.Equal(RuleSeverity.Pass, OutsideAntibodyReportRule.Evaluate(null, null, Today, null, null).Severity);
        Assert.Contains("how the report is documented", OutsideAntibodyReportRule.Evaluate("St. Mary's", null, Today, null, "card").Message);
        Assert.Contains("who called", OutsideAntibodyReportRule.Evaluate("St. Mary's", null, Today, OutsideReportKind.Phone, null).Message);
        Assert.Contains("where it is kept", OutsideAntibodyReportRule.Evaluate("St. Mary's", null, Today, OutsideReportKind.Copy, " ").Message);
        Assert.Equal(RuleSeverity.Pass, OutsideAntibodyReportRule.Evaluate("St. Mary's", null, Today, OutsideReportKind.Copy, "Card, in the chart").Severity);
        Assert.Equal(RuleSeverity.HardStop, OutsideAntibodyReportRule.Evaluate("St. Mary's", null, Today, OutsideReportKind.Copy, new string('x', 501)).Severity);

        Assert.True(OutsideAntibodyReportRule.NeedsConfirmation("St. Mary's", null, true));
        Assert.False(OutsideAntibodyReportRule.NeedsConfirmation("St. Mary's", DateTime.UtcNow, true));
        Assert.False(OutsideAntibodyReportRule.NeedsConfirmation("St. Mary's", null, false));
        Assert.False(OutsideAntibodyReportRule.NeedsConfirmation(null, null, true));

        Assert.Equal(RuleSeverity.Pass, OutsideAntibodyReportRule.EvaluateConfirm("St. Mary's", null, true, "Panel").Severity);
        Assert.Contains("Only an antibody reported", OutsideAntibodyReportRule.EvaluateConfirm(null, null, true, "Panel").Message);
        Assert.Contains("already confirmed", OutsideAntibodyReportRule.EvaluateConfirm("St. Mary's", DateTime.UtcNow, true, "Panel").Message);
        Assert.Contains("inactive", OutsideAntibodyReportRule.EvaluateConfirm("St. Mary's", null, false, "Panel").Message);
        Assert.Contains("Name the testing", OutsideAntibodyReportRule.EvaluateConfirm("St. Mary's", null, true, " ").Message);
    }
}
