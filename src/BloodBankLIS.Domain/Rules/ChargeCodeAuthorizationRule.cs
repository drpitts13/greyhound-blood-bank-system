namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for charge-code catalog mutations that change
/// which codes can be billed after issue or transfusion.
/// </summary>
public static class ChargeCodeAuthorizationRule
{
    public const string CreateCode = "CHG-CREATE-PERM";
    public const string UpdateCode = "CHG-UPD-PERM";
    public const string ActivateCode = "CHG-ACT-PERM";
    public const string DeactivateCode = "CHG-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a charge code requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a charge code requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a charge code requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a charge code requires the admin.config.activate permission.");
}
