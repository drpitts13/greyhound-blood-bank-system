namespace BloodBankLIS.Application.Specimens;

/// <summary>
/// Pending-specimen rows that can no longer be received or accepted because the tube expired while waiting. They need
/// rejecting (and usually a new specimen), so the dashboard names them instead of counting them as routine work.
/// </summary>
public static class PendingSpecimenAttentionRule
{
    public static int CountExpired(IEnumerable<PendingSpecimenWorkItemDto> rows) =>
        rows.Count(static row => row.IsExpired);

    /// <summary>What to do with an expired row in place of the Receive or Accept it can no longer take.</summary>
    public const string ExpiredNextStep = "Expired: reject it";
}
