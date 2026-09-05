using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AntigenTypingMethodInfoTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Serologic", false)]
    [InlineData("tube", false)]
    [InlineData("Molecular", true)]
    [InlineData("predicted genotype", true)]
    [InlineData("DNA array", true)]
    [InlineData("PCR-SSP", true)]
    public void ClassifiesMethod(string? method, bool expected) =>
        Assert.Equal(expected, AntigenTypingMethodInfo.IndicatesPredictedGenotype(method));
}
