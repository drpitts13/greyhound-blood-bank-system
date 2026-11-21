using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The recipient lookback notifications still open (Pending or Attempted) for a unit's donation, which the unit page
/// shows (end-user testing pass 218: W000123LSIB001's page showed its DIN but not that the donation was under lookback).
/// </summary>
public static class UnitLookbackNotifications
{
    public static List<LookbackNotificationDto> OpenFor(string? din, IEnumerable<LookbackNotificationDto>? notifications) =>
        string.IsNullOrWhiteSpace(din) || notifications is null
            ? []
            : notifications
                .Where(n => string.Equals(n.Din?.Trim(), din.Trim(), StringComparison.OrdinalIgnoreCase)
                    && n.Status is LookbackNotificationStatus.Pending or LookbackNotificationStatus.Attempted)
                .ToList();
}
