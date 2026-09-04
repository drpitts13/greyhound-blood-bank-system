namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for verifying an entered result, including unit ABO/Rh retype
/// that can move a unit to Available.
/// </summary>
public static class ResultAuthorizationRule
{
    public const string VerifyCode = "RES-VERIFY-PERM";

    public static RuleResult EvaluateVerify(bool hasResultVerify) =>
        hasResultVerify
            ? RuleResult.Pass(VerifyCode)
            : RuleResult.HardStop(
                VerifyCode,
                "Verifying a result requires the result.verify permission.");
}
