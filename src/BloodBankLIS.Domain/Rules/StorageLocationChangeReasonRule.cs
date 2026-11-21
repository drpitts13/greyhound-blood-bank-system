namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A storage location's settings are enforced at transfer and issue (storage range, which components it may hold,
/// remote and electronic issue, second verifier), so changing one, or switching it on or off, records why. Same minimum
/// as the other safety catalogs.
/// </summary>
public static class StorageLocationChangeReasonRule
{
    public const string Code = "LOC.REASON.REQUIRED";
    public const int MinimumLength = 8;

    public static RuleResult Evaluate(string? reason) =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < MinimumLength
            ? RuleResult.HardStop(Code, $"Give a reason of at least {MinimumLength} characters for changing a storage location.")
            : RuleResult.Pass(Code);
}
