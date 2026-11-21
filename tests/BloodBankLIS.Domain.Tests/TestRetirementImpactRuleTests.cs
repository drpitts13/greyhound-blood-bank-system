using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class TestRetirementImpactRuleTests
{
    // TEST-BB-1402 (end-user testing loop 2 tick 22): each active use of a retired test is named (groupers, reflex rules by
    // trigger or result, products retyping with it), matched ignoring case; nothing in use gives no warning.
    [Fact]
    public void Warnings_NameEachUse()
    {
        var warnings = TestRetirementImpactRule.Warnings("ABSC",
            [new("TS", ["ABORH", "absc"]), new("ABO-ONLY", ["ABORH"])],
            [new("ABSC-POS-ABID", "ABSC", "ABID"), new("OTHER", "DAT", "ELUTION"), new("TO-SCREEN", "DAT", "ABSC")],
            [new("RBC-LR", true), new("FFP", false)]);
        Assert.Equal(3, warnings.Count);
        Assert.Contains("1 test grouper (TS)", warnings[0].Message);
        Assert.Contains("2 reflex rules (ABSC-POS-ABID, TO-SCREEN)", warnings[1].Message);
        Assert.Contains("retype test for 1 product (RBC-LR)", warnings[2].Message);
        Assert.All(warnings, w => Assert.Equal(RuleSeverity.Warning, w.Severity));
        Assert.Empty(TestRetirementImpactRule.Warnings("WEAKD", [new("TS", ["ABSC"])], [], [new("FFP", false)]));
    }
}
