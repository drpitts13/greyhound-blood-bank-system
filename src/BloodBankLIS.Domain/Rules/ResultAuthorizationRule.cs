namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for entering, correcting, and verifying results, including unit
/// ABO/Rh retype that can move a unit to Available.
/// </summary>
public static class ResultAuthorizationRule
{
    public const string EnterCode = "RES-ENTER-PERM";
    public const string CorrectCode = "RES-CORRECT-PERM";
    public const string VerifyCode = "RES-VERIFY-PERM";

    public static RuleResult EvaluateEnter(bool hasResultEnter) =>
        hasResultEnter
            ? RuleResult.Pass(EnterCode)
            : RuleResult.HardStop(
                EnterCode,
                "Entering a result requires the result.enter permission.");

    public static RuleResult EvaluateCorrect(bool hasResultCorrect) =>
        hasResultCorrect
            ? RuleResult.Pass(CorrectCode)
            : RuleResult.HardStop(
                CorrectCode,
                "Correcting a verified result requires the result.correct permission.");

    public static RuleResult EvaluateVerify(bool hasResultVerify) =>
        hasResultVerify
            ? RuleResult.Pass(VerifyCode)
            : RuleResult.HardStop(
                VerifyCode,
                "Verifying a result requires the result.verify permission.");
}

