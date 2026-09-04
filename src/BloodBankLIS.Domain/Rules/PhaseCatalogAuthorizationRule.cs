namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for phase-catalog mutations used at result entry
/// and panel interpretation (including check-cell phases).
/// </summary>
public static class PhaseCatalogAuthorizationRule
{
    public const string CreateCode = "PHASE-CREATE-PERM";
    public const string UpdateCode = "PHASE-UPD-PERM";
    public const string ActivateCode = "PHASE-ACT-PERM";
    public const string DeactivateCode = "PHASE-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a phase definition requires the admin.tests.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a phase definition requires the admin.tests.manage permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a phase definition requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a phase definition requires the admin.config.activate permission.");
}
