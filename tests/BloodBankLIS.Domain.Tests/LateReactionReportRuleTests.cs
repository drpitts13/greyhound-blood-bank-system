using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-402
public class LateReactionReportRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = Now.AddHours(-8);

    private static RuleResult Report(bool permission = true, bool found = true, long? existing = null, DateTime? onset = null, string? text = "Fever and chills") =>
        LateReactionReportRule.Evaluate(permission, found, existing, Start, onset, text, Now);

    [Fact]
    public void AValidReport_Passes_WithOrWithoutAnOnset()
    {
        Assert.Equal(RuleSeverity.Pass, Report().Severity);
        Assert.Equal(RuleSeverity.Pass, Report(onset: Start).Severity);
        Assert.Equal(RuleSeverity.Pass, Report(onset: Now).Severity);
    }

    [Fact]
    public void Refusals_SayWhy()
    {
        Assert.Contains("requires permission", Report(permission: false).Message);
        Assert.Contains("Document the transfusion first", Report(found: false).Message);
        Assert.Contains("investigation 7 is already open", Report(existing: 7).Message);
        Assert.Contains("Describe the reaction", Report(text: "  ").Message);
        Assert.Contains("before the transfusion started", Report(onset: Start.AddMinutes(-1)).Message);
        Assert.Contains("in the future", Report(onset: Now.AddHours(1)).Message);
    }
}
