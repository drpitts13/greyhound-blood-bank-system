namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for action-catalog mutations that change which
/// modification actions can be attached to a product order.
/// </summary>
public static class ActionDefinitionAuthorizationRule
{
    public const string CreateCode = "ACTION-CREATE-PERM";
    public const string UpdateCode = "ACTION-UPD-PERM";
    public const string ActivateCode = "ACTION-ACT-PERM";
    public const string DeactivateCode = "ACTION-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an action requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an action requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating an action requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating an action requires the admin.config.activate permission.");
}
