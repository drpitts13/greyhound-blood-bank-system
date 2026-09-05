namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for HL7 value-translation replacements that change
/// how inbound result codes map to internal values.
/// </summary>
public static class InterfaceTranslationAuthorizationRule
{
    public const string ReplaceCode = "HL7XLAT-REPLACE-PERM";

    public static RuleResult EvaluateReplace(bool hasAdminHl7Manage) =>
        hasAdminHl7Manage
            ? RuleResult.Pass(ReplaceCode)
            : RuleResult.HardStop(
                ReplaceCode,
                "Replacing HL7 value translations requires the admin.hl7.manage permission.");
}
