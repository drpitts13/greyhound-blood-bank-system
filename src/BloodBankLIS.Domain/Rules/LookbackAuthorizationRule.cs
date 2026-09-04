namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for lookback actions that change unit status (donor recall).
/// </summary>
public static class LookbackAuthorizationRule
{
    public const string RecallCode = "LK-RECALL-PERM";

    public static RuleResult EvaluateRecall(bool hasLookbackManage) =>
        hasLookbackManage
            ? RuleResult.Pass(RecallCode)
            : RuleResult.HardStop(
                RecallCode,
                "Recalling units by DIN requires the lookback.manage permission.");
}
