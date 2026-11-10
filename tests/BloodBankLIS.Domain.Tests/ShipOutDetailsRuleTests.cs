using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>OCD-072 (decided 2026-11-10): what a shipment to another facility must record.</summary>
public class ShipOutDetailsRuleTests
{
    // TEST-BB-860: all four details given passes; each missing one is named, in order, joined as a sentence; a packing
    // temperature outside -50 to 50 °C is taken as a typing slip; over-long text is refused.
    [Fact]
    public void EvaluateDetails_NamesWhatIsMissing()
    {
        Assert.Equal(RuleSeverity.Pass, ShipOutRule.EvaluateDetails("COOLER-7", 4m, "tech", "J. Ruiz").Severity);
        Assert.Equal(RuleSeverity.Pass, ShipOutRule.EvaluateDetails("BOX-2", -20m, "tech", "J. Ruiz").Severity);

        Assert.Equal("Record the container and who packed it for the shipment (OCD-072).",
            ShipOutRule.EvaluateDetails(" ", 4m, null, "J. Ruiz").Message);
        Assert.Equal("Record the packing temperature for the shipment (OCD-072).",
            ShipOutRule.EvaluateDetails("COOLER-7", null, "tech", "J. Ruiz").Message);

        var hot = ShipOutRule.EvaluateDetails("COOLER-7", 51m, "tech", "J. Ruiz");
        Assert.Equal((RuleSeverity.HardStop, ShipOutRule.DetailsCode), (hot.Severity, hot.Code));
        Assert.Equal(RuleSeverity.HardStop, ShipOutRule.EvaluateDetails("COOLER-7", -51m, "tech", "J. Ruiz").Severity);
        Assert.Equal(RuleSeverity.HardStop, ShipOutRule.EvaluateDetails(new string('x', 101), 4m, "tech", "J. Ruiz").Severity);
    }
}
