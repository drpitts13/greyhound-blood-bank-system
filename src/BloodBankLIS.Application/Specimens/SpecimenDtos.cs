using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Specimens;

/// <summary>The tube's scanned accession number or label barcode.</summary>
public sealed record ReceiveSpecimenScanRequest(string? Scan);

public sealed record AccessionSpecimenRequest(
    string AccessionNumber,
    long PatientId,
    string SpecimenType,
    DateTime CollectedUtc,
    string? Barcode = null,
    string? DrawLocation = null,
    string? Collector = null,
    int? ValidityHours = null,
    IdentityTokenType? Identifier1Type = null,
    string? Identifier1Value = null,
    IdentityTokenType? Identifier2Type = null,
    string? Identifier2Value = null,
    string? Comment = null,
    bool AcceptImmediately = true);

public sealed record UpdateSpecimenRequest(
    DateTime CollectedUtc,
    string? Barcode = null,
    string? DrawLocation = null,
    string? Collector = null,
    int? ValidityHours = null,
    string? Comment = null,
    DateTime? ExpiresUtc = null,
    string? OverrideReason = null,
    string? AuthorizedBy = null);

public sealed record RelabelSpecimenRequest(string Barcode, string Reason);

/// <summary>
/// The expiration the facility policy gives a specimen of this type collected at this time for this patient,
/// including the shorter window for a patient at alloimmunization risk. Saving a different expiration needs an override.
/// </summary>
public sealed record SpecimenExpirationPreviewDto(DateTime PolicyExpiresUtc, bool ShortenedForAlloimmunizationRisk);

public sealed record SpecimenDto(
    long Id,
    string AccessionNumber,
    long PatientId,
    string SpecimenType,
    string? SpecimenTypeDescription,
    string? Barcode,
    string? DrawLocation,
    string? Collector,
    DateTime CollectedUtc,
    DateTime? ReceivedUtc,
    DateTime? ExpiresUtc,
    SpecimenStatus Status,
    string? RejectionReason,
    string? Comment = null,
    bool HasCompletedTypeAndScreen = false)
{
    public static SpecimenDto From(Specimen s, string? typeDescription = null, bool hasCompletedTypeAndScreen = false) => new(
        s.Id, s.AccessionNumber, s.PatientId, s.SpecimenType, typeDescription, s.Barcode,
        s.DrawLocation, s.Collector, s.CollectedUtc, s.ReceivedUtc, s.ExpiresUtc, s.Status, s.RejectionReason,
        s.Comment, hasCompletedTypeAndScreen);
}

public sealed record PendingSpecimenWorkItemDto(
    long SpecimenId,
    long PatientId,
    string PatientName,
    string Mrn,
    string AccessionNumber,
    string SpecimenType,
    DateTime CollectedUtc,
    DateTime? ReceivedUtc,
    DateTime? ExpiresUtc,
    SpecimenStatus Status,
    bool IsExpired,
    // "received 42 min after draw" or "waiting 1 h 05 min since draw".
    string? TransitNote = null)
{
    public static PendingSpecimenWorkItemDto From(Specimen s, Patient? patient, DateTime now) => new(
        s.Id,
        s.PatientId,
        patient is null ? "—" : $"{patient.LastName}, {patient.FirstName}",
        patient?.MedicalRecordNumber ?? "—",
        s.AccessionNumber,
        s.SpecimenType,
        s.CollectedUtc,
        s.ReceivedUtc,
        s.ExpiresUtc,
        s.Status,
        s.ExpiresUtc is DateTime expires && expires <= now,
        SpecimenTransitRule.Describe(s.CollectedUtc, s.ReceivedUtc, now));
}
