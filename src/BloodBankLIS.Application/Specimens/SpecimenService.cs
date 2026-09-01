using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Specimens;

/// <summary>
/// Specimen accessioning, metadata edit, and rejection. Expiration is computed at accessioning from
/// a policy window (defaulted here; intended to move to SystemConfiguration) and is
/// enforced on the issue path in a later phase (see docs/workflows.md section 2).
/// </summary>
public sealed class SpecimenService
{
    /// <summary>Default specimen validity window when no facility policy is registered.</summary>
    public const int DefaultValidityHours = SpecimenValidityPolicy.DefaultStandardHours;

    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<SpecimenTypeDefinition> _specimenTypes;
    private readonly IRepository<TransfusionEvent>? _transfusions;
    private readonly FacilityPolicyService? _policy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditWriter? _audit;

    public SpecimenService(
        IRepository<Specimen> specimens,
        IRepository<Patient> patients,
        IRepository<SpecimenTypeDefinition> specimenTypes,
        IUnitOfWork unitOfWork,
        IClock clock,
        IRepository<TransfusionEvent>? transfusions = null,
        FacilityPolicyService? policy = null,
        IAuditWriter? audit = null)
    {
        _specimens = specimens;
        _patients = patients;
        _specimenTypes = specimenTypes;
        _transfusions = transfusions;
        _policy = policy;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
    }

    public async Task<SpecimenDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var specimen = await _specimens.GetByIdAsync(id, ct);
        return specimen is null ? null : await MapAsync(specimen, ct);
    }

    public async Task<IReadOnlyList<SpecimenDto>> GetByPatientAsync(long patientId, CancellationToken ct = default)
    {
        var specimens = await _specimens.ListAsync(s => s.PatientId == patientId, ct);
        var descriptions = await LoadActiveDescriptionMapAsync(ct);
        return specimens
            .OrderByDescending(s => s.CollectedUtc)
            .Select(s => SpecimenDto.From(s, ResolveDescription(s.SpecimenType, descriptions)))
            .ToList();
    }

    public async Task<OperationResult<Specimen>> AccessionAsync(AccessionSpecimenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.AccessionNumber))
        {
            return OperationResult<Specimen>.Fail("Accession number is required.");
        }

        var typeCode = request.SpecimenType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(typeCode))
        {
            return OperationResult<Specimen>.Fail("Specimen type is required.");
        }

        var typeDef = await _specimenTypes.FirstOrDefaultAsync(
            t => t.IsActive && !t.IsDraft && t.Code == typeCode, ct);
        if (typeDef is null)
        {
            return OperationResult<Specimen>.Fail($"Specimen type '{typeCode}' is not in the active catalog.");
        }

        if (request.CollectedUtc > _clock.UtcNow)
        {
            return OperationResult<Specimen>.Fail("Collection date/time cannot be in the future.");
        }

        if (await _patients.GetByIdAsync(request.PatientId, ct) is not { } patient)
        {
            return OperationResult<Specimen>.Fail("Patient not found.");
        }

        if (await _specimens.AnyAsync(s => s.AccessionNumber == request.AccessionNumber, ct))
        {
            return OperationResult<Specimen>.Fail($"Accession number '{request.AccessionNumber}' already exists.");
        }

        var id1Type = request.Identifier1Type ?? IdentityTokenType.MedicalRecordNumber;
        var id1Value = string.IsNullOrWhiteSpace(request.Identifier1Value) ? patient.MedicalRecordNumber : request.Identifier1Value;
        var id2Type = request.Identifier2Type ?? IdentityTokenType.DateOfBirth;
        var id2Value = string.IsNullOrWhiteSpace(request.Identifier2Value)
            ? patient.DateOfBirth.ToString("yyyy-MM-dd")
            : request.Identifier2Value;

        var identity = PatientIdentityMatchRule.Evaluate(
            patient.MedicalRecordNumber,
            patient.DateOfBirth,
            patient.LastName,
            patient.FirstName,
            new PatientIdentityMatchRule.IdentityToken(id1Type, id1Value),
            new PatientIdentityMatchRule.IdentityToken(id2Type, id2Value));
        if (identity.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<Specimen>.Fail(identity.Message);
        }

        var validityHours = request.ValidityHours ?? await ResolveValidityHoursForPatientAsync(patient, ct);
        var specimen = new Specimen
        {
            AccessionNumber = request.AccessionNumber,
            PatientId = request.PatientId,
            SpecimenType = typeCode,
            Barcode = request.Barcode,
            CollectedUtc = request.CollectedUtc,
            ReceivedUtc = _clock.UtcNow,
            ExpiresUtc = request.CollectedUtc.AddHours(validityHours),
            DrawLocation = request.DrawLocation,
            Collector = request.Collector,
            Identifier1Type = id1Type,
            Identifier1Value = id1Value,
            Identifier2Type = id2Type,
            Identifier2Value = id2Value,
            Status = SpecimenStatus.Accepted
        };

        await _specimens.AddAsync(specimen, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Specimen>.Ok(specimen);
    }

    /// <summary>
    /// Updates collection metadata on an accepted specimen. Accession number, type,
    /// patient, identity tokens, and status are immutable here.
    /// </summary>
    public async Task<OperationResult<Specimen>> UpdateAsync(long specimenId, UpdateSpecimenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        if (specimen is null)
        {
            return OperationResult<Specimen>.Fail("Specimen not found.");
        }

        if (specimen.Status is not SpecimenStatus.Accepted)
        {
            return OperationResult<Specimen>.Fail($"A specimen with status {specimen.Status} cannot be edited.");
        }

        if (request.CollectedUtc > _clock.UtcNow)
        {
            return OperationResult<Specimen>.Fail("Collection date/time cannot be in the future.");
        }

        var hours = request.ValidityHours
            ?? (specimen.ExpiresUtc.HasValue
                ? (int)Math.Round((specimen.ExpiresUtc.Value - specimen.CollectedUtc).TotalHours)
                : await ResolveValidityHoursForSpecimenAsync(specimen, ct));

        var previous = new
        {
            specimen.CollectedUtc,
            specimen.Barcode,
            specimen.DrawLocation,
            specimen.Collector,
            specimen.ExpiresUtc
        };

        specimen.CollectedUtc = request.CollectedUtc;
        specimen.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        specimen.DrawLocation = string.IsNullOrWhiteSpace(request.DrawLocation) ? null : request.DrawLocation.Trim();
        specimen.Collector = string.IsNullOrWhiteSpace(request.Collector) ? null : request.Collector.Trim();
        specimen.ExpiresUtc = request.CollectedUtc.AddHours(hours);

        _specimens.Update(specimen);
        _audit?.Record(
            AuditEventType.Update,
            nameof(Specimen),
            specimen.Id,
            oldValue: previous,
            newValue: new
            {
                specimen.CollectedUtc,
                specimen.Barcode,
                specimen.DrawLocation,
                specimen.Collector,
                specimen.ExpiresUtc
            },
            reason: "Specimen metadata updated.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Specimen>.Ok(specimen);
    }

    public async Task<OperationResult<Specimen>> RejectAsync(long specimenId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<Specimen>.Fail("A reason is required to reject a specimen.");
        }

        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        if (specimen is null)
        {
            return OperationResult<Specimen>.Fail("Specimen not found.");
        }

        if (specimen.Status is SpecimenStatus.Rejected or SpecimenStatus.Cancelled or SpecimenStatus.Expired)
        {
            return OperationResult<Specimen>.Fail($"A specimen with status {specimen.Status} cannot be rejected.");
        }

        specimen.Status = SpecimenStatus.Rejected;
        specimen.RejectionReason = reason;
        _specimens.Update(specimen);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Specimen>.Ok(specimen);
    }

    /// <summary>
    /// Recomputes expiry for accepted specimens when alloimmunization risk changes
    /// (recent transfusion or documented pregnancy).
    /// </summary>
    public async Task RecomputeValidityForPatientAsync(long patientId, CancellationToken ct = default)
    {
        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return;
        }

        var hours = await ResolveValidityHoursForPatientAsync(patient, ct);
        var specimens = await _specimens.ListAsync(
            s => s.PatientId == patientId && s.Status == SpecimenStatus.Accepted, ct);
        foreach (var specimen in specimens)
        {
            var next = specimen.CollectedUtc.AddHours(hours);
            if (specimen.ExpiresUtc == next)
            {
                continue;
            }

            var previous = specimen.ExpiresUtc;
            specimen.ExpiresUtc = next;
            _audit?.Record(
                AuditEventType.Update,
                nameof(Specimen),
                specimen.Id,
                oldValue: new { ExpiresUtc = previous },
                newValue: new { ExpiresUtc = next },
                reason: "Specimen validity recomputed after alloimmunization-risk change.");
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<int> ResolveValidityHoursForSpecimenAsync(Specimen specimen, CancellationToken ct)
    {
        var patient = await _patients.GetByIdAsync(specimen.PatientId, ct);
        return patient is null
            ? DefaultValidityHours
            : await ResolveValidityHoursForPatientAsync(patient, ct);
    }

    private async Task<int> ResolveValidityHoursForPatientAsync(Patient patient, CancellationToken ct)
    {
        var alloHours = _policy is null
            ? SpecimenValidityPolicy.DefaultAlloimmunizationRiskHours
            : await _policy.GetSpecimenAlloHoursAsync(ct);
        var standardHours = _policy is null
            ? SpecimenValidityPolicy.DefaultStandardHours
            : await _policy.GetSpecimenStandardHoursAsync(ct);
        var lookbackDays = _policy is null
            ? SpecimenValidityPolicy.DefaultLookbackDays
            : await _policy.GetSpecimenLookbackDaysAsync(ct);

        DateTime? lastTransfusion = null;
        if (_transfusions is not null)
        {
            var events = await _transfusions.ListAsync(t => t.PatientId == patient.Id, ct);
            if (events.Count > 0)
            {
                lastTransfusion = events.Max(t => t.StartUtc ?? t.CreatedUtc);
            }
        }

        var risk = SpecimenValidityPolicy.HasAlloimmunizationRisk(
            _clock.UtcNow, lastTransfusion, patient.RecentPregnancyUtc, lookbackDays);
        return risk ? alloHours : standardHours;
    }

    private async Task<SpecimenDto> MapAsync(Specimen specimen, CancellationToken ct)
    {
        var descriptions = await LoadActiveDescriptionMapAsync(ct);
        return SpecimenDto.From(specimen, ResolveDescription(specimen.SpecimenType, descriptions));
    }

    private async Task<Dictionary<string, string>> LoadActiveDescriptionMapAsync(CancellationToken ct)
    {
        var types = await _specimenTypes.ListAsync(t => t.IsActive && !t.IsDraft, ct);
        return types.ToDictionary(t => t.Code, t => t.Description, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveDescription(string typeCode, IReadOnlyDictionary<string, string> descriptions) =>
        descriptions.TryGetValue(typeCode, out var description) ? description : null;
}
