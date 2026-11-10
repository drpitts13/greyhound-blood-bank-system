using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>OCD-068 (decided 2026-11-10): which thawed shelf life applies to a thaw.</summary>
public class ThawedShelfLifeForTests
{
    // TEST-BB-857: a closed-system single unit uses the closed shelf life; an open-system thaw or a pool uses its own,
    // the shorter when both apply; one that applies but is not set refuses rather than falling back to the longer time;
    // an optional setting is blank or 1 to 720 hours.
    [Fact]
    public void ShelfLifeFor_PicksTheApplicableSetting()
    {
        Assert.Equal(24, ThawedExpirationRule.ShelfLifeFor("E0707", 24, 4, 6, false, false).Hours);
        Assert.Equal(4, ThawedExpirationRule.ShelfLifeFor("E0707", 24, 4, 6, true, false).Hours);
        Assert.Equal(6, ThawedExpirationRule.ShelfLifeFor("E0707", 24, 4, 6, false, true).Hours);
        Assert.Equal(4, ThawedExpirationRule.ShelfLifeFor("E0707", 24, 4, 6, true, true).Hours);
        Assert.Equal(24, ThawedExpirationRule.ShelfLifeFor("E0707", 24, null, null, false, false).Hours);

        var open = ThawedExpirationRule.ShelfLifeFor("E0707", 24, null, 6, true, false);
        Assert.Null(open.Hours);
        Assert.Equal((RuleSeverity.HardStop, ThawedExpirationRule.NotSetCode), (open.Result.Severity, open.Result.Code));
        Assert.Contains("E0707", open.Result.Message);
        Assert.Null(ThawedExpirationRule.ShelfLifeFor("E0707", 24, 4, null, true, true).Hours);

        Assert.Equal(RuleSeverity.Pass, ThawedExpirationRule.EvaluateOptionalShelfLife(null, "pooled").Severity);
        Assert.Equal(RuleSeverity.Pass, ThawedExpirationRule.EvaluateOptionalShelfLife(720, "pooled").Severity);
        Assert.Equal(RuleSeverity.HardStop, ThawedExpirationRule.EvaluateOptionalShelfLife(0, "pooled").Severity);
        Assert.Equal(RuleSeverity.HardStop, ThawedExpirationRule.EvaluateOptionalShelfLife(721, "open-system").Severity);
    }
}
