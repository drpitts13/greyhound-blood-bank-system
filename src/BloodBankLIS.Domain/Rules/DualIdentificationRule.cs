namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Dual identification at issue/transfusion: a second distinct operator, or a
/// validated electronic bedside scan with positive patient identification.
/// </summary>
public static class DualIdentificationRule
{
    public const string Code = "TX-DUAL-ID";

    public static RuleResult Evaluate(
        string primaryUser,
        string? secondVerifier,
        bool electronicIdentificationComplete,
        bool required)
    {
        if (!required)
        {
            return RuleResult.Pass(Code);
        }

        if (electronicIdentificationComplete)
        {
            return RuleResult.Pass(Code);
        }

        if (string.IsNullOrWhiteSpace(secondVerifier))
        {
            return RuleResult.HardStop(Code, "A second verifier or validated electronic identification is required.");
        }

        if (string.Equals(primaryUser.Trim(), secondVerifier.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(Code, "The second verifier must be a different user from the primary operator.");
        }

        return RuleResult.Pass(Code);
    }
}
