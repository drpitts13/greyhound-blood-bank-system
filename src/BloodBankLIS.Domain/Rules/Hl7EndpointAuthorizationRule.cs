namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for HL7 endpoint mutations that change
/// which interfaces can send or receive clinical messages.
/// </summary>
public static class Hl7EndpointAuthorizationRule
{
    public const string CreateCode = "HL7EP-CREATE-PERM";
    public const string UpdateCode = "HL7EP-UPD-PERM";
    public const string EnableCode = "HL7EP-ENABLE-PERM";
    public const string DisableCode = "HL7EP-DISABLE-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminHl7Manage) =>
        hasAdminHl7Manage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an HL7 endpoint requires the admin.hl7.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminHl7Manage) =>
        hasAdminHl7Manage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an HL7 endpoint requires the admin.hl7.manage permission.");

    public static RuleResult EvaluateEnable(bool hasAdminHl7Manage) =>
        hasAdminHl7Manage
            ? RuleResult.Pass(EnableCode)
            : RuleResult.HardStop(
                EnableCode,
                "Enabling an HL7 endpoint requires the admin.hl7.manage permission.");

    public static RuleResult EvaluateDisable(bool hasAdminHl7Manage) =>
        hasAdminHl7Manage
            ? RuleResult.Pass(DisableCode)
            : RuleResult.HardStop(
                DisableCode,
                "Disabling an HL7 endpoint requires the admin.hl7.manage permission.");
}
