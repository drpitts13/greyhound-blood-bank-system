using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One chart row touched by a patient merge. Unmerge restores reassigned rows
/// and ignores rows that were left on the duplicate as duplicates.
/// </summary>
public class PatientMergeMove : BaseEntity
{
    public long PatientMergeId { get; set; }

    public PatientMerge? PatientMerge { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public PatientMergeMoveAction Action { get; set; }
}
