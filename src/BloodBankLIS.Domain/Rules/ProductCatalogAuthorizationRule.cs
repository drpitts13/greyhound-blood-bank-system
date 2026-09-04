namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for product-definition mutations that change
/// which units can be received, typed, and issued.
/// </summary>
public static class ProductCatalogAuthorizationRule
{
    public const string CreateCode = "PROD-CREATE-PERM";
    public const string UpdateCode = "PROD-UPD-PERM";
    public const string ActivateCode = "PROD-ACT-PERM";
    public const string DeactivateCode = "PROD-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminProductsManage) =>
        hasAdminProductsManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a product definition requires the admin.products.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminProductsManage) =>
        hasAdminProductsManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a product definition requires the admin.products.manage permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a product definition requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a product definition requires the admin.config.activate permission.");
}
