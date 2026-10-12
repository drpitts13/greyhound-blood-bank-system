using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Ledger for one patient merge. Records which chart rows moved so a later
/// unmerge can put them back without guessing. History is not deleted.
/// </summary>
public class PatientMerge : BaseEntity
{
    public long SurvivorPatientId { get; set; }

    public long DuplicatePatientId { get; set; }

    public PatientMergeStatus Status { get; set; } = PatientMergeStatus.Active;

    public string Reason { get; set; } = string.Empty;

    public string MergedBy { get; set; } = string.Empty;

    public DateTime MergedUtc { get; set; }

    public string? UnmergeReason { get; set; }

    public string? UnmergedBy { get; set; }

    public DateTime? UnmergedUtc { get; set; }

    public bool SurvivorDeceasedBefore { get; set; }

    public DateTime? SurvivorDeceasedUtcBefore { get; set; }

    public DateTime? SurvivorRecentPregnancyUtcBefore { get; set; }

    public bool SurvivorDeceasedAfter { get; set; }

    public DateTime? SurvivorDeceasedUtcAfter { get; set; }

    public DateTime? SurvivorRecentPregnancyUtcAfter { get; set; }

    /// <summary>Prior-MRN alias created on the survivor for this merge. Null when that alias already existed.</summary>
    public long? PriorMrnIdentifierId { get; set; }

    public ICollection<PatientMergeMove> Moves { get; set; } = new List<PatientMergeMove>();
}
