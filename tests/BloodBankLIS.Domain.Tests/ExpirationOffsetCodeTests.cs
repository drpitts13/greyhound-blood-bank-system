using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class ExpirationOffsetCodeTests
{
    [Theory]
    [InlineData("24H", 24, ExpirationOffsetUnit.Hours)]
    [InlineData("24h", 24, ExpirationOffsetUnit.Hours)]
    [InlineData("5D", 5, ExpirationOffsetUnit.Days)]
    [InlineData("5d", 5, ExpirationOffsetUnit.Days)]
    [InlineData(" 12 H ", 12, ExpirationOffsetUnit.Hours)]
    [InlineData("1D", 1, ExpirationOffsetUnit.Days)]
    public void TryParse_ValidCodes_Succeeds(string code, int expectedAmount, ExpirationOffsetUnit expectedUnit)
    {
        var ok = ExpirationOffsetCode.TryParse(code, out var result);

        Assert.True(ok);
        Assert.Equal(expectedAmount, result.Amount);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("24")]
    [InlineData("H")]
    [InlineData("24X")]
    [InlineData("0H")]
    [InlineData("-5D")]
    [InlineData("5.5D")]
    public void TryParse_InvalidCodes_Fails(string? code)
    {
        var ok = ExpirationOffsetCode.TryParse(code, out var result);

        Assert.False(ok);
        Assert.Equal(default, result);
    }

    [Fact]
    public void ToTimeSpan_Hours_ConvertsCorrectly()
    {
        var code = new ExpirationOffsetCode(24, ExpirationOffsetUnit.Hours);

        Assert.Equal(TimeSpan.FromHours(24), code.ToTimeSpan());
    }

    [Fact]
    public void ToTimeSpan_Days_ConvertsCorrectly()
    {
        var code = new ExpirationOffsetCode(5, ExpirationOffsetUnit.Days);

        Assert.Equal(TimeSpan.FromDays(5), code.ToTimeSpan());
    }

    [Theory]
    [InlineData(24, ExpirationOffsetUnit.Hours, "24H")]
    [InlineData(5, ExpirationOffsetUnit.Days, "5D")]
    public void ToString_FormatsCanonically(int amount, ExpirationOffsetUnit unit, string expected)
    {
        var code = new ExpirationOffsetCode(amount, unit);

        Assert.Equal(expected, code.ToString());
    }
}
