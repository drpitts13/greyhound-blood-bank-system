namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for test-catalog mutations that change interpretation,
/// history posting, and compatibility.
/// </summary>
public static class TestCatalogAuthorizationRule
{
    public const string CreateCode = "TEST-CREATE-PERM";
    public const string UpdateCode = "TEST-UPD-PERM";
    public const string ActivateCode = "TEST-ACT-PERM";
    public const string DeactivateCode = "TEST-DEACT-PERM";
    public const string CloneCode = "TEST-CLONE-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a test definition requires the admin.tests.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a test definition requires the admin.tests.manage permission.");

    public static RuleResult EvaluateActivate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a test definition requires the admin.tests.manage permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a test definition requires the admin.tests.manage permission.");

    public static RuleResult EvaluateClone(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(CloneCode)
            : RuleResult.HardStop(
                CloneCode,
                "Cloning a test definition requires the admin.tests.manage permission.");
}
