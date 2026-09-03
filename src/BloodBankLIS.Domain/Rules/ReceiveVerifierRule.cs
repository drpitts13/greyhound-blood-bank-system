namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace dual control at receipt: a distinct second verifier must
/// confirm walk-in, expected-arrival, and ISBT receive.
/// </summary>
public static class ReceiveVerifierRule
{
    public const string Code = "INV-RCV-2ND";

    public static RuleResult Evaluate(string primaryUser, string? secondVerifier, bool required)
    {
        if (!required)
        {
            return RuleResult.Pass(Code);
        }

        if (string.IsNullOrWhiteSpace(secondVerifier))
        {
            return RuleResult.HardStop(
                Code,
                "A second verifier is required to receive a unit into inventory.");
        }

        if (string.Equals(primaryUser.Trim(), secondVerifier.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(
                Code,
                "The second verifier must be a different user from the person receiving the unit.");
        }

        return RuleResult.Pass(Code);
    }
}
