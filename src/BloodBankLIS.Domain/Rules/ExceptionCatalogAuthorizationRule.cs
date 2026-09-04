namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for exception-definition mutations that control
/// whether a HardStop can be overridden at result verify.
/// </summary>
public static class ExceptionCatalogAuthorizationRule
{
    public const string CreateCode = "EXC-CREATE-PERM";
    public const string UpdateCode = "EXC-UPD-PERM";
    public const string ActivateCode = "EXC-ACT-PERM";
    public const string DeactivateCode = "EXC-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an exception definition requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an exception definition requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating an exception definition requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating an exception definition requires the admin.config.activate permission.");
}
