namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege and identity gates for antibody panel manufacturers.
/// Creating or withdrawing a manufacturer does not identify antibodies.
/// </summary>
public static class AntibodyPanelManufacturerAdminRule
{
    public const string CreatePermCode = "ABID-MFG-CREATE-PERM";
    public const string IdentityCode = "ABID-MFG-CREATE-ID";
    public const string DuplicateCode = "ABID-MFG-CREATE-DUP";
    public const string ActivatePermCode = "ABID-MFG-ACT-PERM";
    public const string DeactivatePermCode = "ABID-MFG-DEACT-PERM";
    public const string DeactivateReasonCode = "ABID-MFG-DEACT-REASON";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreatePermCode)
            : RuleResult.HardStop(
                CreatePermCode,
                "Creating an antibody panel manufacturer requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivatePermCode)
            : RuleResult.HardStop(
                ActivatePermCode,
                "Activating an antibody panel manufacturer requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivatePermCode)
            : RuleResult.HardStop(
                DeactivatePermCode,
                "Deactivating an antibody panel manufacturer requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivateReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? RuleResult.HardStop(
                DeactivateReasonCode,
                "Deactivating an antibody panel manufacturer requires a reason. Withdrawal does not identify antibodies or void an open workup.")
            : RuleResult.Pass(DeactivateReasonCode);

    public static IReadOnlyList<RuleResult> EvaluateCreateDraft(string? code, string? name, bool duplicateCode)
    {
        var stops = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            stops.Add(RuleResult.HardStop(
                IdentityCode,
                "Manufacturer code and name are required. Creating a manufacturer does not identify antibodies."));
        }

        if (duplicateCode)
        {
            stops.Add(RuleResult.HardStop(
                DuplicateCode,
                "That manufacturer code is already in use."));
        }

        return stops;
    }
}
