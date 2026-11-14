using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// When a lookback notification's physician is to be notified by: the day it was opened plus the facility's days
/// (OCD-089). Null while the setting is not entered, or once the notification is no longer open. A reminder only.
/// </summary>
public static class LookbackNotifyRule
{
    public static DateTime? NotifyBy(DateTime openedUtc, int withinDays, LookbackNotificationStatus status) =>
        withinDays <= 0 || openedUtc == default || status is not (LookbackNotificationStatus.Pending or LookbackNotificationStatus.Attempted)
            ? null
            : openedUtc.AddDays(withinDays);
}
