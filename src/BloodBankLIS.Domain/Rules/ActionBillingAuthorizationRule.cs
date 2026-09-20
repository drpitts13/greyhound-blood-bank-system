namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for action-billing catalog mutations that change
/// which completed actions bill a charge and DFT.
/// </summary>
public static class ActionBillingAuthorizationRule
{
    public const string CreateCode = "ACTBILL-CREATE-PERM";
    public const string UpdateCode = "ACTBILL-UPD-PERM";
    public const string ActivateCode = "ACTBILL-ACT-PERM";
    public const string DeactivateCode = "ACTBILL-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an action billing row requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an action billing row requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating an action billing row requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating an action billing row requires the admin.config.activate permission.");
}
