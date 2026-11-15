using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class RequestedVolumeRuleTests
{
    // TEST-BB-1136: a requested volume is none (a whole unit) or 1 to 1000 mL, and a line names it after the product
    // (end-user testing pass 118).
    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(15, true)]
    [InlineData(1000, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1001, false)]
    public void Validate(int? ml, bool ok) => Assert.Equal(ok, RequestedVolumeRule.Validate(ml) is null);

    [Fact]
    public void Label_NamesTheVolume() =>
        Assert.Equal(("Red Blood Cells (15 mL)", "Red Blood Cells"),
            (RequestedVolumeRule.Label("Red Blood Cells", 15), RequestedVolumeRule.Label("Red Blood Cells", null)));
}
