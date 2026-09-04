namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for subtest-catalog mutations used at panel
/// interpretation and graded-reaction entry.
/// </summary>
public static class SubtestCatalogAuthorizationRule
{
    public const string CreateCode = "SUBTEST-CREATE-PERM";
    public const string UpdateCode = "SUBTEST-UPD-PERM";
    public const string ActivateCode = "SUBTEST-ACT-PERM";
    public const string DeactivateCode = "SUBTEST-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a subtest definition requires the admin.tests.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a subtest definition requires the admin.tests.manage permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a subtest definition requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a subtest definition requires the admin.config.activate permission.");
}
