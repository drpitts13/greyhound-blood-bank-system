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

/// <summary>A tube recorded in a storage place now.</summary>
public sealed record TubeInPlaceDto(
    long SpecimenId,
    string AccessionNumber,
    string SpecimenType,
    SpecimenStatus Status,
    long PatientId,
    string PatientName,
    string Mrn,
    DateTime CollectedUtc,
    DateTime? StoredUtc,
    string? StoredBy);

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

    /// <summary>
    /// Tubes recorded in a place now (exact place, trimmed, case-insensitive), most recently stored first, so a rack can
    /// be checked against the system or a tube found in it.
    /// </summary>
    public async Task<IReadOnlyList<TubeInPlaceDto>> ListInPlaceAsync(string? location, CancellationToken ct = default)
    {
        if (SpecimenStorageRule.Key(location).Length == 0)
        {
            return [];
        }

        var tubes = (await _specimens.ListAsync(s => s.StorageLocation != null, ct))
            .Where(s => SpecimenStorageRule.SamePlace(s.StorageLocation, location))
            .ToList();
        if (tubes.Count == 0)
        {
            return [];
        }

        var ids = tubes.Select(t => t.PatientId).Distinct().ToList();
        var patients = (await _patients.ListAsync(p => ids.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        return tubes
            .OrderByDescending(t => t.StoredUtc)
            .ThenBy(t => t.AccessionNumber, StringComparer.OrdinalIgnoreCase)
            .Select(t =>
            {
                patients.TryGetValue(t.PatientId, out var patient);
                return new TubeInPlaceDto(
                    t.Id, t.AccessionNumber, t.SpecimenType, t.Status, t.PatientId,
                    patient is null ? $"Patient {t.PatientId}" : $"{patient.LastName}, {patient.FirstName}",
                    patient?.MedicalRecordNumber ?? "—", t.CollectedUtc, t.StoredUtc, t.StoredBy);
            })
            .ToList();
    }

    /// <summary>Places tubes have been stored in, most recently used first, one spelling each.</summary>
    public async Task<IReadOnlyList<string>> KnownPlacesAsync(CancellationToken ct = default)
    {
        var events = await _events.ListAsync(ct);
        return events
            .OrderByDescending(e => e.StoredUtc)
            .Select(e => e.Location.Trim())
            .Where(l => SpecimenStorageRule.Key(l).Length > 0)
            .DistinctBy(SpecimenStorageRule.Key)
            .ToList();
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

        // The same place typed or scanned another way ("rack3" for "Rack 3") is stored under the spelling already in use.
        var location = string.IsNullOrWhiteSpace(request.Location)
            ? string.Empty
            : SpecimenStorageRule.Canonical(request.Location, await KnownPlacesAsync(ct));
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
