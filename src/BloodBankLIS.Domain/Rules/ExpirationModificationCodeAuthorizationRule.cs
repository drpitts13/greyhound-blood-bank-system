namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for expiration-modification-code mutations that change
/// how long a modified unit remains usable.
/// </summary>
public static class ExpirationModificationCodeAuthorizationRule
{
    public const string CreateCode = "EXPCODE-CREATE-PERM";
    public const string UpdateCode = "EXPCODE-UPD-PERM";
    public const string ActivateCode = "EXPCODE-ACT-PERM";
    public const string DeactivateCode = "EXPCODE-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminModificationRulesManage) =>
        hasAdminModificationRulesManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an expiration modification code requires the admin.modification-rules.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminModificationRulesManage) =>
        hasAdminModificationRulesManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an expiration modification code requires the admin.modification-rules.manage permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating an expiration modification code requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating an expiration modification code requires the admin.config.activate permission.");
}
