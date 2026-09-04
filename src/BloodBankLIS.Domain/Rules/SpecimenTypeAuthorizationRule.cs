namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for specimen-type catalog mutations that change
/// which tests can be entered on a specimen.
/// </summary>
public static class SpecimenTypeAuthorizationRule
{
    public const string CreateCode = "SPECTYPE-CREATE-PERM";
    public const string UpdateCode = "SPECTYPE-UPD-PERM";
    public const string ActivateCode = "SPECTYPE-ACT-PERM";
    public const string DeactivateCode = "SPECTYPE-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a specimen type definition requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a specimen type definition requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a specimen type definition requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a specimen type definition requires the admin.config.activate permission.");
}
