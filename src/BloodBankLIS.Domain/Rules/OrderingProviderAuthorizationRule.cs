namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for ordering-provider catalog mutations that change
/// who can be named on a type-and-screen or product order.
/// </summary>
public static class OrderingProviderAuthorizationRule
{
    public const string CreateCode = "ORDPROV-CREATE-PERM";
    public const string UpdateCode = "ORDPROV-UPD-PERM";
    public const string ActivateCode = "ORDPROV-ACT-PERM";
    public const string DeactivateCode = "ORDPROV-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an ordering provider requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an ordering provider requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating an ordering provider requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating an ordering provider requires the admin.config.activate permission.");
}
