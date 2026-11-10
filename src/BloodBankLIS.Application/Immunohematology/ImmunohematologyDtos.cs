using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Immunohematology;

public sealed record RecordBloodTypeRequest(
    AboGroup Abo,
    RhType RhD,
    string Reason,
    AboSubgroup AboSubgroup = AboSubgroup.Unknown,
    ManualTypeDocumentation Documentation = ManualTypeDocumentation.NotDocumented,
    string? DocumentationReference = null);

public sealed record AddAntibodyRequest(
    long? BloodAttributeDefinitionId,
    string? AntibodySpecificity,
    AntibodyStatus Status,
    string? Comment = null,
    // Reported by another facility (antibody card, transfer letter): who, and when they identified it.
    string? ReportingFacility = null,
    DateOnly? IdentifiedOn = null,
    // OCD-073: how the outside report is documented; both required with a reporting facility.
    OutsideReportKind? ReportKind = null,
    string? ReportDocumentation = null);

/// <summary>OCD-073: the testing here that confirmed an outside report.</summary>
public sealed record ConfirmOutsideAntibodyRequest(string? Note);

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
    string RecordedBy,
    AboSubgroup AboSubgroup = AboSubgroup.Unknown,
    bool Invalidated = false,
    // Plain source wording, including a manual entry's documented source (OCD-048).
    string? SourceLabel = null,
    // Whether this row can be one of the two ABO/Rh determinations (not retired, not an undocumented manual entry).
    bool CountsAsDetermination = true,
    // The specimen the type was tested on: two types from one specimen are one determination.
    long? SpecimenId = null)
{
    public static BloodTypeDto From(PatientBloodTypeHistory h) => new(
        h.Id, h.PatientId, h.Abo, h.RhD, h.BloodType.ToString(), h.Source, h.SourceResultId,
        h.IsCurrent, h.Reason, h.CreatedUtc, h.CreatedBy, h.AboSubgroup, h.InvalidatedUtc is not null,
        BloodTypeHistoryLabel.Source(h.Source, h.ManualDocumentation, h.ManualDocumentationReference),
        BloodTypeHistoryLabel.CountsAsDetermination(h.Source, h.ManualDocumentation, h.InvalidatedUtc is not null),
        h.SpecimenId);

    /// <summary>
    /// The same determinations the server uses (<see cref="SecondAboDeterminationRule.FromHistory"/>), from the rows a
    /// screen already has, so the chart cannot say a second ABO/Rh is on file when issue would refuse it.
    /// </summary>
    public static IReadOnlyList<SecondAboDeterminationRule.Determination> Determinations(IEnumerable<BloodTypeDto> rows) =>
        rows.Where(r => !r.Invalidated)
            .Select(r => new SecondAboDeterminationRule.Determination(
                new AboRh(r.Abo, r.RhD), r.IsCurrent, r.SpecimenId, r.CountsAsDetermination))
            .ToList();

    /// <summary>The same input the server gives the ABO/Rh discrepancy check (<see cref="IssueGate.DiscrepancyInput"/>).</summary>
    public static IReadOnlyList<(AboRh Type, bool IsCurrent)> DiscrepancyInput(IEnumerable<BloodTypeDto> rows) =>
        rows.Where(r => !r.Invalidated).Select(r => (new AboRh(r.Abo, r.RhD), r.IsCurrent)).ToList();
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
    string RecordedBy,
    bool PostedByCompletedWorkup = false,
    string? ReportingFacility = null,
    DateOnly? IdentifiedOn = null,
    OutsideReportKind? ReportKind = null,
    string? ReportDocumentation = null,
    DateTime? ConfirmedUtc = null,
    string? ConfirmedBy = null,
    string? ConfirmationNote = null)
{
    /// <summary>"Reported by …, identified …" for an outside report; null when found here.</summary>
    public string? SourceLabel => OutsideAntibodyReportRule.SourceLabel(ReportingFacility, IdentifiedOn);

    /// <summary>OCD-073: an active outside report this laboratory has not yet confirmed.</summary>
    public bool NeedsConfirmation => OutsideAntibodyReportRule.NeedsConfirmation(ReportingFacility, ConfirmedUtc, IsActive);

    public static AntibodyDto From(AntibodyHistory a, bool postedByCompletedWorkup = false) => new(
        a.Id, a.PatientId, a.BloodAttributeDefinitionId, a.AntibodySpecificity, a.Status, a.IsActive, a.Comment,
        a.DeactivationReason, a.CreatedUtc, a.CreatedBy, postedByCompletedWorkup, a.ReportingFacility, a.IdentifiedOn,
        a.ReportKind, a.ReportDocumentation, a.ConfirmedUtc, a.ConfirmedBy, a.ConfirmationNote);
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
