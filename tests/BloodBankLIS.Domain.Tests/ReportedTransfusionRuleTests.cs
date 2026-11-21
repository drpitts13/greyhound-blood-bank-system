using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReportedTransfusionRuleTests
{
    // TEST-BB-947 (Cycle 703): a unit never reported transfused passes; one reported is a hard stop naming the unit and OCD-083.
    [Fact]
    public void Evaluate_StopsAReportedUnit()
    {
        Assert.Equal(RuleSeverity.Pass, ReportedTransfusionRule.Evaluate(null, "U-1").Severity);
        var stop = ReportedTransfusionRule.Evaluate(new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc), "U-1");
        Assert.Equal((RuleSeverity.HardStop, ReportedTransfusionRule.Code), (stop.Severity, stop.Code));
        Assert.Contains("U-1 was reported transfused by the EHR (OCD-083)", stop.Message);
    }
}
