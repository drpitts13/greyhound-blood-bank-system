using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// What became of a reservation, in words, for the patient, order, and specimen timelines: still reserved, issued,
/// released (with the reason given), or ended because the unit expired (with the sweep's reason, naming the unit and
/// when). Before Cycle 600 the timelines printed the status code ("Now Consumed") and gave a reason only for a release.
/// </summary>
public static class AllocationTimelineNote
{
    public static string Describe(Allocation allocation) => Describe(allocation.Status, allocation.ReleaseReason);

    public static string Describe(AllocationStatus status, string? reason)
    {
        var why = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return status switch
        {
            AllocationStatus.Reserved => "Still reserved",
            AllocationStatus.Consumed => "Issued",
            AllocationStatus.Released => why is null ? "Released" : $"Released: {why}",
            AllocationStatus.Expired => why is null ? "Ended: the unit expired" : $"Ended: {why}",
            _ => status.ToString()
        };
    }
}
