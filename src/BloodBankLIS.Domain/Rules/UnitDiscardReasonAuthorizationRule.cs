namespace BloodBankLIS.Domain.Rules;

public static class UnitDiscardReasonAuthorizationRule
{
    public const string CreateCode = "DISCARD-CREATE-PERM";
    public const string UpdateCode = "DISCARD-UPDATE-PERM";
    public const string ActivateCode = "DISCARD-ACT-PERM";
    public const string DeactivateCode = "DISCARD-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(CreateCode, "Adding a discard reason requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(UpdateCode, "Editing a discard reason requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(ActivateCode, "Activating a discard reason requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(DeactivateCode, "Deactivating a discard reason requires the admin.config.activate permission.");
}
