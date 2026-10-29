namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Name changes retain the previous display name as a historical alias. A reason
/// is required when the name changes. Does not invent merge or unmerge policy.
/// </summary>
public static class PatientHistoricalAliasRule
{
    public const string ReasonCode = "PAT-ALIAS-REASON";
    public const string AdtArchiveReason = "HL7 ADT demographics name change.";
    public const int ValueMaxLength = 100;

    public static string FormatDisplayName(string lastName, string firstName, string? middleName)
    {
        var last = (lastName ?? string.Empty).Trim();
        var first = (firstName ?? string.Empty).Trim();
        var middle = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim();
        var given = middle is null ? first : $"{first} {middle}";
        return string.IsNullOrWhiteSpace(given) ? last : $"{last}, {given}";
    }

    public static bool NameChanged(
        string previousLast,
        string previousFirst,
        string? previousMiddle,
        string nextLast,
        string nextFirst,
        string? nextMiddle) =>
        !string.Equals(previousLast?.Trim(), nextLast?.Trim(), StringComparison.OrdinalIgnoreCase)
        || !string.Equals(previousFirst?.Trim(), nextFirst?.Trim(), StringComparison.OrdinalIgnoreCase)
        || !string.Equals(
            string.IsNullOrWhiteSpace(previousMiddle) ? null : previousMiddle.Trim(),
            string.IsNullOrWhiteSpace(nextMiddle) ? null : nextMiddle.Trim(),
            StringComparison.OrdinalIgnoreCase);

    public static RuleResult EvaluateNameChangeReason(bool nameChanged, string? reason) =>
        !nameChanged
            ? RuleResult.Pass(ReasonCode)
            : string.IsNullOrWhiteSpace(reason)
                ? RuleResult.HardStop(ReasonCode, "A reason is required when changing the patient name.")
                : RuleResult.Pass(ReasonCode);

    public static string TruncateAliasValue(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= ValueMaxLength
            ? trimmed
            : trimmed[..ValueMaxLength];
    }
}
