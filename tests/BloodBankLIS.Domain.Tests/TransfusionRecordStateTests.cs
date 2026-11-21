using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-348
public class TransfusionRecordStateTests
{
    private static readonly DateTime Stop = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoStop_OnAnIssuedIssue_IsRunning_WithNoOutcome()
    {
        Assert.True(TransfusionRecordState.IsRunning(null, IssueStatus.Issued));
        Assert.Null(TransfusionRecordState.Outcome(TransfusionDisposition.Completed, null, IssueStatus.Issued));
    }

    [Theory]
    [InlineData(true, IssueStatus.Issued)]
    [InlineData(false, IssueStatus.Transfused)]
    [InlineData(true, IssueStatus.Transfused)]
    public void AStopOrAFinishedIssue_IsNotRunning_AndKeepsItsOutcome(bool hasStop, IssueStatus status)
    {
        DateTime? stop = hasStop ? Stop : null;
        Assert.False(TransfusionRecordState.IsRunning(stop, status));
        Assert.Equal(TransfusionDisposition.Stopped, TransfusionRecordState.Outcome(TransfusionDisposition.Stopped, stop, status));
    }

    [Fact]
    public void SqlDisposition_ReportsInProgressForTheIssuedStatusValue() =>
        Assert.Equal("CASE WHEN t.StopUtc IS NULL AND i.Status = 0 THEN 'InProgress' ELSE X END",
            TransfusionRecordState.SqlDisposition("t.StopUtc", "i.Status", "X"));
}
