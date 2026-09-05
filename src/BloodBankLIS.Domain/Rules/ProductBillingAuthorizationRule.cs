namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for product-billing catalog mutations that change
/// which ISBT product bills after issue or transfusion.
/// </summary>
public static class ProductBillingAuthorizationRule
{
    public const string CreateCode = "PRODBILL-CREATE-PERM";
    public const string UpdateCode = "PRODBILL-UPD-PERM";
    public const string ActivateCode = "PRODBILL-ACT-PERM";
    public const string DeactivateCode = "PRODBILL-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a product billing row requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a product billing row requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a product billing row requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a product billing row requires the admin.config.activate permission.");
}
