using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// An outage the blood bank worked through on paper (Cycle 662): when it started, when it ended (null while it is still
/// going on), and what was down. Recorded so that recovery does not depend on anyone remembering the window: paper
/// back-entries are checked against it and counted per outage. Never deleted; ending one is a separate audited action.
/// </summary>
public class DowntimeEpisode : BaseEntity
{
    public DateTime StartedUtc { get; set; }

    public DateTime? EndedUtc { get; set; }

    /// <summary>What was down, e.g. "LIS upgrade" or "Network outage, EHR and LIS".</summary>
    public string Description { get; set; } = string.Empty;

    public string? EndedBy { get; set; }
}
