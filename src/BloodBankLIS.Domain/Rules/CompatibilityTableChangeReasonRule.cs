namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A rule in the active compatibility table is approved, in-use configuration (MEDICAL_DIRECTOR_APPROVAL),
/// so adding, changing, or switching one records why, as activating or retiring the table does.
/// Rules in a draft version change freely until that version is activated with a reason.
/// </summary>
public static class CompatibilityTableChangeReasonRule
{
    public const string Code = "COMPAT.REASON.REQUIRED";
    public const int MinimumLength = 8;

    public static RuleResult Evaluate(bool versionIsActive, string? reason) =>
        versionIsActive && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < MinimumLength)
            ? RuleResult.HardStop(
                Code,
                $"This rule is in the active compatibility table. Give a reason of at least {MinimumLength} characters, or make the change in a draft version.")
            : RuleResult.Pass(Code);
}
