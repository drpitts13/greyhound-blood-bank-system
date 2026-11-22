namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// An exception definition decides who may override a safety rule and whether it can be overridden at all,
/// so changing one, or switching it on or off, records why. Same minimum as a facility policy edit.
/// </summary>
public static class ExceptionCatalogChangeReasonRule
{
    public const string Code = "EXC.REASON.REQUIRED";
    public const int MinimumLength = 8;

    public static RuleResult Evaluate(string? reason) =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < MinimumLength
            ? RuleResult.HardStop(Code, $"Give a reason of at least {MinimumLength} characters for changing an override exception.")
            : RuleResult.Pass(Code);
}
