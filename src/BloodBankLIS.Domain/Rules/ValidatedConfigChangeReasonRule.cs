namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Configuration that records a facility validation or a medical-director decision (transport containers with their
/// validated range and hours; MTP pack contents) records why it was changed, or switched on or off. Same minimum as
/// the other safety catalogs.
/// </summary>
public static class ValidatedConfigChangeReasonRule
{
    public const string Code = "CFG.REASON.REQUIRED";
    public const int MinimumLength = 8;

    public static RuleResult Evaluate(string? reason, string what) =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < MinimumLength
            ? RuleResult.HardStop(Code, $"Give a reason of at least {MinimumLength} characters for changing {what}.")
            : RuleResult.Pass(Code);
}
