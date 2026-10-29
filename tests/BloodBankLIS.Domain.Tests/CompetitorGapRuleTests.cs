using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CompetitorGapRuleTests
{
    [Fact]
    public void Reviewer_MustDifferFromPerformer()
    {
        Assert.Equal(RuleSeverity.HardStop, ReagentQcReviewRule.Evaluate("tech", "tech").Severity);
        Assert.Equal(RuleSeverity.Pass, ReagentQcReviewRule.Evaluate("tech", "lead").Severity);
    }

    [Fact]
    public void Worklist_ShowsOverdueFailedAndUnreviewed()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var items = ReagentQcWorklistRule.Evaluate(
            [new ReagentQcLotFact(1, "L1", ReagentLotState.Opened, now.AddDays(10))],
            [new ReagentQcFact(9, 1, now.AddDays(-1), false, null)],
            now);
        Assert.Contains(items, item => item.Kind == ReagentQcWorklistRule.Overdue);
        Assert.Contains(items, item => item.Kind == ReagentQcWorklistRule.Failed);
        Assert.Contains(items, item => item.Kind == ReagentQcWorklistRule.Unreviewed);
    }

    [Fact]
    public void ResultGate_StaysOpenUnlessPolicyAndWorkAreBothOn()
    {
        Assert.Equal(RuleSeverity.Pass, ReagentQcResultGate.Evaluate(false, true).Severity);
        Assert.Equal(RuleSeverity.HardStop, ReagentQcResultGate.Evaluate(true, true).Severity);
    }

    [Fact]
    public void Disposition_IgnoresDocumentedReturnedAndDiscardedUnits()
    {
        var issued = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var now = issued.AddHours(25);
        Assert.True(FinalDispositionDueRule.IsOverdue(issued, now, 24, false, false, false));
        Assert.False(FinalDispositionDueRule.IsOverdue(issued, now, 24, true, false, false));
        Assert.False(FinalDispositionDueRule.IsOverdue(issued, now, 24, false, true, false));
        Assert.False(FinalDispositionDueRule.IsOverdue(issued, now, 24, false, false, true));
        Assert.False(FinalDispositionDueRule.IsOverdue(issued, issued.AddHours(23), 24, false, false, false));
    }

    [Fact]
    public void ParShortfall_IsTheGapBelowTheMinimum()
    {
        Assert.Equal(2, StockParRule.Shortfall(1, 3));
        Assert.Equal(0, StockParRule.Shortfall(4, 3));
    }

    [Fact]
    public void Batch_RequiresAtLeastTwoUnits()
    {
        Assert.Equal(RuleSeverity.HardStop, BatchModificationRule.Evaluate(1).Severity);
        Assert.Equal(RuleSeverity.Pass, BatchModificationRule.Evaluate(2).Severity);
    }
}
