namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace dual control when releasing a directed unit into volunteer
/// (allogeneic) inventory so a single operator cannot silently drop the recipient lock.
/// </summary>
public static class DirectedConversionVerifierRule
{
    public const string Code = "INV-DIR-CONV-2ND";

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
                "A second verifier is required to convert a directed unit to allogeneic inventory.");
        }

        if (string.Equals(primaryUser.Trim(), secondVerifier.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(
                Code,
                "The second verifier must be a different user from the person converting the unit.");
        }

        return RuleResult.Pass(Code);
    }
}
