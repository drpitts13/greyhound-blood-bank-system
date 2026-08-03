using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A reservation of a unit for a specific patient. A unit may have at most one
/// active (<see cref="AllocationStatus.Reserved"/>) allocation at a time. The
/// issue gate requires the unit to be allocated to the receiving patient.
/// </summary>
public class Allocation : BaseEntity
{
    public long BloodProductId { get; set; }

    public BloodUnit? Unit { get; set; }

    public long PatientId { get; set; }

    public long? EncounterId { get; set; }

    public long? OrderId { get; set; }

    public long? SpecimenId { get; set; }

    public AllocationStatus Status { get; set; } = AllocationStatus.Reserved;

    /// <summary>Patient-assignment pathway; not a generic linked flag.</summary>
    public AssignmentType AssignmentType { get; set; } = AssignmentType.Reservation;

    public DateTime AllocatedUtc { get; set; }

    public string AllocatedBy { get; set; } = "system";

    public DateTime? ExpiresUtc { get; set; }

    public string? ReleaseReason { get; set; }
}
