using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// AABB / 21 CFR 606.151(b) follow-up: emergency or MTP issues released
/// without a compatible crossmatch remain pending until retrospective XM.
/// SoftBank and SafeTrace keep this queue until the work is done.
/// </summary>
public static class RetrospectiveCrossmatchPendingRule
{
    public const string Code = "ISS-RETRO-XM-PENDING";

    public static bool IsPending(
        bool testsIncompleteAtIssue,
        CrossmatchClinicalStatus crossmatchStatus,
        DateTime? completedUtc,
        IssueStatus issueStatus) =>
        testsIncompleteAtIssue
        && crossmatchStatus != CrossmatchClinicalStatus.Compatible
        && completedUtc is null
        && issueStatus != IssueStatus.Returned;

    public static RuleResult EvaluateOverdue(DateTime? dueUtc, DateTime now)
    {
        if (dueUtc is null || now <= dueUtc.Value)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.Warning(Code, "Retrospective crossmatch is overdue.");
    }
}
