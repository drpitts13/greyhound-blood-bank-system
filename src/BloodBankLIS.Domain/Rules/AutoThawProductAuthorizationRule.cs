namespace BloodBankLIS.Domain.Rules;

/// <summary>Privilege gates for the auto-thaw product catalog.</summary>
public static class AutoThawProductAuthorizationRule
{
    public const string CreateCode = "AUTOTHAW-CREATE-PERM";
    public const string ActivateCode = "AUTOTHAW-ACT-PERM";
    public const string DeactivateCode = "AUTOTHAW-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Adding an auto-thaw product requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating an auto-thaw product requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating an auto-thaw product requires the admin.config.activate permission.");
}
