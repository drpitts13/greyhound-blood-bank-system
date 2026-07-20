using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

public class CrossmatchResultTypeTests
{
    [Theory]
    [InlineData(ResultValueType.Crossmatch, true)]
    [InlineData(ResultValueType.ComplexCrossmatch, true)]
    [InlineData(ResultValueType.Subtest, true)]
    [InlineData(ResultValueType.AboRh, true)]
    [InlineData(ResultValueType.Coded, false)]
    [InlineData(ResultValueType.BloodAttribute, false)]
    public void UsesPanelSubtests_IncludesCrossmatchTypes(ResultValueType type, bool expected) =>
        Assert.Equal(expected, TestDefinitionValidator.UsesPanelSubtests(type));

    [Theory]
    [InlineData(ResultValueType.Crossmatch, true)]
    [InlineData(ResultValueType.ComplexCrossmatch, true)]
    [InlineData(ResultValueType.Subtest, false)]
    [InlineData(ResultValueType.Coded, false)]
    public void IsCrossmatchResultType_OnlyXmTypes(ResultValueType type, bool expected) =>
        Assert.Equal(expected, TestDefinitionValidator.IsCrossmatchResultType(type));
}
