namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for compatibility-table version and rule mutations
/// used after type verify at issue.
/// </summary>
public static class CompatibilityTableAuthorizationRule
{
    public const string CreateVersionCode = "XM-VER-CREATE-PERM";
    public const string UpdateVersionCode = "XM-VER-UPD-PERM";
    public const string ActivateVersionCode = "XM-VER-ACT-PERM";
    public const string RetireVersionCode = "XM-VER-RETIRE-PERM";
    public const string CreateRuleCode = "XM-RULE-CREATE-PERM";
    public const string UpdateRuleCode = "XM-RULE-UPD-PERM";
    public const string ActivateRuleCode = "XM-RULE-ACT-PERM";
    public const string DeactivateRuleCode = "XM-RULE-DEACT-PERM";

    public static RuleResult EvaluateCreateVersion(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateVersionCode)
            : RuleResult.HardStop(
                CreateVersionCode,
                "Creating a compatibility table version requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdateVersion(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateVersionCode)
            : RuleResult.HardStop(
                UpdateVersionCode,
                "Updating a compatibility table version requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivateVersion(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateVersionCode)
            : RuleResult.HardStop(
                ActivateVersionCode,
                "Activating a compatibility table version requires the admin.config.activate permission.");

    public static RuleResult EvaluateRetireVersion(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(RetireVersionCode)
            : RuleResult.HardStop(
                RetireVersionCode,
                "Retiring a compatibility table version requires the admin.config.activate permission.");

    public static RuleResult EvaluateCreateRule(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateRuleCode)
            : RuleResult.HardStop(
                CreateRuleCode,
                "Creating a compatibility rule requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdateRule(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateRuleCode)
            : RuleResult.HardStop(
                UpdateRuleCode,
                "Updating a compatibility rule requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivateRule(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateRuleCode)
            : RuleResult.HardStop(
                ActivateRuleCode,
                "Activating a compatibility rule requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivateRule(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateRuleCode)
            : RuleResult.HardStop(
                DeactivateRuleCode,
                "Deactivating a compatibility rule requires the admin.config.activate permission.");
}
