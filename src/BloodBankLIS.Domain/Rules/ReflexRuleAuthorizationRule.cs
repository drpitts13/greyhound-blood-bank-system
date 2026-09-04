namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for reflex-rule catalog mutations that auto-order
/// follow-up tests after a trigger result.
/// </summary>
public static class ReflexRuleAuthorizationRule
{
    public const string CreateCode = "REFLEX-CREATE-PERM";
    public const string UpdateCode = "REFLEX-UPD-PERM";
    public const string ActivateCode = "REFLEX-ACT-PERM";
    public const string DeactivateCode = "REFLEX-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a reflex rule requires the admin.tests.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a reflex rule requires the admin.tests.manage permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a reflex rule requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a reflex rule requires the admin.config.activate permission.");
}
