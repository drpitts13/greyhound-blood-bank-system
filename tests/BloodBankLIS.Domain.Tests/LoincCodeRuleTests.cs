using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-523: a LOINC code needs digits, a hyphen, and its Mod-10 check digit; a test definition with a malformed
// code is refused, and one without a code is not affected.
public class LoincCodeRuleTests
{
    [Theory]
    [InlineData("882-1", true)]
    [InlineData("883-9", true)]
    [InlineData(" 882-1 ", true)]
    [InlineData("882-2", false)]
    [InlineData("8821", false)]
    [InlineData("882-", false)]
    [InlineData("-1", false)]
    [InlineData("12345678-0", false)]
    [InlineData("A82-1", false)]
    [InlineData(null, false)]
    public void Code_NeedsDigitsHyphenAndCheckDigit(string? code, bool valid) =>
        Assert.Equal(valid, LoincCodeRule.IsValid(code));

    [Fact]
    public void Definition_WithAMalformedCode_IsRefused()
    {
        var definition = new TestDefinition { Code = "DAT", Name = "DAT", ResultValueType = ResultValueType.FreeText, LoincCode = "1007-5" };
        Assert.Contains(TestDefinitionValidator.Validate(definition, false).HardStops, r => r.Code == LoincCodeRule.Code);

        definition.LoincCode = null;
        Assert.DoesNotContain(TestDefinitionValidator.Validate(definition, false).HardStops, r => r.Code == LoincCodeRule.Code);
    }
}
