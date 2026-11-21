using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-329: documented transfusion times and volume must be consistent; they are data checks, not clinical limits.
public class TransfusionDocumentationRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Consistent_Passes() =>
        Assert.False(TransfusionDocumentationRule.Evaluate(Now.AddHours(-2), Now.AddHours(-1), 280m, 300m, Now).IsHardStopped);

    [Fact]
    public void NothingEntered_Passes() =>
        Assert.False(TransfusionDocumentationRule.Evaluate(null, null, null, null, Now).IsHardStopped);

    [Fact]
    public void WithinClockAllowance_Passes() =>
        Assert.False(TransfusionDocumentationRule.Evaluate(Now.AddMinutes(4), null, null, null, Now).IsHardStopped);

    [Theory]
    [InlineData(10, null, null, null)]   // start in the future
    [InlineData(-60, 10, null, null)]    // stop in the future
    [InlineData(-60, -90, null, null)]   // stop before start
    [InlineData(-60, -30, 0, null)]      // zero volume
    [InlineData(-60, -30, -5, null)]     // negative volume
    [InlineData(-60, -30, 301, 300)]     // more than the bag held
    public void Inconsistent_IsRefused(int startMinutes, int? stopMinutes, int? volume, int? bag)
    {
        var evaluation = TransfusionDocumentationRule.Evaluate(
            Now.AddMinutes(startMinutes),
            stopMinutes is int s ? Now.AddMinutes(s) : null,
            volume, bag, Now);
        Assert.True(evaluation.IsHardStopped);
    }

    [Fact]
    public void UnknownBagVolume_DoesNotCapTheVolume() =>
        Assert.False(TransfusionDocumentationRule.Evaluate(null, null, 450m, null, Now).IsHardStopped);
}
