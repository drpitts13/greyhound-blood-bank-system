using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-409 (OCD-057)
public class ReactionConclusionRuleTests
{
    private static readonly ReactionCatalogEntry[] Lists =
    [
        new() { Kind = ReactionCatalogKind.ReactionType, Code = "FNHTR", Text = "Febrile non-hemolytic", IsActive = true },
        new() { Kind = ReactionCatalogKind.ReactionType, Code = "OLD", Text = "Retired", IsActive = false },
        new() { Kind = ReactionCatalogKind.Treatment, Code = "WASHED", Text = "Washed products", IsActive = true },
        new() { Kind = ReactionCatalogKind.Treatment, Code = "PREMED", Text = "Premedicate", IsActive = true }
    ];

    [Fact]
    public void Closing_NeedsAReactionAndATreatment_NoneCountsForEach()
    {
        Assert.Contains("reaction concluded", ReactionConclusionRule.Evaluate(null, ["NONE"], Lists, true).Message);
        Assert.Contains("treatment for future transfusions", ReactionConclusionRule.Evaluate("NONE", [], Lists, true).Message);
        Assert.Equal(RuleSeverity.Pass, ReactionConclusionRule.Evaluate("none", ["none"], [], true).Severity);
        Assert.Equal(RuleSeverity.Pass, ReactionConclusionRule.Evaluate("fnhtr", ["WASHED", "PREMED"], Lists, true).Severity);
    }

    [Fact]
    public void CodesMustBeOnTheActiveList_OfTheRightKind_AndNoTreatmentStandsAlone()
    {
        Assert.Contains("not on the active reaction list", ReactionConclusionRule.Evaluate("OLD", ["NONE"], Lists, true).Message);
        Assert.Contains("not on the active reaction list", ReactionConclusionRule.Evaluate("WASHED", ["NONE"], Lists, true).Message);
        Assert.Contains("not on the active treatment list", ReactionConclusionRule.Evaluate("NONE", ["FNHTR"], Lists, true).Message);
        Assert.Contains("cannot be chosen together", ReactionConclusionRule.Evaluate("NONE", ["NONE", "WASHED"], Lists, true).Message);
        // Saving before close may leave either blank.
        Assert.Equal(RuleSeverity.Pass, ReactionConclusionRule.Evaluate("FNHTR", null, Lists, false).Severity);
    }

    [Fact]
    public void TheNoneEntries_CannotBeDeactivatedOrRecoded()
    {
        Assert.Equal(RuleSeverity.HardStop, ReactionConclusionRule.EvaluateCatalogChange(true, null, false).Severity);
        Assert.Equal(RuleSeverity.HardStop, ReactionConclusionRule.EvaluateCatalogChange(true, "NIL", null).Severity);
        Assert.Equal(RuleSeverity.Pass, ReactionConclusionRule.EvaluateCatalogChange(true, "none", null).Severity);
        Assert.Equal(RuleSeverity.Pass, ReactionConclusionRule.EvaluateCatalogChange(false, null, false).Severity);
    }
}
