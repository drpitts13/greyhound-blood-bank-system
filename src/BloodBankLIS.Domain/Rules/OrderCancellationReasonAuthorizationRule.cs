namespace BloodBankLIS.Domain.Rules;

public static class OrderCancellationReasonAuthorizationRule
{
    public const string CreateCode = "OCANCEL-CREATE-PERM";
    public const string UpdateCode = "OCANCEL-UPDATE-PERM";
    public const string ActivateCode = "OCANCEL-ACT-PERM";
    public const string DeactivateCode = "OCANCEL-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(CreateCode, "Adding a cancellation reason requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(UpdateCode, "Editing a cancellation reason requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(ActivateCode, "Activating a cancellation reason requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(DeactivateCode, "Deactivating a cancellation reason requires the admin.config.activate permission.");
}
