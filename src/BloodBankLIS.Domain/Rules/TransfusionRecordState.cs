using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A transfusion record started at the bedside and not yet completed has no stop time and its issue is still
/// Issued. Its FinalDisposition holds only the field's default until completion, so readers must not report it
/// as an outcome. One rule for every reader (chart, timeline, reports, lookback, unit activity, utilization).
/// </summary>
public static class TransfusionRecordState
{
    /// <summary>The wording reports use for a running transfusion's disposition.</summary>
    public const string InProgress = "InProgress";

    public static bool IsRunning(DateTime? stopUtc, IssueStatus issueStatus) =>
        stopUtc is null && issueStatus == IssueStatus.Issued;

    /// <summary>The recorded outcome, or null while the transfusion is still running.</summary>
    public static TransfusionDisposition? Outcome(TransfusionDisposition disposition, DateTime? stopUtc, IssueStatus issueStatus) =>
        IsRunning(stopUtc, issueStatus) ? null : disposition;

    /// <summary>SQL for a report column: InProgress while running, otherwise the disposition name.</summary>
    public static string SqlDisposition(string stopColumn, string issueStatusColumn, string dispositionCase) =>
        $"CASE WHEN {stopColumn} IS NULL AND {issueStatusColumn} = {(int)IssueStatus.Issued} THEN '{InProgress}' ELSE {dispositionCase} END";
}
