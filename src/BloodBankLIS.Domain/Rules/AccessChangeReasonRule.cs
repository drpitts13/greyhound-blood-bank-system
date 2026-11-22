namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Roles, permissions, and security levels decide who may verify, issue, and override; activating or
/// unlocking an account lets someone act again. Each such change records why (8+ characters, as for
/// other change-control reasons).
/// </summary>
public static class AccessChangeReasonRule
{
    public const string Code = "ACCESS.REASON.REQUIRED";
    public const int MinimumLength = 8;

    public static RuleResult Evaluate(string? reason) =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < MinimumLength
            ? RuleResult.HardStop(Code, $"Give a reason of at least {MinimumLength} characters for this access change.")
            : RuleResult.Pass(Code);

    public static string? Missing(string? reason) =>
        Evaluate(reason).Severity == RuleSeverity.HardStop ? Evaluate(reason).Message : null;
}
