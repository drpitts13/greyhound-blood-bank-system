using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank cooler / remote-issue custody: an issued unit remains in transit
/// until the receiving location acknowledges ward receipt or the unit is returned.
/// </summary>
public static class InTransitPendingRule
{
    public const string Code = "ISS-IN-TRANSIT";

    public static bool IsPending(IssueStatus issueStatus, DateTime? wardReceivedUtc) =>
        issueStatus == IssueStatus.Issued && wardReceivedUtc is null;

    public static RuleResult EvaluateOverdue(DateTime? dueUtc, DateTime now)
    {
        if (dueUtc is null || now <= dueUtc.Value)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.Warning(Code, "Issued unit has not been received at the ward and is past the in-transit due time.");
    }
}
