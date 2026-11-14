using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReactionWorkupCompletenessRuleTests
{
    // End-user testing pass 59: every step still due is listed, in gate order; none when the gate passes.
    [Fact]
    public void Outstanding_ListsEveryStepStillDue()
    {
        var nothingYet = new ReactionWorkupCompletenessRule.WorkupState(false, false, 0, 0, false);
        var steps = ReactionWorkupCompletenessRule.Outstanding(true, false, nothingYet, null);
        Assert.Equal(3, steps.Count);
        Assert.StartsWith("Record the visual inspection", steps[0]);
        Assert.StartsWith("Choose the pre-transfusion specimen", steps[1]);
        Assert.StartsWith("Accession or choose the post-transfusion specimen", steps[2]);

        var datPositive = new ReactionWorkupCompletenessRule.WorkupState(true, true, 1, 1, true);
        Assert.Equal(["Result and verify the workup tests: 2 still to verify.", "The post-transfusion DAT is positive: record the elution result or why elution was not performed."],
            ReactionWorkupCompletenessRule.Outstanding(true, true, datPositive, null));

        var done = new ReactionWorkupCompletenessRule.WorkupState(true, true, 0, 0, false);
        Assert.Empty(ReactionWorkupCompletenessRule.Outstanding(true, true, done, null));
        Assert.Equal(RuleSeverity.Pass, ReactionWorkupCompletenessRule.Evaluate(true, true, done, null).Severity);
    }

    [Fact]
    public void CompleteNegativeDat_Passes()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, true, DatWorkupResult.Negative, null);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void MissingClericalCheck_IsHardStop()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(false, true, DatWorkupResult.Negative, null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ReactionWorkupCompletenessRule.Code, result.Code);
    }

    [Fact]
    public void MissingVisualInspection_IsHardStop()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, false, DatWorkupResult.Negative, null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void DatNotRecorded_IsHardStop()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, true, DatWorkupResult.NotRecorded, null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void PositiveDatWithoutElution_IsHardStop()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, true, DatWorkupResult.Positive, "  ");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void PositiveDatWithElution_Passes()
    {
        var result = ReactionWorkupCompletenessRule.Evaluate(true, true, DatWorkupResult.Positive, "No alloantibody recovered");
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
