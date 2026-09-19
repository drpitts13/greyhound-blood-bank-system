namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for coded-comment catalog mutations.
/// </summary>
public static class CodedCommentAuthorizationRule
{
    public const string CreateCode = "CMT-CREATE-PERM";
    public const string UpdateCode = "CMT-UPD-PERM";
    public const string ActivateCode = "CMT-ACT-PERM";
    public const string DeactivateCode = "CMT-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a coded comment requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a coded comment requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(
                ActivateCode,
                "Activating a coded comment requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(
                DeactivateCode,
                "Deactivating a coded comment requires the admin.config.activate permission.");
}
