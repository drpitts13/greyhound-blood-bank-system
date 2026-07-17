using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A patient visit/encounter. Orders must belong to an encounter. Hospital visit
/// number is a preserved source identifier and is unique within the facility.
/// </summary>
public class Encounter : BaseEntity
{
    public long PatientId { get; set; }

    public Patient? Patient { get; set; }

    public string VisitNumber { get; set; } = string.Empty;

    public string? AccountNumber { get; set; }

    public EncounterType EncounterType { get; set; } = EncounterType.Unknown;

    public EncounterStatus Status { get; set; } = EncounterStatus.Active;

    public DateTime? AdmitUtc { get; set; }

    public DateTime? DischargeUtc { get; set; }

    public long? AttendingProviderId { get; set; }

    public OrderingProvider? AttendingProviderRef { get; set; }

    /// <summary>Display name at time of visit (denormalized from provider or HL7 text).</summary>
    public string? AttendingProvider { get; set; }

    public string? AdmissionLocation { get; set; }

    public string? CurrentLocation { get; set; }

    public string? DischargeDisposition { get; set; }

    public string? FinancialClass { get; set; }

    public string? SourceSystem { get; set; }

    public string? ExternalVisitId { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
