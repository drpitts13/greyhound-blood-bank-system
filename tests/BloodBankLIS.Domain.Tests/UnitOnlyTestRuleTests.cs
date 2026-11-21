using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class UnitOnlyTestRuleTests
{
    // TEST-BB-1266: unit retypes and unit antigen typing are unit-only tests, not offered on a patient order; patient
    // tests are not (end-user testing pass 242).
    [Theory]
    [InlineData("ABORH-RETYPE", true)]
    [InlineData("ABORH-RETYPE-POS", true)]
    [InlineData("abo rh", false)]
    [InlineData("ABORH-RETYPE-NEG", true)]
    [InlineData(" uagtype ", true)]
    [InlineData("ABORH", false)]
    [InlineData("ABSC", false)]
    [InlineData("AGTYPE", false)]
    [InlineData(null, false)]
    public void IsUnitOnly_NamesUnitTests(string? code, bool expected) =>
        Assert.Equal(expected, UnitOnlyTestRule.IsUnitOnly(code));
}
