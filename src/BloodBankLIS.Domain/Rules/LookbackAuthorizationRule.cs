namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for lookback search, recall, and notification history.
/// Search reveals who received a donation; recall and attempt change inventory
/// or physician-notification records.
/// </summary>
public static class LookbackAuthorizationRule
{
    public const string RecallCode = "LK-RECALL-PERM";
    public const string AttemptCode = "LK-ATTEMPT-PERM";
    public const string FindCode = "LK-FIND-PERM";
    public const string TraceCode = "LK-TRACE-PERM";

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

    public static RuleResult EvaluateFind(bool hasLookbackManage) =>
        hasLookbackManage
            ? RuleResult.Pass(FindCode)
            : RuleResult.HardStop(
                FindCode,
                "Searching lookback by DIN requires the lookback.manage permission.");

    public static RuleResult EvaluateTrace(bool hasLookbackManage) =>
        hasLookbackManage
            ? RuleResult.Pass(TraceCode)
            : RuleResult.HardStop(
                TraceCode,
                "Recipient traceback requires the lookback.manage permission.");
}
