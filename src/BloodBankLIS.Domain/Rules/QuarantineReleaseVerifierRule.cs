namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace quality release: a distinct second verifier must approve
/// moving a unit out of quarantine into Available.
/// </summary>
public static class QuarantineReleaseVerifierRule
{
    public const string Code = "INV-Q-RELEASE-2ND";

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
                "A second verifier is required to release a unit from quarantine.");
        }

        if (string.Equals(primaryUser.Trim(), secondVerifier.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(
                Code,
                "The second verifier must be a different user from the person releasing the unit.");
        }

        return RuleResult.Pass(Code);
    }
}
