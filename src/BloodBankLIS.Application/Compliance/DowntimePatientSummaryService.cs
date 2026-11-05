using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record DowntimeUnitLine(
    string UnitNumber,
    string Product,
    string AboRh,
    DateTime UnitExpiresUtc,
    // Reserved for the patient, or issued and not yet returned or transfused.
    bool Issued,
    DateTime? ReservationEndsUtc = null,
    string? IssuedToLocation = null);

public sealed record DowntimeSpecimenLine(string AccessionNumber, DateTime CollectedUtc, DateTime? ExpiresUtc);

public sealed record DowntimePatientRow(
    long PatientId,
    string MedicalRecordNumber,
    string Name,
    DateOnly DateOfBirth,
    // "O+", or "Not on file" when no current type is recorded.
    string AboRh,
    // Two concordant determinations on file (the same check issue uses).
    bool SecondDeterminationOnFile,
    IReadOnlyList<string> Antibodies,
    IReadOnlyList<string> SpecialRequirements,
    IReadOnlyList<string> Informational,
    IReadOnlyList<DowntimeSpecimenLine> Specimens,
    IReadOnlyList<DowntimeUnitLine> Units);

public sealed record DowntimePatientSummary(DateTime GeneratedUtc, IReadOnlyList<DowntimePatientRow> Patients);

/// <summary>
/// The paper copy for an outage: every patient the blood bank is working for now (a specimen not yet expired, a unit
/// reserved, or a unit issued and still out) with what the bench needs to issue safely without the system. Read only;
/// the print is audited because the sheet carries patient data off the system.
/// </summary>
public sealed class DowntimePatientSummaryService
{
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<SpecialTransfusionRequirement> _requirements;
    private readonly IRepository<SpecialRequirementDefinition> _requirementDefinitions;
    private readonly IAuditWriter _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DowntimePatientSummaryService(
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        IRepository<Allocation> allocations,
        IRepository<Issue> issues,
        IRepository<BloodUnit> units,
        IRepository<ProductType> productTypes,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IRepository<SpecialTransfusionRequirement> requirements,
        IRepository<SpecialRequirementDefinition> requirementDefinitions,
        IAuditWriter audit,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _patients = patients;
        _specimens = specimens;
        _allocations = allocations;
        _issues = issues;
        _units = units;
        _productTypes = productTypes;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _requirements = requirements;
        _requirementDefinitions = requirementDefinitions;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<DowntimePatientSummary> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var specimens = (await _specimens.ListAsync(
                s => s.Status != SpecimenStatus.Rejected
                    && s.Status != SpecimenStatus.Expired
                    && s.Status != SpecimenStatus.Cancelled,
                ct))
            .Where(s => s.ExpiresUtc is null || s.ExpiresUtc > now)
            .ToList();
        var reserved = await _allocations.ListAsync(a => a.Status == AllocationStatus.Reserved, ct);
        var issued = await _issues.ListAsync(i => i.Status == IssueStatus.Issued, ct);

        var patientIds = specimens.Select(s => s.PatientId)
            .Concat(reserved.Select(a => a.PatientId))
            .Concat(issued.Select(i => i.PatientId))
            .ToHashSet();
        if (patientIds.Count == 0)
        {
            return new DowntimePatientSummary(now, []);
        }

        var patients = (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct))
            .Where(p => p.MergedIntoPatientId is null)
            .ToList();
        var unitIds = reserved.Select(a => a.BloodProductId).Concat(issued.Select(i => i.BloodProductId)).ToHashSet();
        var units = (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id);
        var productNames = (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Name);
        var types = (await _bloodTypes.ListAsync(t => patientIds.Contains(t.PatientId), ct)).ToLookup(t => t.PatientId);
        var antibodies = (await _antibodies.ListAsync(a => patientIds.Contains(a.PatientId) && a.IsActive, ct))
            .ToLookup(a => a.PatientId);
        var requirements = (await _requirements.ListAsync(r => patientIds.Contains(r.PatientId), ct))
            .ToLookup(r => r.PatientId);
        var definitions = (await _requirementDefinitions.ListAsync(ct)).ToDictionary(d => d.Id);

        DowntimeUnitLine? UnitLine(long unitId, bool isIssued, DateTime? reservationEnds, string? location) =>
            units.TryGetValue(unitId, out var u)
                ? new DowntimeUnitLine(
                    u.UnitNumber,
                    productNames.GetValueOrDefault(u.ProductTypeId) ?? "Unknown product",
                    AboRhText(u.Abo, u.RhD),
                    u.ExpiresUtc,
                    isIssued,
                    reservationEnds,
                    location)
                : null;

        var rows = patients.Select(p =>
            {
                var history = types[p.Id].ToList();
                var current = history.FirstOrDefault(t => t.IsCurrent && t.InvalidatedUtc is null);
                var active = requirements[p.Id]
                    .Select(r => SpecialRequirementDto.From(
                        r, r.RequirementDefinitionId is { } id ? definitions.GetValueOrDefault(id) : null, now))
                    .Where(r => r.IsClinicallyActive)
                    .ToList();
                var unitLines = reserved.Where(a => a.PatientId == p.Id)
                    .Select(a => UnitLine(a.BloodProductId, false, a.ExpiresUtc, null))
                    .Concat(issued.Where(i => i.PatientId == p.Id)
                        .Select(i => UnitLine(i.BloodProductId, true, null, i.IssuedToLocation)))
                    .OfType<DowntimeUnitLine>()
                    .OrderBy(l => l.Issued)
                    .ThenBy(l => l.UnitNumber, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return new DowntimePatientRow(
                    p.Id,
                    p.MedicalRecordNumber,
                    $"{p.LastName}, {p.FirstName}",
                    p.DateOfBirth,
                    current is null ? "Not on file" : AboRhText(current.Abo, current.RhD),
                    SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(history)),
                    antibodies[p.Id].Select(a => a.AntibodySpecificity)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    active.Where(r => !r.IsInformational).Select(RequirementText).Order(StringComparer.OrdinalIgnoreCase).ToList(),
                    active.Where(r => r.IsInformational)
                        .Select(r => string.IsNullOrWhiteSpace(r.Instruction) ? r.Name ?? string.Empty : r.Instruction!)
                        .ToList(),
                    specimens.Where(s => s.PatientId == p.Id)
                        .OrderByDescending(s => s.CollectedUtc)
                        .Select(s => new DowntimeSpecimenLine(s.AccessionNumber, s.CollectedUtc, s.ExpiresUtc))
                        .ToList(),
                    unitLines);
            })
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.MedicalRecordNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DowntimePatientSummary(now, rows);
    }

    /// <summary>Records who printed the sheet and which patients it carried, before the browser prints it.</summary>
    public async Task<int> RecordPrintedAsync(CancellationToken ct = default)
    {
        var summary = await GetAsync(ct);
        _audit.Record(
            AuditEventType.Print,
            "DowntimePatientSummary",
            null,
            newValue: new
            {
                Document = "Downtime patient summary",
                Patients = summary.Patients.Count,
                PatientIds = summary.Patients.Select(p => p.PatientId).ToList()
            },
            reason: "Downtime patient summary printed.");
        await _unitOfWork.SaveChangesAsync(ct);
        return summary.Patients.Count;
    }

    private static string RequirementText(SpecialRequirementDto r) =>
        string.IsNullOrWhiteSpace(r.AntigenCode) ? r.Name ?? r.RequirementType.ToString() : $"{r.Name} ({r.AntigenCode})";

    private static string AboRhText(AboGroup abo, RhType rh) =>
        abo == AboGroup.Unknown && rh == RhType.Unknown ? "Not on file" : new Domain.ValueObjects.AboRh(abo, rh).ToString();
}
