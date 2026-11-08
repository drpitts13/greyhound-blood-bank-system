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
}
