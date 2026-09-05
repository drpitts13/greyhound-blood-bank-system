using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ResultProvenanceRuleTests
{
    [Theory]
    [InlineData(ResultSource.Manual, false, ResultSource.Manual)]
    [InlineData(ResultSource.Manual, true, ResultSource.Calculated)]
    [InlineData(ResultSource.Calculated, true, ResultSource.Calculated)]
    [InlineData(ResultSource.Instrument, true, ResultSource.Instrument)]
    [InlineData(ResultSource.Interface, true, ResultSource.Interface)]
    [InlineData(ResultSource.Instrument, false, ResultSource.Instrument)]
    public void Resolve_TagsCalculated_UnlessInstrumentOrInterface(
        ResultSource requested, bool catalogLogicApplied, ResultSource expected)
    {
        Assert.Equal(expected, ResultProvenanceRule.Resolve(requested, catalogLogicApplied));
    }
}
