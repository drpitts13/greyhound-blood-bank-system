using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Patient demographics. The medical record number (MRN) is a preserved source
/// identifier and is unique. Immunohematology history (ABO/Rh, antibodies) lives
/// in dedicated append-only tables, not here (added in a later phase).
/// </summary>
public class Patient : BaseEntity
{
    public string MedicalRecordNumber { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public Sex Sex { get; set; } = Sex.Unknown;

    public bool Deceased { get; set; }

    public DateTime? DeceasedUtc { get; set; }

    public PatientStatus Status { get; set; } = PatientStatus.Active;

    /// <summary>When a duplicate is merged, the surviving patient is referenced here. Merges never delete.</summary>
    public long? MergedIntoPatientId { get; set; }

    public ICollection<Specimen> Specimens { get; set; } = new List<Specimen>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();
}
