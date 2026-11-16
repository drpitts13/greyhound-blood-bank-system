using BloodBankLIS.Domain.Isbt128.Parsing;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests.Isbt128;

public class ExpirationPlausibilityRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    // TEST-BB-1183: a scanned expiration a century off is refused with what to check (the century digit, OCD-092);
    // a year within two before or eleven after today (frozen red cells) is accepted (end-user testing pass 161).
    [Theory]
    [InlineData(1926, RuleSeverity.HardStop)]
    [InlineData(2023, RuleSeverity.HardStop)]
    [InlineData(2024, RuleSeverity.Pass)]
    [InlineData(2026, RuleSeverity.Pass)]
    [InlineData(2037, RuleSeverity.Pass)]
    [InlineData(2038, RuleSeverity.HardStop)]
    public void Evaluate_RefusesAnImplausibleYear(int year, RuleSeverity expected)
    {
        var result = ExpirationPlausibilityRule.Evaluate(year, Now);
        Assert.Equal(expected, result.Severity);
        if (expected == RuleSeverity.HardStop)
        {
            Assert.Contains($"year {year}", result.Message);
            Assert.Contains("OCD-092", result.Message);
        }
    }

    // The label code that started it: ICCBBA labels in use read &>0263312359 as 2026 day 331; under the century mapping
    // awaiting confirmation it resolves to 1926, which the rule refuses.
    [Fact]
    public void LabelExpiration_ResolvingToTheLastCentury_IsRefused()
    {
        var parsed = ExpirationParser.Parse("&>0263312359");
        Assert.True(parsed.Success);
        Assert.Equal(RuleSeverity.HardStop, ExpirationPlausibilityRule.Evaluate(parsed.Value!.Year, Now).Severity);
    }
}
