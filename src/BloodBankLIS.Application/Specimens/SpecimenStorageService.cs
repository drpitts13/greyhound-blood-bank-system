using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
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
    string? StoredBy,
    // Where it is (filled on the due-for-discard list) and when it may be discarded (null when no retention period is set).
    string? Location = null,
    DateTime? KeepUntilUtc = null);

/// <summary>A tube scanned to be thrown away; a reason is needed before its retention date or when none is set.</summary>
public sealed record DiscardSpecimenRequest(string Scan, string? Reason = null);

public sealed record DiscardedSpecimenDto(
    long SpecimenId,
    string AccessionNumber,
    long PatientId,
    string PatientName,
    string Mrn,
    string? PreviousLocation,
    DateTime DiscardedUtc,
    // Discarded before its retention date (or with none set), with the reason.
    string? EarlyReason);

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
    private readonly IRepository<OrderSpecimen>? _orderSpecimens;
    private readonly IRepository<Order>? _orders;
    private readonly IRepository<ReactionInvestigation>? _investigations;
    private readonly FacilityPolicyService? _policy;

    public SpecimenStorageService(
        IRepository<Specimen> specimens,
        IRepository<SpecimenStorageEvent> events,
        IRepository<Patient> patients,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null,
        IRepository<OrderSpecimen>? orderSpecimens = null,
        IRepository<Order>? orders = null,
        IRepository<ReactionInvestigation>? investigations = null,
        FacilityPolicyService? policy = null)
    {
        _orderSpecimens = orderSpecimens;
        _orders = orders;
        _investigations = investigations;
        _policy = policy;
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

        var rows = await ToRowsAsync(tubes, await RetentionDaysAsync(ct), ct);
        return rows
            .OrderByDescending(t => t.StoredUtc)
            .ThenBy(t => t.AccessionNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Stored tubes past their retention date (expiration plus the facility's retention days), oldest first, with their
    /// place. Empty when no retention period is set (OCD-070).
    /// </summary>
    public async Task<IReadOnlyList<TubeInPlaceDto>> ListDueForDiscardAsync(CancellationToken ct = default)
    {
        var days = await RetentionDaysAsync(ct);
        if (days <= 0)
        {
            return [];
        }

        var now = _clock.UtcNow;
        var tubes = (await _specimens.ListAsync(s => s.StorageLocation != null && s.DiscardedUtc == null, ct))
            .Where(s => SpecimenDiscardRule.Due(SpecimenDiscardRule.KeepUntil(s.CollectedUtc, s.ExpiresUtc, days), now))
            .ToList();
        var rows = await ToRowsAsync(tubes, days, ct);
        return rows
            .OrderBy(t => t.KeepUntilUtc)
            .ThenBy(t => t.Location, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<int> RetentionDaysAsync(CancellationToken ct) =>
        _policy is null ? 0 : Math.Max(0, await _policy.GetSpecimenRetentionDaysAsync(ct));

    private async Task<List<TubeInPlaceDto>> ToRowsAsync(IReadOnlyList<Specimen> tubes, int retentionDays, CancellationToken ct)
    {
        if (tubes.Count == 0)
        {
            return [];
        }

        var ids = tubes.Select(t => t.PatientId).Distinct().ToList();
        var patients = (await _patients.ListAsync(p => ids.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        return tubes
            .Select(t =>
            {
                patients.TryGetValue(t.PatientId, out var patient);
                return new TubeInPlaceDto(
                    t.Id, t.AccessionNumber, t.SpecimenType, t.Status, t.PatientId, PatientName(patient, t.PatientId),
                    patient?.MedicalRecordNumber ?? "—", t.CollectedUtc, t.StoredUtc, t.StoredBy, t.StorageLocation,
                    SpecimenDiscardRule.KeepUntil(t.CollectedUtc, t.ExpiresUtc, retentionDays));
            })
            .ToList();
    }

    private static string PatientName(Patient? patient, long patientId) =>
        patient is null ? $"Patient {patientId}" : $"{patient.LastName}, {patient.FirstName}";

    /// <summary>Places tubes have been stored in, most recently used first, one spelling each.</summary>
    public async Task<IReadOnlyList<string>> KnownPlacesAsync(CancellationToken ct = default)
    {
        var events = await _events.ListAsync(e => !e.Discarded, ct);
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

        var (specimen, error) = await FindByScanAsync(request.Scan, ct);
        if (specimen is null)
        {
            return OperationResult<StoredSpecimenDto>.Fail(error!);
        }

        if (specimen.DiscardedUtc is DateTime discardedUtc)
        {
            return OperationResult<StoredSpecimenDto>.Fail($"Tube {specimen.AccessionNumber} was discarded on {ClinicalTimeText.Local(discardedUtc)}; it cannot be stored.");
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
            PatientName(patient, specimen.PatientId),
            patient?.MedicalRecordNumber ?? "—",
            location,
            previous,
            now));
    }

    /// <summary>
    /// Throws a tube away by scan. Kept while an order on it is unfinished or a reaction investigation on it is open;
    /// before its retention date (or with none set) a reason is required. The place it was in stays on its history.
    /// </summary>
    public async Task<OperationResult<DiscardedSpecimenDto>> DiscardAsync(DiscardSpecimenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_permissions is not null
            && !await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.SpecimenAccession, ct))
        {
            return OperationResult<DiscardedSpecimenDto>.Fail("Discarding tubes requires specimen accession permission.");
        }

        if (_orderSpecimens is null || _orders is null || _investigations is null)
        {
            return OperationResult<DiscardedSpecimenDto>.Fail("Discarding tubes is not available: the order and investigation checks are not configured.");
        }

        var (specimen, error) = await FindByScanAsync(request.Scan, ct);
        if (specimen is null)
        {
            return OperationResult<DiscardedSpecimenDto>.Fail(error!);
        }

        var orderIds = (await _orderSpecimens.ListAsync(os => os.SpecimenId == specimen.Id, ct)).Select(os => os.OrderId).Distinct().ToList();
        var openOrders = orderIds.Count == 0
            ? []
            : (await _orders.ListAsync(o => orderIds.Contains(o.Id)
                    && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Discontinued, ct))
                .Select(o => o.OrderNumber)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        var onInvestigation = await _investigations.AnyAsync(
            r => r.Status != ReactionInvestigationStatus.Closed
                 && (r.PreTransfusionSpecimenId == specimen.Id || r.PostTransfusionSpecimenId == specimen.Id), ct);

        var now = _clock.UtcNow;
        var keepUntil = SpecimenDiscardRule.KeepUntil(specimen.CollectedUtc, specimen.ExpiresUtc, await RetentionDaysAsync(ct));
        var check = SpecimenDiscardRule.Evaluate(specimen.DiscardedUtc, openOrders, onInvestigation, keepUntil, now, request.Reason);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DiscardedSpecimenDto>.Fail(check.Message);
        }

        var early = check.Severity == RuleSeverity.Warning ? request.Reason!.Trim() : null;
        var previous = specimen.StorageLocation;
        specimen.DiscardedUtc = now;
        specimen.DiscardedBy = _currentUser.UserName;
        specimen.DiscardReason = early;
        specimen.StorageLocation = null;
        _specimens.Update(specimen);
        await _events.AddAsync(new SpecimenStorageEvent
        {
            SpecimenId = specimen.Id,
            Location = "Discarded",
            PreviousLocation = previous,
            StoredUtc = now,
            StoredBy = _currentUser.UserName,
            Note = early is null ? null : $"Early: {early}",
            Discarded = true
        }, ct);
        _audit.Record(
            AuditEventType.Update,
            nameof(Specimen),
            specimen.Id,
            oldValue: new { StorageLocation = previous, DiscardedUtc = (DateTime?)null },
            newValue: new { DiscardedUtc = now, specimen.AccessionNumber, specimen.PatientId, KeepUntilUtc = keepUntil },
            reason: early is null
                ? $"Tube discarded{(previous is null ? string.Empty : $" from {previous}")}."
                : $"Tube discarded early{(previous is null ? string.Empty : $" from {previous}")}: {early}");
        await _unitOfWork.SaveChangesAsync(ct);

        var patient = await _patients.GetByIdAsync(specimen.PatientId, ct);
        return OperationResult<DiscardedSpecimenDto>.Ok(new DiscardedSpecimenDto(
            specimen.Id, specimen.AccessionNumber, specimen.PatientId, PatientName(patient, specimen.PatientId),
            patient?.MedicalRecordNumber ?? "—", previous, now, early));
    }

    // One tube by accession or label barcode (case-insensitive), tracked for update.
    private async Task<(Specimen? Specimen, string? Error)> FindByScanAsync(string? raw, CancellationToken ct)
    {
        var scan = ScannerInputSanitizer.Sanitize(raw).Sanitized.Trim().ToUpperInvariant();
        if (scan.Length == 0)
        {
            return (null, "Scan the tube's accession or label barcode.");
        }

        var matches = await _specimens.ListAsync(
            s => s.AccessionNumber.ToUpper() == scan || (s.Barcode != null && s.Barcode.ToUpper() == scan), ct);
        if (matches.Count != 1)
        {
            return (null, matches.Count == 0
                ? $"No tube has accession or barcode {scan}."
                : $"More than one tube matches {scan}; scan the accession number.");
        }

        // The match came from a read-only query; change the tracked row.
        var specimen = await _specimens.GetByIdAsync(matches[0].Id, ct);
        return specimen is null ? (null, $"No tube has accession or barcode {scan}.") : (specimen, null);
    }
}
