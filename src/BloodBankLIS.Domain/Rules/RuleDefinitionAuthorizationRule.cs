namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for order/test rule-definition mutations that add,
/// cancel, or block tests after an order or result.
/// </summary>
public static class RuleDefinitionAuthorizationRule
{
    public const string CreateCode = "RULEDEF-CREATE-PERM";
    public const string UpdateCode = "RULEDEF-UPD-PERM";
    public const string ActivateCode = "RULEDEF-ACT-PERM";
    public const string DeactivateCode = "RULEDEF-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an order or test rule requires the admin.tests.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminTestsManage) =>
        hasAdminTestsManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an order or test rule requires the admin.tests.manage permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating an order or test rule requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating an order or test rule requires the admin.config.activate permission.");
}
