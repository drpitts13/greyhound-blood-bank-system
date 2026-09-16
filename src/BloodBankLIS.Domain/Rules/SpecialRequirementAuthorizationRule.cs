namespace BloodBankLIS.Domain.Rules;

/// <summary>Privilege gates for special-requirement catalog mutations.</summary>
public static class SpecialRequirementAuthorizationRule
{
    public const string CreateCode = "SRDEF-CREATE-PERM";
    public const string UpdateCode = "SRDEF-UPD-PERM";
    public const string ActivateCode = "SRDEF-ACT-PERM";
    public const string DeactivateCode = "SRDEF-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a special requirement definition requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a special requirement definition requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a special requirement definition requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a special requirement definition requires the admin.config.activate permission.");
}
