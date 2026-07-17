using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Specimens;

/// <summary>
/// Specimen accessioning and rejection. Expiration is computed at accessioning from
/// a policy window (defaulted here; intended to move to SystemConfiguration) and is
/// enforced on the issue path in a later phase (see docs/workflows.md section 2).
/// </summary>
public sealed class SpecimenService
{
    /// <summary>Default specimen validity window. Policy-driven; placeholder for SystemConfiguration.</summary>
    public const int DefaultValidityHours = 72;

    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<SpecimenTypeDefinition> _specimenTypes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SpecimenService(
        IRepository<Specimen> specimens,
        IRepository<Patient> patients,
        IRepository<SpecimenTypeDefinition> specimenTypes,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _specimens = specimens;
        _patients = patients;
        _specimenTypes = specimenTypes;
        _unitOfWork = unitOfWork;
        _clock = clock;
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

        if (await _patients.GetByIdAsync(request.PatientId, ct) is null)
        {
            return OperationResult<Specimen>.Fail("Patient not found.");
        }

        if (await _specimens.AnyAsync(s => s.AccessionNumber == request.AccessionNumber, ct))
        {
            return OperationResult<Specimen>.Fail($"Accession number '{request.AccessionNumber}' already exists.");
        }

        var validityHours = request.ValidityHours ?? DefaultValidityHours;
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
            Status = SpecimenStatus.Accepted
        };

        await _specimens.AddAsync(specimen, ct);
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
