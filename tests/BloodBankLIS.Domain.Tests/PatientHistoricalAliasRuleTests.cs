using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PatientHistoricalAliasRuleTests
{
    [Fact]
    public void FormatDisplayName_IncludesMiddleWhenPresent()
    {
        Assert.Equal("Smith, Jane Q", PatientHistoricalAliasRule.FormatDisplayName("Smith", "Jane", "Q"));
        Assert.Equal("Smith, Jane", PatientHistoricalAliasRule.FormatDisplayName("Smith", "Jane", "  "));
    }

    [Fact]
    public void NameChanged_IgnoresCaseAndWhitespace()
    {
        Assert.False(PatientHistoricalAliasRule.NameChanged("Smith", "Jane", null, " smith ", "JANE", null));
        Assert.True(PatientHistoricalAliasRule.NameChanged("Smith", "Jane", null, "Smyth", "Jane", null));
        Assert.True(PatientHistoricalAliasRule.NameChanged("Smith", "Jane", "Q", "Smith", "Jane", null));
    }

    [Fact]
    public void NameChangeReason_RequiredWhenNameChanges()
    {
        var missing = PatientHistoricalAliasRule.EvaluateNameChangeReason(true, "  ");
        Assert.Equal(RuleSeverity.HardStop, missing.Severity);
        Assert.Equal(PatientHistoricalAliasRule.ReasonCode, missing.Code);

        Assert.Equal(
            RuleSeverity.Pass,
            PatientHistoricalAliasRule.EvaluateNameChangeReason(true, "Legal name correction").Severity);
        Assert.Equal(
            RuleSeverity.Pass,
            PatientHistoricalAliasRule.EvaluateNameChangeReason(false, null).Severity);
    }

    [Fact]
    public void TruncateAliasValue_CapsAtMaxLength()
    {
        var longName = new string('A', PatientHistoricalAliasRule.ValueMaxLength + 20);
        Assert.Equal(
            PatientHistoricalAliasRule.ValueMaxLength,
            PatientHistoricalAliasRule.TruncateAliasValue(longName).Length);
    }
}
