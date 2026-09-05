namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for inventory-location catalog mutations that change
/// where units can be stored or issued from.
/// </summary>
public static class InventoryLocationAuthorizationRule
{
    public const string CreateCode = "INVLOC-CREATE-PERM";
    public const string UpdateCode = "INVLOC-UPD-PERM";
    public const string ActivateCode = "INVLOC-ACT-PERM";
    public const string DeactivateCode = "INVLOC-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an inventory location requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an inventory location requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating an inventory location requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating an inventory location requires the admin.config.activate permission.");
}
