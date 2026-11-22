using BloodBankLIS.Domain.Rules;
using static BloodBankLIS.Domain.Rules.IsbtReadinessRule;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-318: ISBT licensed-code readiness (OCD-004). Read-only classification; no code is invented.
public class IsbtReadinessRuleTests
{
    private static readonly Dictionary<string, bool> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["E0206"] = false,
        ["E0701"] = true
    };

    [Theory]
    [InlineData("E0206", CodeState.Licensed)]
    [InlineData("e0206", CodeState.Licensed)]
    [InlineData("E0701", CodeState.Placeholder)]
    [InlineData("E9999", CodeState.Unknown)]
    [InlineData(" ", CodeState.None)]
    [InlineData(null, CodeState.None)]
    public void Classify(string? code, CodeState expected) =>
        Assert.Equal(expected, IsbtReadinessRule.Classify(code, Catalog));

    [Theory]
    [InlineData("E0206", "E0206V00", "E0206")]
    [InlineData(null, "E0206V00", "E0206")]
    [InlineData(null, "E02", "E02")]
    [InlineData(null, null, null)]
    public void ProductDescriptionCode_PrefersTheStoredCode(string? stored, string? data, string? expected) =>
        Assert.Equal(expected, ProductDescriptionCode(stored, data));

    [Fact]
    public void Ready_NeedsALicensedVersion_AndNothingInUseOnPlaceholderOrUnknown()
    {
        Assert.True(IsReady(1, 0, 0));
        Assert.False(IsReady(0, 0, 0));
        Assert.False(IsReady(1, 1, 0));
        Assert.False(IsReady(1, 0, 1));
    }

    [Fact]
    public void Summary_SaysWhatIsMissing()
    {
        Assert.StartsWith("Ready:", Summary(1, 0, 0));
        var notReady = Summary(0, 2, 1);
        Assert.Contains("no licensed ICCBBA extract has been imported", notReady);
        Assert.Contains("2 codes in use rely on placeholder rows", notReady);
        Assert.Contains("1 code is not in the catalog at all", notReady);
        Assert.Contains("OCD-004", notReady);
    }
}
