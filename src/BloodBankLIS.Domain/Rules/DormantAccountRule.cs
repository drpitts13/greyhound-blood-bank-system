namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// An active person's account with no sign-in within the chosen number of days (or never), shown in an access review so
/// it can be deactivated if no longer needed. Service accounts and inactive accounts are not dormant. The number of days
/// is the reviewer's choice, not a rule of the software.
/// </summary>
public static class DormantAccountRule
{
    public static bool IsDormant(bool isActive, bool isServiceAccount, DateTime? lastLoginUtc, DateTime nowUtc, int days) =>
        isActive && !isServiceAccount && days > 0
        && (lastLoginUtc is not DateTime last || last < nowUtc.AddDays(-days));
}
