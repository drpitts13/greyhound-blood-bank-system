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

    /// <summary>A requested expiration later than the policy-computed one: an extension, which the medical director approves.</summary>
    public static bool IsExtension(DateTime policyExpiresUtc, DateTime requestedExpiresUtc) =>
        TruncateToSeconds(requestedExpiresUtc) > TruncateToSeconds(policyExpiresUtc);

    /// <summary>
    /// Whether a save needs the expiration override: the expiration differs from the policy value, unless it is the
    /// expiration the tube already has and collection is unchanged (an extended tube's comment can be edited without
    /// approving the extension again).
    /// </summary>
    public static bool NeedsOverride(DateTime policyExpiresUtc, DateTime requestedExpiresUtc, DateTime? currentExpiresUtc, bool collectionChanged) =>
        Evaluate(policyExpiresUtc, requestedExpiresUtc, overrideAuthorized: false).Severity == RuleSeverity.Warning
        && !(currentExpiresUtc is DateTime current && !collectionChanged
             && TruncateToSeconds(current) == TruncateToSeconds(requestedExpiresUtc));

    /// <summary>
    /// An extension past the policy window (Facility policy: Medical director may extend tube expiration). Off, it is
    /// refused; on, it is a warning answered by the override, naming the medical director who approved it.
    /// </summary>
    public static RuleResult EvaluateExtension(bool extensionAllowed) =>
        extensionAllowed
            ? RuleResult.Warning(Code, ExtensionNeedsApprovalText)
            : RuleResult.HardStop(Code, ExtensionNotAllowedText);

    public const string ExtensionNeedsApprovalText =
        "Extending a tube's expiration past the policy window needs the medical director's approval: name the medical director under Authorized by and give the reason.";

    public const string ExtensionNotAllowedText =
        "Your facility does not allow extending a tube's expiration (Facility policy: Medical director may extend tube expiration). An earlier expiration can still be set with an override.";

    public static DateTime TruncateToSeconds(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, DateTimeKind.Utc);
}
