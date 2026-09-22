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

    [Fact]
    public void FormatPanel_WritesReadablePlusInsteadOfUnicodeEscape()
    {
        var json = AboRhResultValue.FormatPanel(new AboRhPanelResult(
            AboGroup.B,
            RhType.Positive,
            new Dictionary<string, string>
            {
                ["Anti-A"] = "0",
                ["Anti-B"] = "4+",
                ["Anti-D"] = "2+"
            }));

        Assert.Contains("4+", json, StringComparison.Ordinal);
        Assert.Contains("2+", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u002B", json, StringComparison.Ordinal);
        Assert.Equal("B+ · Anti-A:0 · Anti-B:4+ · Anti-D:2+", AboRhResultValue.FormatDisplay(json));
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
