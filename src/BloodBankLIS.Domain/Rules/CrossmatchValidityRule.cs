namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Evaluates whether the crossmatch precondition for issuing is satisfied. For a
/// crossmatch-required product, a compatible and unexpired crossmatch must exist.
/// Inside an emergency release, a missing crossmatch is an overridable Warning;
/// outside it, a HardStop (see docs/safety-rules.md section 1 and workflows 6).
/// </summary>
public static class CrossmatchValidityRule
{
    public const string Code = "ISS-XM-REQUIRED";

    public static RuleResult Evaluate(bool requiresCrossmatch, bool hasValidCrossmatch, bool isEmergencyRelease)
    {
        if (!requiresCrossmatch || hasValidCrossmatch)
        {
            return RuleResult.Pass(Code);
        }

        return isEmergencyRelease
            ? RuleResult.Warning(Code, "No compatible, unexpired crossmatch — permitted only via authorized emergency release.")
            : RuleResult.HardStop(Code, "A compatible, unexpired crossmatch is required for this product.");
    }
}
