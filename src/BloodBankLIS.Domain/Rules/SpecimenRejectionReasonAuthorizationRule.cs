namespace BloodBankLIS.Domain.Rules;

public static class SpecimenRejectionReasonAuthorizationRule
{
    public const string CreateCode = "REJECTREASON-CREATE-PERM";
    public const string UpdateCode = "REJECTREASON-UPDATE-PERM";
    public const string ActivateCode = "REJECTREASON-ACT-PERM";
    public const string DeactivateCode = "REJECTREASON-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(CreateCode, "Adding a rejection reason requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(UpdateCode, "Editing a rejection reason requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(ActivateCode, "Activating a rejection reason requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(DeactivateCode, "Deactivating a rejection reason requires the admin.config.activate permission.");
}
