namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for modification-rule catalog mutations that change
/// which product paths and expirations apply when a unit is modified.
/// </summary>
public static class ModificationRuleAuthorizationRule
{
    public const string CreateCode = "MODRULE-CREATE-PERM";
    public const string UpdateCode = "MODRULE-UPD-PERM";
    public const string ActivateCode = "MODRULE-ACT-PERM";
    public const string DeactivateCode = "MODRULE-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminModificationRulesManage) =>
        hasAdminModificationRulesManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a modification rule requires the admin.modification-rules.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminModificationRulesManage) =>
        hasAdminModificationRulesManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a modification rule requires the admin.modification-rules.manage permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a modification rule requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a modification rule requires the admin.config.activate permission.");
}
