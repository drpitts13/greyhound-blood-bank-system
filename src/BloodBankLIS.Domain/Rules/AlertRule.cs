using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>A condition an alert source has found, before it is stored.</summary>
public sealed record AlertCandidate(
    string Source,
    string Key,
    AlertSeverity Severity,
    string Title,
    string Message,
    string? Link = null);

/// <summary>
/// Alert lifecycle. Alerts are in-app only: no email or SMS contract exists.
/// They point at the existing worklists and never change a unit, order, or result.
/// </summary>
public static class AlertRule
{
    public const string TemperatureExcursion = "temperature-excursion";
    public const string RetrospectiveCrossmatchIncompatible = "retro-xm-incompatible";
    public const string UnitsExpired = "units-expired";
    // The stored source value predates the "minimum stock" name; changing it would orphan open alerts.
    public const string MinimumStockShortage = "par-shortage";
    public const string NearExpiry = "near-expiry";
    public const string ReagentQc = "reagent-qc";
    public const string BackupFailed = "backup-failed";
    public const string BackupOverdue = "backup-overdue";
    public const string TransfusionNotCompleted = "transfusion-not-completed";

    /// <summary>Still active: the condition has not cleared. An acknowledged alert can still be active.</summary>
    public static bool IsActive(Alert alert) => alert.ResolvedUtc is null;

    /// <summary>Shown in the inbox: active and nobody has acknowledged it.</summary>
    public static bool IsOpen(Alert alert) => alert.ResolvedUtc is null && alert.AcknowledgedUtc is null;

    /// <summary>
    /// Keys to raise and active alerts to resolve when a condition source reports its
    /// current findings. An active key that is still found is left alone, acknowledged or not.
    /// </summary>
    public static (IReadOnlyList<AlertCandidate> ToRaise, IReadOnlyList<Alert> ToResolve) Reconcile(
        IReadOnlyCollection<Alert> activeForSource,
        IReadOnlyCollection<AlertCandidate> found)
    {
        var activeKeys = activeForSource.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var foundKeys = found.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var toRaise = found
            .GroupBy(c => c.Key, StringComparer.Ordinal)
            .Select(g => g.First())
            .Where(c => !activeKeys.Contains(c.Key))
            .ToList();
        var toResolve = activeForSource.Where(a => !foundKeys.Contains(a.Key)).ToList();
        return (toRaise, toResolve);
    }
}
