namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for test/service-billing catalog mutations that change
/// which verified test bills after result release.
/// </summary>
public static class TestServiceBillingAuthorizationRule
{
    public const string CreateCode = "TSBILL-CREATE-PERM";
    public const string UpdateCode = "TSBILL-UPD-PERM";
    public const string ActivateCode = "TSBILL-ACT-PERM";
    public const string DeactivateCode = "TSBILL-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a test/service billing row requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a test/service billing row requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a test/service billing row requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a test/service billing row requires the admin.config.activate permission.");
}
