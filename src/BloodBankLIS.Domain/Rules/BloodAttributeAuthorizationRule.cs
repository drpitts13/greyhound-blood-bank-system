namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for blood-attribute catalog mutations used at
/// antigen-negative selection and antibody history.
/// </summary>
public static class BloodAttributeAuthorizationRule
{
    public const string CreateCode = "ATTR-CREATE-PERM";
    public const string UpdateCode = "ATTR-UPD-PERM";
    public const string ActivateCode = "ATTR-ACT-PERM";
    public const string DeactivateCode = "ATTR-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a blood attribute definition requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a blood attribute definition requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a blood attribute definition requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a blood attribute definition requires the admin.config.activate permission.");
}
