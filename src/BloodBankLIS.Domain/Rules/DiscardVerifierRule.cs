namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace dual control at discard: a distinct second verifier must
/// approve destroying a unit so a single operator cannot silently remove inventory.
/// </summary>
public static class DiscardVerifierRule
{
    public const string Code = "INV-DISC-2ND";

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
                "A second verifier is required to discard a unit.");
        }

        if (string.Equals(primaryUser.Trim(), secondVerifier.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(
                Code,
                "The second verifier must be a different user from the person discarding the unit.");
        }

        return RuleResult.Pass(Code);
    }
}
