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

    // TEST-BB-558: a failed QC stands only while it is the open lot's latest QC; an expired lot is flagged to close
    // instead of asking for daily QC.
    [Fact]
    public void Worklist_FailedOnlyWhileLatest_AndExpiredLotsFlagged()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        ReagentQcLotFact[] lots =
        [
            new(1, "PASSED-AFTER", ReagentLotState.Opened, now.AddDays(10)),
            new(2, "CLOSED", ReagentLotState.Closed, now.AddDays(10)),
            new(3, "EXPIRED", ReagentLotState.Opened, now.AddHours(-1)),
            new(4, "STILL-FAILED", ReagentLotState.Opened, now.AddDays(10))
        ];
        ReagentQcFact[] qc =
        [
            new(10, 1, now.AddDays(-2), false, "lead"),
            new(11, 1, now, true, "lead"),
            new(20, 2, now.AddDays(-1), false, "lead"),
            new(40, 4, now.AddDays(-1), true, "lead"),
            new(41, 4, now, false, "lead")
        ];

        var items = ReagentQcWorklistRule.Evaluate(lots, qc, now);

        Assert.Equal([41L], items.Where(i => i.Kind == ReagentQcWorklistRule.Failed).Select(i => i.QcId!.Value));
        var expired = Assert.Single(items, i => i.Kind == ReagentQcWorklistRule.Expired);
        Assert.Equal(3, expired.LotId);
        Assert.Contains("close it and open a new lot", expired.Message);
        Assert.DoesNotContain(items, i => i.LotId == 3 && i.Kind == ReagentQcWorklistRule.Overdue);
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
    public void MinimumStockShortfall_IsTheGapBelowTheMinimum()
    {
        Assert.Equal(2, MinimumStockRule.Shortfall(1, 3));
        Assert.Equal(0, MinimumStockRule.Shortfall(4, 3));
    }

    [Fact]
    public void Batch_RequiresAtLeastTwoUnits()
    {
        Assert.Equal(RuleSeverity.HardStop, BatchModificationRule.Evaluate(1).Severity);
        Assert.Equal(RuleSeverity.Pass, BatchModificationRule.Evaluate(2).Severity);
    }
}
