namespace BloodBankLIS.Domain.Rules;

/// <summary>Privilege gates for the blood supplier catalog.</summary>
public static class BloodSupplierAuthorizationRule
{
    public const string CreateCode = "SUPPLIER-CREATE-PERM";
    public const string UpdateCode = "SUPPLIER-UPD-PERM";
    public const string ActivateCode = "SUPPLIER-ACT-PERM";
    public const string DeactivateCode = "SUPPLIER-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a blood supplier requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a blood supplier requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a blood supplier requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a blood supplier requires the admin.config.activate permission.");
}
