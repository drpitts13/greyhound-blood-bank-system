namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for lookback actions that change unit status or notification history.
/// </summary>
public static class LookbackAuthorizationRule
{
    public const string RecallCode = "LK-RECALL-PERM";
    public const string AttemptCode = "LK-ATTEMPT-PERM";

    public static RuleResult EvaluateRecall(bool hasLookbackManage) =>
        hasLookbackManage
            ? RuleResult.Pass(RecallCode)
            : RuleResult.HardStop(
                RecallCode,
                "Recalling units by DIN requires the lookback.manage permission.");

    public static RuleResult EvaluateAttempt(bool hasLookbackManage) =>
        hasLookbackManage
            ? RuleResult.Pass(AttemptCode)
            : RuleResult.HardStop(
                AttemptCode,
                "Recording a lookback notification attempt requires the lookback.manage permission.");
}
