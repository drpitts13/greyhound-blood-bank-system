using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-351
public class SpecimenTransitRuleTests
{
    private static readonly DateTime Drawn = new(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(42, "42 min")]
    [InlineData(65, "1 h 05 min")]
    [InlineData(600, "10 h 00 min")]
    public void Duration_IsPlainWords(int minutes, string expected) =>
        Assert.Equal(expected, SpecimenTransitRule.Duration(minutes));

    [Fact]
    public void Describe_ReceivedOrWaiting()
    {
        Assert.Equal("received 42 min after draw", SpecimenTransitRule.Describe(Drawn, Drawn.AddMinutes(42), Drawn.AddHours(3)));
        Assert.Equal("waiting 1 h 05 min since draw", SpecimenTransitRule.Describe(Drawn, null, Drawn.AddMinutes(65)));
    }

    [Fact]
    public void ReceiptBeforeDraw_IsNotAPositiveTransit() =>
        Assert.Null(SpecimenTransitRule.DrawToReceiptMinutes(Drawn, Drawn.AddMinutes(-5)));
}
