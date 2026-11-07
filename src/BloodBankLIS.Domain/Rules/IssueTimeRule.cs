namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The recorded issue time. It cannot be in the future. An issue time earlier than now is a late entry (for example
/// paper documentation entered after downtime): it needs a reason, and the record keeps both the stated time and when
/// it was entered. The issue gate still runs at the time of entry; a late entry does not move the checks back in time.
/// </summary>
public static class IssueTimeRule
{
    public const string Code = "ISS-TIME";
    public static readonly TimeSpan ClockAllowance = TimeSpan.FromMinutes(5);

    public static bool IsLateEntry(DateTime issuedUtc, DateTime nowUtc) => issuedUtc < nowUtc - ClockAllowance;

    public static RuleResult Evaluate(DateTime issuedUtc, DateTime nowUtc, string? lateEntryReason)
    {
        if (issuedUtc > nowUtc + ClockAllowance)
        {
            return RuleResult.HardStop(Code, "The issue time is in the future. Enter when the unit actually left the blood bank.");
        }

        if (IsLateEntry(issuedUtc, nowUtc) && string.IsNullOrWhiteSpace(lateEntryReason))
        {
            return RuleResult.HardStop(Code,
                "The issue time is earlier than now, so this is a late entry. Give the reason (for example: issued on paper during downtime).");
        }

        return RuleResult.Pass(Code);
    }
}
