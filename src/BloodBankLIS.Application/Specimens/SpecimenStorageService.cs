using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Specimens;

/// <summary>A tube scanned (accession or label barcode) into a storage place, with an optional note.</summary>
public sealed record StoreSpecimenRequest(string Scan, string Location, string? Note = null);

public sealed record StoredSpecimenDto(
    long SpecimenId,
    string AccessionNumber,
    long PatientId,
    string PatientName,
    string Mrn,
    string Location,
    string? PreviousLocation,
    DateTime StoredUtc);

/// <summary>
/// Puts tubes away by scan (specimen.accession). The tube's current place is on the specimen; every store or move is
/// also kept as a <see cref="SpecimenStorageEvent"/> and audited, so the earlier place is never lost.
/// </summary>
public sealed class SpecimenStorageService
{
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<SpecimenStorageEvent> _events;
    private readonly IRepository<Patient> _patients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public SpecimenStorageService(
        IRepository<Specimen> specimens,
        IRepository<SpecimenStorageEvent> events,
        IRepository<Patient> patients,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _specimens = specimens;
        _events = events;
        _patients = patients;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<OperationResult<StoredSpecimenDto>> StoreAsync(StoreSpecimenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_permissions is not null
            && !await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.SpecimenAccession, ct))
        {
            return OperationResult<StoredSpecimenDto>.Fail("Storing tubes requires specimen accession permission.");
        }

        var scan = ScannerInputSanitizer.Sanitize(request.Scan).Sanitized.Trim().ToUpperInvariant();
        if (scan.Length == 0)
        {
            return OperationResult<StoredSpecimenDto>.Fail("Scan the tube's accession or label barcode.");
        }

        var matches = await _specimens.ListAsync(
            s => s.AccessionNumber.ToUpper() == scan || (s.Barcode != null && s.Barcode.ToUpper() == scan), ct);
        if (matches.Count != 1)
        {
            return OperationResult<StoredSpecimenDto>.Fail(matches.Count == 0
                ? $"No tube has accession or barcode {scan}."
                : $"More than one tube matches {scan}; scan the accession number.");
        }

        // The match came from a read-only query; change the tracked row.
        var specimen = await _specimens.GetByIdAsync(matches[0].Id, ct);
        if (specimen is null)
        {
            return OperationResult<StoredSpecimenDto>.Fail($"No tube has accession or barcode {scan}.");
        }

        var location = request.Location?.Trim() ?? string.Empty;
        var check = SpecimenStorageRule.Evaluate(specimen.Status, specimen.StorageLocation, location);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<StoredSpecimenDto>.Fail(check.Message);
        }

        var previous = specimen.StorageLocation;
        var now = _clock.UtcNow;
        specimen.StorageLocation = location;
        specimen.StoredUtc = now;
        specimen.StoredBy = _currentUser.UserName;
        _specimens.Update(specimen);
        await _events.AddAsync(new SpecimenStorageEvent
        {
            SpecimenId = specimen.Id,
            Location = location,
            PreviousLocation = previous,
            StoredUtc = now,
            StoredBy = _currentUser.UserName,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        }, ct);
        _audit.Record(
            AuditEventType.Update,
            nameof(Specimen),
            specimen.Id,
            oldValue: new { StorageLocation = previous },
            newValue: new { StorageLocation = location, specimen.AccessionNumber, specimen.PatientId },
            reason: previous is null ? $"Tube stored in {location}." : $"Tube moved from {previous} to {location}.");
        await _unitOfWork.SaveChangesAsync(ct);

        var patient = await _patients.GetByIdAsync(specimen.PatientId, ct);
        return OperationResult<StoredSpecimenDto>.Ok(new StoredSpecimenDto(
            specimen.Id,
            specimen.AccessionNumber,
            specimen.PatientId,
            patient is null ? $"Patient {specimen.PatientId}" : $"{patient.LastName}, {patient.FirstName}",
            patient?.MedicalRecordNumber ?? "—",
            location,
            previous,
            now));
    }
}
