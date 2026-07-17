using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Immunohematology;

public sealed record RecordBloodTypeRequest(AboGroup Abo, RhType RhD, string Reason);

public sealed record AddAntibodyRequest(
    long? BloodAttributeDefinitionId,
    string? AntibodySpecificity,
    AntibodyStatus Status,
    string? Comment = null);

public sealed record SaveAntigenProfileRequest(
    long BloodAttributeDefinitionId,
    AntigenResult Result,
    string? Method = null);

public sealed record BloodTypeDto(
    long Id,
    long PatientId,
    AboGroup Abo,
    RhType RhD,
    string BloodType,
    BloodTypeSource Source,
    long? SourceResultId,
    bool IsCurrent,
    string? Reason,
    DateTime RecordedUtc,
    string RecordedBy)
{
    public static BloodTypeDto From(PatientBloodTypeHistory h) => new(
        h.Id, h.PatientId, h.Abo, h.RhD, h.BloodType.ToString(), h.Source, h.SourceResultId,
        h.IsCurrent, h.Reason, h.CreatedUtc, h.CreatedBy);
}

public sealed record AntibodyDto(
    long Id,
    long PatientId,
    long? BloodAttributeDefinitionId,
    string AntibodySpecificity,
    AntibodyStatus Status,
    bool IsActive,
    string? Comment,
    string? DeactivationReason,
    DateTime RecordedUtc,
    string RecordedBy)
{
    public static AntibodyDto From(AntibodyHistory a) => new(
        a.Id, a.PatientId, a.BloodAttributeDefinitionId, a.AntibodySpecificity, a.Status, a.IsActive, a.Comment,
        a.DeactivationReason, a.CreatedUtc, a.CreatedBy);
}

public sealed record AntigenProfileDto(
    long Id,
    long PatientId,
    long BloodAttributeDefinitionId,
    string AntigenCode,
    string AntigenName,
    AntigenResult Result,
    string? Method,
    DateTime? TestedUtc,
    string? TestedBy,
    long? SourceResultId)
{
    public static AntigenProfileDto From(AntigenProfile p, string code, string name) => new(
        p.Id, p.PatientId, p.BloodAttributeDefinitionId, code, name, p.Result, p.Method,
        p.TestedUtc, p.TestedBy, p.SourceResultId);
}
