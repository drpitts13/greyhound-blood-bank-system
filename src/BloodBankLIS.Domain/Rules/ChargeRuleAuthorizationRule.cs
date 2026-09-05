namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for charge-rule catalog mutations that change
/// which trigger maps to a billable charge after issue or transfusion.
/// </summary>
public static class ChargeRuleAuthorizationRule
{
    public const string CreateCode = "CHGRULE-CREATE-PERM";
    public const string UpdateCode = "CHGRULE-UPD-PERM";
    public const string ActivateCode = "CHGRULE-ACT-PERM";
    public const string DeactivateCode = "CHGRULE-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a charge rule requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a charge rule requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a charge rule requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a charge rule requires the admin.config.activate permission.");
}
