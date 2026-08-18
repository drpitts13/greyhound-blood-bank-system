namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Optional AABB/CLIA-style control: the user who entered a result may not verify it.
/// </summary>
public static class SelfVerifyRule
{
    public const string Code = "RES-SELF-VERIFY";

    public static RuleResult Evaluate(string enteredBy, string verifiedBy, bool blockSelfVerify)
    {
        if (!blockSelfVerify)
        {
            return RuleResult.Pass(Code);
        }

        if (string.IsNullOrWhiteSpace(enteredBy) || string.IsNullOrWhiteSpace(verifiedBy))
        {
            return RuleResult.Pass(Code);
        }

        return string.Equals(enteredBy.Trim(), verifiedBy.Trim(), StringComparison.OrdinalIgnoreCase)
            ? RuleResult.HardStop(Code, "The user who entered this result may not verify it.")
            : RuleResult.Pass(Code);
    }
}
