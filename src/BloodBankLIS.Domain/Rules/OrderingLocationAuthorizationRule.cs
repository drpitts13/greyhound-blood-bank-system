namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for ordering-location catalog mutations that change
/// which ward or department can be named on an order.
/// </summary>
public static class OrderingLocationAuthorizationRule
{
    public const string CreateCode = "ORDLOC-CREATE-PERM";
    public const string UpdateCode = "ORDLOC-UPD-PERM";
    public const string ActivateCode = "ORDLOC-ACT-PERM";
    public const string DeactivateCode = "ORDLOC-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an ordering location requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an ordering location requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating an ordering location requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating an ordering location requires the admin.config.activate permission.");
}
