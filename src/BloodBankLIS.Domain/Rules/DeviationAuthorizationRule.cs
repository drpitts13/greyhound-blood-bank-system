namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for creating or closing a quality-system deviation.
/// </summary>
public static class DeviationAuthorizationRule
{
    public const string ManageCode = "DEV-PERM";

    public static RuleResult EvaluateManage(bool hasDeviationManage) =>
        hasDeviationManage
            ? RuleResult.Pass(ManageCode)
            : RuleResult.HardStop(
                ManageCode,
                "Creating or updating a deviation requires the deviation.manage permission.");
}
