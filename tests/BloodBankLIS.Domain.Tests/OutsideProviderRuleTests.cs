using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-391 (OCD-055)
public class OutsideProviderRuleTests
{
    [Fact]
    public void Validate_NeedsANameAndAReason_AndRefusesAListedName()
    {
        Assert.False(OutsideProviderRule.Validate("Okafor, Chidi", "Transferring trauma surgeon", alreadyInDirectory: false).IsHardStopped);
        Assert.Contains(OutsideProviderRule.Validate("", "Transferring trauma surgeon", false).HardStops, h => h.Code == OutsideProviderRule.NameCode);
        Assert.Contains(OutsideProviderRule.Validate("Okafor, Chidi", "1234567", false).HardStops, h => h.Code == OutsideProviderRule.ReasonCode);
        Assert.Contains(OutsideProviderRule.Validate("Okafor, Chidi", "Transferring trauma surgeon", true).HardStops, h => h.Code == OutsideProviderRule.AlreadyListedCode);
    }

    [Fact]
    public void Permission_IsAHardStopWithoutIt_AndSaysTheOverrideStillWorks()
    {
        Assert.Equal(RuleSeverity.Pass, OutsideProviderRule.EvaluatePermission(true).Severity);
        var denied = OutsideProviderRule.EvaluatePermission(false);
        Assert.Equal(RuleSeverity.HardStop, denied.Severity);
        Assert.Contains("still be recorded", denied.Message);
    }

    [Fact]
    public void GeneratedId_AndIsOutside()
    {
        Assert.Equal("OUT-0003", OutsideProviderRule.GeneratedId(3));
        Assert.True(OutsideProviderRule.IsOutside(" outside "));
        Assert.False(OutsideProviderRule.IsOutside("Manual"));
        Assert.False(OutsideProviderRule.IsOutside(null));
    }

    [Fact]
    public void Directory_MatchesAnOutsideProvider_AsOutside_AndDescribesIt()
    {
        var providers = new[] { new AuthorizerDirectoryRule.Entry("OUT-0001", "Okafor, Chidi", Outside: true) };
        var match = AuthorizerDirectoryRule.Match("okafor, chidi", providers, []);
        Assert.Equal(AuthorizerDirectoryMatch.OutsideProvider, match.Match);
        Assert.Equal("OUT-0001", match.Reference);
        Assert.Equal("outside provider OUT-0001", AuthorizerDirectoryRule.Describe(match.Match, match.Reference));
    }
}
