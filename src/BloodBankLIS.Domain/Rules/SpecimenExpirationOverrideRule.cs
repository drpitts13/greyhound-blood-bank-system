namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Changing a specimen's expiration away from the policy-computed value
/// (<c>CollectedUtc + validity hours</c>) is a Warning that requires an authorized
/// exception override. Collection-only edits that keep expiry on the policy window pass.
/// </summary>
public static class SpecimenExpirationOverrideRule
{
    public const string Code = "SPC-EXP-OVERRIDE";

    public static RuleResult Evaluate(
        DateTime policyExpiresUtc,
        DateTime requestedExpiresUtc,
        bool overrideAuthorized)
    {
        if (TruncateToSeconds(policyExpiresUtc) == TruncateToSeconds(requestedExpiresUtc))
        {
            return RuleResult.Pass(Code);
        }

        return overrideAuthorized
            ? RuleResult.Pass(Code, "Specimen expiration set with authorized override.")
            : RuleResult.Warning(
                Code,
                "Changing specimen expiration from the policy-computed value requires an authorized override.");
    }

    public static DateTime TruncateToSeconds(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, DateTimeKind.Utc);
}
