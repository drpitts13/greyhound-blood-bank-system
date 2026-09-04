namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for manually setting the current ABO/Rh (not a verified result).
/// </summary>
public static class ImmunoAuthorizationRule
{
    public const string ManualBloodTypeCode = "IH-ABO-PERM";

    public static RuleResult EvaluateManualBloodType(bool hasImmunoOverride)
    {
        return hasImmunoOverride
            ? RuleResult.Pass(ManualBloodTypeCode)
            : RuleResult.HardStop(
                ManualBloodTypeCode,
                "Manually recording ABO/Rh requires the immuno.override permission.");
    }
}
