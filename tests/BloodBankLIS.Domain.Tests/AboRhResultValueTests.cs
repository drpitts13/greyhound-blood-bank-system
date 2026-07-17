using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AboRhResultValueTests
{
    [Fact]
    public void Format_RoundTrips_ThroughTryParse()
    {
        var value = AboRhResultValue.Format(AboGroup.AB, RhType.Negative);

        Assert.True(AboRhResultValue.TryParse(value, out var parsed));
        Assert.Equal(AboGroup.AB, parsed.Abo);
        Assert.Equal(RhType.Negative, parsed.Rh);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("O")]
    [InlineData("O|Positive|extra")]
    [InlineData("Bogus|Positive")]
    public void TryParse_InvalidInput_ReturnsFalse(string? value)
    {
        Assert.False(AboRhResultValue.TryParse(value, out _));
    }
}
