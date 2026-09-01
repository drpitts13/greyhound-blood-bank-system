using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Specimens;

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
    string? Identifier2Value = null);

public sealed record UpdateSpecimenRequest(
    DateTime CollectedUtc,
    string? Barcode = null,
    string? DrawLocation = null,
    string? Collector = null,
    int? ValidityHours = null);

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
    string? RejectionReason)
{
    public static SpecimenDto From(Specimen s, string? typeDescription = null) => new(
        s.Id, s.AccessionNumber, s.PatientId, s.SpecimenType, typeDescription, s.Barcode,
        s.DrawLocation, s.Collector, s.CollectedUtc, s.ReceivedUtc, s.ExpiresUtc, s.Status, s.RejectionReason);
}
