namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Second-person check must name an active application user (SoftBank / SafeTrace
/// two-person verification). Free-text initials are not accepted.
/// </summary>
public static class SecondVerifierDirectoryRule
{
    public const string Code = "TX-SECOND-USER";

    public static RuleResult Evaluate(string? secondVerifier, bool isActiveUser)
    {
        if (string.IsNullOrWhiteSpace(secondVerifier))
        {
            return RuleResult.Pass(Code);
        }

        return isActiveUser
            ? RuleResult.Pass(Code)
            : RuleResult.HardStop(Code, "Second verifier must be an active system user, not free text.");
    }
}
