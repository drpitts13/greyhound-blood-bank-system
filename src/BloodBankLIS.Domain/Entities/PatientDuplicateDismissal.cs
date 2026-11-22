using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A reviewer's decision that two charts on the Possible duplicates report are different people. The pair stays off
/// the report while the reasons it is listed for are the ones reviewed; new evidence (for example an identifier the two
/// charts come to share) brings it back.
/// </summary>
public class PatientDuplicateDismissal : BaseEntity
{
    /// <summary>The lower patient id of the pair.</summary>
    public long FirstPatientId { get; set; }

    /// <summary>The higher patient id of the pair.</summary>
    public long SecondPatientId { get; set; }

    /// <summary>Why the pair was listed when it was reviewed ("Same name and date of birth; …").</summary>
    public string ReviewedReasons { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string DismissedBy { get; set; } = string.Empty;

    public DateTime DismissedUtc { get; set; }
}
