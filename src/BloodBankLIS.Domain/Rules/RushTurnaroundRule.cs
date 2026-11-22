using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The facility's turnaround target for rush tests (end-user testing pass 106: a STAT type and screen ordered days ago
/// looked the same on the worklist as one ordered five minutes ago, and the oldest rush work sorted last). A STAT, urgent,
/// emergency-release or massive-transfusion test not yet verified is past the target when it has waited longer than the
/// target since it was ordered. The target is the facility's to set; 0 means not set and nothing is marked.
/// </summary>
public static class RushTurnaroundRule
{
    public const int MaxTargetMinutes = 1440;

    public static bool IsRush(OrderPriority priority) =>
        priority is OrderPriority.Stat or OrderPriority.Urgent or OrderPriority.EmergencyRelease or OrderPriority.MassiveTransfusionProtocol;

    /// <summary>
    /// "Past the 60 min target: waiting 2 h 15 min", or null when no target is set, the test is not rush, it is verified,
    /// or it is still within the target.
    /// </summary>
    public static string? Notice(OrderPriority priority, DateTime orderedUtc, ResultStatus? resultStatus, int targetMinutes, DateTime nowUtc)
    {
        if (targetMinutes <= 0 || !IsRush(priority) || resultStatus is ResultStatus.Verified or ResultStatus.Corrected)
        {
            return null;
        }

        var waited = nowUtc - orderedUtc;
        return waited.TotalMinutes > targetMinutes
            ? $"Past the {targetMinutes} min target: waiting {Duration(waited)}"
            : null;
    }

    /// <summary>"45 min", "2 h 15 min", "3 d 4 h".</summary>
    public static string Duration(TimeSpan span)
    {
        if (span.TotalDays >= 1)
        {
            return $"{(int)span.TotalDays} d {span.Hours} h";
        }

        return span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes} min" : $"{Math.Max(0, (int)span.TotalMinutes)} min";
    }
}
