namespace BloodBankLIS.Domain.Rules;

/// <summary>Privilege gate for in-place auto-thaw of an allocated unit.</summary>
public static class AutoThawAuthorizationRule
{
    public const string ApplyCode = "AUTOTHAW-APPLY-PERM";

    public static RuleResult EvaluateApply(bool hasCompatibilityAllocate) =>
        hasCompatibilityAllocate
            ? RuleResult.Pass(ApplyCode)
            : RuleResult.HardStop(
                ApplyCode,
                "Auto-thawing an allocated product requires the compatibility.allocate permission.");
}
