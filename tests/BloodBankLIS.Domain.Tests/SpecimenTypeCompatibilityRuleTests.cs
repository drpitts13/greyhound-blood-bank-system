using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class SpecimenTypeCompatibilityRuleTests
{
    [Fact]
    public void RequiredSpecimenType_Mismatch_HardStops()
    {
        var eval = SpecimenTypeCompatibilityRule.Evaluate("SERUM", "ABORH", "EDTA", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "SPECIMEN.TYPE.REQUIRED");
    }

    [Fact]
    public void ExcludedTest_HardStops()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "XM" };
        var eval = SpecimenTypeCompatibilityRule.Evaluate("SERUM", "XM", null, excluded);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "SPECIMEN.TYPE.EXCLUDED");
    }

    [Fact]
    public void CompatibleSpecimen_Passes()
    {
        var eval = SpecimenTypeCompatibilityRule.Evaluate("EDTA", "ABORH", "EDTA", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.False(eval.IsHardStopped);
    }
}
