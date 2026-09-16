namespace BloodBankLIS.Domain.Rules;

/// <summary>Privilege gate for the singleton crossmatch settings catalog.</summary>
public static class CrossmatchSettingsAuthorizationRule
{
    public const string UpdateCode = "XMSET-UPD-PERM";

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating crossmatch settings requires the admin.config.edit permission.");
}
