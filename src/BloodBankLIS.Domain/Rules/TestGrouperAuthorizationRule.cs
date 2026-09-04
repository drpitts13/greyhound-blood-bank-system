namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for test-grouper catalog mutations that change
/// which tests are ordered together (for example Type and Screen).
/// </summary>
public static class TestGrouperAuthorizationRule
{
    public const string CreateCode = "GROUPER-CREATE-PERM";
    public const string UpdateCode = "GROUPER-UPD-PERM";
    public const string ActivateCode = "GROUPER-ACT-PERM";
    public const string DeactivateCode = "GROUPER-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a test grouper requires the admin.tests.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a test grouper requires the admin.tests.manage permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a test grouper requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a test grouper requires the admin.config.activate permission.");
}
