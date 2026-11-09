using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record StorageTemperatureReadingDto(
    long Id,
    long LocationId,
    string LocationCode,
    decimal ReadingC,
    DateTime ReadingUtc,
    string RecordedBy,
    decimal? RangeMinC,
    decimal? RangeMaxC,
    StorageTemperatureStatus Status,
    long? DeviationId,
    string? Comment);

public sealed record StorageLocationTemperatureDto(
    long LocationId,
    string Code,
    string Name,
    decimal? RangeMinC,
    decimal? RangeMaxC,
    decimal? LastReadingC,
    DateTime? LastReadingUtc,
    StorageTemperatureStatus? LastStatus,
    int UnitsStored);

public sealed record StorageDeviceDto(
    long LocationId,
    string Code,
    string Name,
    LocationType LocationType,
    bool IsActive);

public sealed record StorageTemperatureDeviceSummaryDto(
    long LocationId,
    string Code,
    string Name,
    LocationType LocationType,
    bool IsActive,
    decimal? RangeMinC,
    decimal? RangeMaxC,
    StorageTemperatureSummary Summary);

public sealed record StorageTemperatureReportDto(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<StorageTemperatureDeviceSummaryDto> Devices,
    IReadOnlyList<StorageTemperatureReadingDto> Readings);

public sealed record RecordStorageTemperatureRequest(
    long LocationId,
    decimal ReadingC,
    DateTime? ReadingUtc = null,
    string? Comment = null);

/// <summary>
/// Storage temperature log. A reading outside the location's range opens a deviation
/// (one per unbroken run of out-of-range readings). Once an excursion has lasted longer
/// than <c>Inventory.ExcursionQuarantineMinutes</c> (default 30), the units stored at that
/// location are quarantined automatically (OCD-036, decided 2026-10-31).
/// </summary>
public sealed class StorageTemperatureService
{
    public const string DeviationContextType = "StorageTemperatureReading";

    private readonly IRepository<StorageTemperatureReading> _readings;
    private readonly IRepository<InventoryLocation> _locations;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<Deviation> _deviations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly AlertService? _alerts;
    private readonly FacilityPolicyService? _policy;
    private readonly IInventoryRepository? _inventory;

    public StorageTemperatureService(
        IRepository<StorageTemperatureReading> readings,
        IRepository<InventoryLocation> locations,
        IRepository<BloodUnit> units,
        IRepository<Deviation> deviations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        AlertService? alerts = null,
        FacilityPolicyService? policy = null,
        IInventoryRepository? inventory = null)
    {
        _readings = readings;
        _locations = locations;
        _units = units;
        _deviations = deviations;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _alerts = alerts;
        _policy = policy;
        _inventory = inventory;
    }

    public async Task<OperationResult<StorageTemperatureReadingDto>> RecordAsync(
        RecordStorageTemperatureRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var location = await _locations.GetByIdAsync(request.LocationId, ct);
        if (location is null || !location.IsActive)
        {
            return OperationResult<StorageTemperatureReadingDto>.Fail("Storage location not found.");
        }

        if (!StorageTemperatureRule.IsPlausible(request.ReadingC))
        {
            return OperationResult<StorageTemperatureReadingDto>.Fail(
                $"A reading of {request.ReadingC} °C is outside {StorageTemperatureRule.LowestPlausibleC} to {StorageTemperatureRule.HighestPlausibleC} °C. Check the entry.");
        }

        var now = _clock.UtcNow;
        var readingUtc = request.ReadingUtc ?? now;
        if (StorageTemperatureRule.IsInFuture(readingUtc, now))
        {
            return OperationResult<StorageTemperatureReadingDto>.Fail("The reading time is in the future.");
        }

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        var status = StorageTemperatureRule.Evaluate(request.ReadingC, location.StorageTempMinC, location.StorageTempMaxC);
        var reading = new StorageTemperatureReading
        {
            InventoryLocationId = location.Id,
            ReadingC = request.ReadingC,
            ReadingUtc = readingUtc,
            RecordedBy = _currentUser.UserName,
            RangeMinC = location.StorageTempMinC,
            RangeMaxC = location.StorageTempMaxC,
            Status = status,
            Comment = comment
        };
        await _readings.AddAsync(reading, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // A reading that continues an excursion joins that excursion's open deviation.
        var runDeviation = StorageTemperatureRule.IsExcursion(status)
            ? await OpenRunDeviationAsync(location.Id, reading.Id, ct)
            : null;
        if (runDeviation is not null)
        {
            reading.DeviationId = runDeviation.Id;
        }
        else if (StorageTemperatureRule.IsExcursion(status))
        {
            var stored = await CountStoredUnitsAsync(location.Id, ct);
            var deviation = new Deviation
            {
                Title = $"Storage temperature excursion at {location.Code}",
                Description =
                    $"{location.Name} read {reading.ReadingC} °C at {ClinicalTimeText.Utc(readingUtc)} against a range of {RangeText(location.StorageTempMinC, location.StorageTempMaxC)}. " +
                    $"{stored} unit(s) were stored there. Review the units before release or issue.",
                Severity = DeviationSeverity.Major,
                Status = DeviationStatus.Open,
                ContextType = DeviationContextType,
                ContextId = reading.Id,
                ReportedBy = _currentUser.UserName,
                ReportedUtc = now
            };
            await _deviations.AddAsync(deviation, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            reading.DeviationId = deviation.Id;
            _audit.Record(
                AuditEventType.Deviation,
                nameof(Deviation),
                deviation.Id,
                null,
                new { deviation.Title, deviation.Severity, deviation.ContextType, deviation.ContextId },
                "Storage temperature excursion.");
        }

        _audit.Record(
            AuditEventType.Create,
            nameof(StorageTemperatureReading),
            reading.Id,
            null,
            new { location.Code, reading.ReadingC, reading.ReadingUtc, reading.Status, reading.DeviationId },
            StorageTemperatureRule.IsExcursion(status) ? "Storage temperature excursion recorded." : "Storage temperature recorded.");
        await _unitOfWork.SaveChangesAsync(ct);

        if (_alerts is not null && StorageTemperatureRule.IsExcursion(status))
        {
            // One alert per excursion: the key follows the excursion's deviation.
            await _alerts.RaiseAsync(new AlertCandidate(
                AlertRule.TemperatureExcursion,
                $"TEMP:DEV-{reading.DeviationId}",
                AlertSeverity.Critical,
                $"Temperature excursion at {location.Code}",
                $"{location.Name} read {reading.ReadingC} °C at {readingUtc:yyyy-MM-dd HH:mm} UTC. Deviation DEV-{reading.DeviationId} is open.",
                "/storage-temps"), ct);
        }

        await EnforceLocationAsync(location, ct);
        return OperationResult<StorageTemperatureReadingDto>.Ok(ToDto(reading, location.Code));
    }

    /// <summary>
    /// Quarantines the units at every location whose excursion has lasted past the facility
    /// limit. Runs after each reading and on the background schedule, so an excursion with no
    /// further reading is still caught. Returns the number of units quarantined.
    /// </summary>
    public async Task<int> EnforceSustainedExcursionsAsync(CancellationToken ct = default)
    {
        var total = 0;
        foreach (var location in await _locations.ListAsync(l => l.IsActive, ct))
        {
            total += await EnforceLocationAsync(location, ct);
        }

        return total;
    }

    private async Task<int> EnforceLocationAsync(InventoryLocation location, CancellationToken ct)
    {
        if (_policy is null || _inventory is null)
        {
            return 0;
        }

        var minutes = await _policy.GetExcursionQuarantineMinutesAsync(ct);
        var runs = (await _readings.ListAsync(r => r.InventoryLocationId == location.Id, ct))
            .OrderByDescending(r => r.ReadingUtc)
            .ThenByDescending(r => r.Id)
            .ToList();
        var start = StorageTemperatureRule.OngoingExcursionStart(runs.Select(r => (r.ReadingUtc, r.Status)));
        var now = _clock.UtcNow;
        if (!StorageTemperatureRule.RequiresQuarantine(start, now, minutes))
        {
            return 0;
        }

        var note = $"Storage excursion at {location.Code} since {start:yyyy-MM-dd HH:mm} UTC lasted more than {minutes} minutes.";
        var stored = StorageTemperatureRule.StoredStatuses.ToList();
        var candidates = await _units.ListAsync(
            u => u.CurrentLocationId == location.Id && stored.Contains(u.Status) && u.Status != UnitStatus.Quarantine,
            ct);
        var quarantined = new List<string>();
        foreach (var candidate in candidates)
        {
            var unit = await _units.GetByIdAsync(candidate.Id, ct);
            if (unit is null || !InventoryStatusTransition.IsAllowed(unit.Status, UnitStatus.Quarantine))
            {
                continue;
            }

            var from = unit.Status;
            unit.Status = UnitStatus.Quarantine;
            unit.QuarantineReasonCode = UnitQuarantineReason.StorageTemperatureExcursion;
            unit.QuarantineReason = note;
            _units.Update(unit);
            _inventory.AddStatusHistory(new InventoryStatusHistory
            {
                BloodProductId = unit.Id,
                FromStatus = from,
                ToStatus = UnitStatus.Quarantine,
                FromLocationId = unit.CurrentLocationId,
                ToLocationId = unit.CurrentLocationId,
                Reason = note,
                ChangedBy = _currentUser.UserName,
                ChangedUtc = now
            });
            _audit.Record(
                AuditEventType.ProductStatus,
                nameof(BloodUnit),
                unit.Id,
                oldValue: new { Status = from },
                newValue: new { unit.UnitNumber, Status = UnitStatus.Quarantine, Path = "TemperatureExcursion", ReasonCode = UnitQuarantineReason.StorageTemperatureExcursion, Notes = note },
                reason: "Unit quarantined automatically after a sustained storage excursion.");
            quarantined.Add(unit.UnitNumber);
        }

        if (quarantined.Count == 0)
        {
            return 0;
        }

        var deviationId = runs.First().DeviationId;
        if (deviationId is long id && await _deviations.GetByIdAsync(id, ct) is { } deviation)
        {
            deviation.Description += $" {now:yyyy-MM-dd HH:mm} UTC: {quarantined.Count} unit(s) quarantined automatically ({string.Join(", ", quarantined)}).";
            _deviations.Update(deviation);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        if (_alerts is not null)
        {
            await _alerts.RaiseAsync(new AlertCandidate(
                AlertRule.TemperatureExcursion,
                $"TEMP-QUAR:{location.Id}:{now:yyyyMMddHHmm}",
                AlertSeverity.Critical,
                $"{quarantined.Count} unit(s) quarantined at {location.Code}",
                note + $" Deviation DEV-{deviationId}. Review each unit before release.",
                "/inventory?board=quarantine"), ct);
        }

        return quarantined.Count;
    }

    /// <summary>The open deviation of the excursion this reading continues, if the previous reading was out of range.</summary>
    private async Task<Deviation?> OpenRunDeviationAsync(long locationId, long currentReadingId, CancellationToken ct)
    {
        var previous = (await _readings.ListAsync(r => r.InventoryLocationId == locationId && r.Id != currentReadingId, ct))
            .OrderByDescending(r => r.ReadingUtc)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();
        if (previous is null || !StorageTemperatureRule.IsExcursion(previous.Status) || previous.DeviationId is not long id)
        {
            return null;
        }

        var deviation = await _deviations.GetByIdAsync(id, ct);
        return deviation is { Status: not DeviationStatus.Closed } ? deviation : null;
    }

    public async Task<IReadOnlyList<StorageTemperatureReadingDto>> ListReadingsAsync(
        long? locationId,
        int days,
        CancellationToken ct = default)
    {
        var since = _clock.UtcNow.AddDays(-Math.Clamp(days, 1, 366));
        var rows = await _readings.ListAsync(
            r => r.ReadingUtc >= since && (locationId == null || r.InventoryLocationId == locationId),
            ct);
        var codes = (await _locations.ListAsync(ct)).ToDictionary(l => l.Id, l => l.Code);
        return rows
            .OrderByDescending(r => r.ReadingUtc)
            .ThenByDescending(r => r.Id)
            .Select(r => ToDto(r, codes.GetValueOrDefault(r.InventoryLocationId, string.Empty)))
            .ToList();
    }

    /// <summary>Every temperature-controlled device, active or not, so retired devices stay reportable.</summary>
    public async Task<IReadOnlyList<StorageDeviceDto>> ListDevicesAsync(CancellationToken ct = default)
    {
        var types = StorageTemperatureRule.DeviceTypes.ToList();
        var ids = (await _readings.ListAsync(ct)).Select(r => r.InventoryLocationId).ToHashSet();
        return (await _locations.ListAsync(ct))
            .Where(l => types.Contains(l.LocationType) || ids.Contains(l.Id))
            .OrderByDescending(l => l.IsActive)
            .ThenBy(l => l.Code)
            .Select(l => new StorageDeviceDto(l.Id, l.Code, l.Name, l.LocationType, l.IsActive))
            .ToList();
    }

    /// <summary>
    /// Temperature history for one device or all devices over [from, to). Summaries cover every
    /// device in scope, including those with no readings in the period; readings may be limited
    /// to excursions.
    /// </summary>
    public async Task<OperationResult<StorageTemperatureReportDto>> ReportAsync(
        long? locationId,
        DateTime fromUtc,
        DateTime toUtc,
        bool excursionsOnly,
        CancellationToken ct = default)
    {
        if (toUtc <= fromUtc)
        {
            return OperationResult<StorageTemperatureReportDto>.Fail("The end of the period must be after the start.");
        }

        if ((toUtc - fromUtc).TotalDays > 366)
        {
            return OperationResult<StorageTemperatureReportDto>.Fail("A report covers at most 366 days.");
        }

        var devices = await ListDevicesAsync(ct);
        if (locationId is long id)
        {
            devices = devices.Where(d => d.LocationId == id).ToList();
            if (devices.Count == 0)
            {
                return OperationResult<StorageTemperatureReportDto>.Fail("Storage device not found.");
            }
        }

        var rows = await _readings.ListAsync(
            r => r.ReadingUtc >= fromUtc && r.ReadingUtc < toUtc && (locationId == null || r.InventoryLocationId == locationId),
            ct);
        var locations = (await _locations.ListAsync(ct)).ToDictionary(l => l.Id);
        var byDevice = rows.ToLookup(r => r.InventoryLocationId);
        var summaries = devices
            .Select(d =>
            {
                var loc = locations[d.LocationId];
                var summary = StorageTemperatureSummaryRule.Summarize(
                    byDevice[d.LocationId].Select(r => (r.ReadingUtc, r.ReadingC, r.Status)),
                    fromUtc,
                    toUtc < _clock.UtcNow ? toUtc : _clock.UtcNow);
                return new StorageTemperatureDeviceSummaryDto(
                    d.LocationId, d.Code, d.Name, d.LocationType, d.IsActive,
                    loc.StorageTempMinC, loc.StorageTempMaxC, summary);
            })
            .ToList();
        var readings = rows
            .Where(r => !excursionsOnly || StorageTemperatureRule.IsExcursion(r.Status))
            .OrderBy(r => locations.TryGetValue(r.InventoryLocationId, out var l) ? l.Code : string.Empty)
            .ThenBy(r => r.ReadingUtc)
            .ThenBy(r => r.Id)
            .Select(r => ToDto(r, locations.TryGetValue(r.InventoryLocationId, out var l) ? l.Code : string.Empty))
            .ToList();
        return OperationResult<StorageTemperatureReportDto>.Ok(new StorageTemperatureReportDto(fromUtc, toUtc, summaries, readings));
    }

    /// <summary>Excursions whose deviation is not yet closed.</summary>
    public async Task<IReadOnlyList<StorageTemperatureReadingDto>> ListOpenExcursionsAsync(CancellationToken ct = default)
    {
        var excursions = await _readings.ListAsync(
            r => r.Status == StorageTemperatureStatus.BelowRange || r.Status == StorageTemperatureStatus.AboveRange,
            ct);
        if (excursions.Count == 0)
        {
            return [];
        }

        var closed = (await _deviations.ListAsync(
                d => d.ContextType == DeviationContextType && d.Status == DeviationStatus.Closed,
                ct))
            .Select(d => d.Id)
            .ToHashSet();
        var codes = (await _locations.ListAsync(ct)).ToDictionary(l => l.Id, l => l.Code);
        return excursions
            .Where(r => r.DeviationId is not long id || !closed.Contains(id))
            .OrderByDescending(r => r.ReadingUtc)
            .Select(r => ToDto(r, codes.GetValueOrDefault(r.InventoryLocationId, string.Empty)))
            .ToList();
    }

    public async Task<IReadOnlyList<StorageLocationTemperatureDto>> ListLocationsAsync(CancellationToken ct = default)
    {
        var locations = await _locations.ListAsync(l => l.IsActive, ct);
        var readings = await _readings.ListAsync(ct);
        var latest = readings
            .GroupBy(r => r.InventoryLocationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReadingUtc).ThenByDescending(r => r.Id).First());
        var stored = StorageTemperatureRule.StoredStatuses.ToList();
        var counts = (await _units.ListAsync(
                u => u.CurrentLocationId != null && stored.Contains(u.Status),
                ct))
            .GroupBy(u => u.CurrentLocationId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return locations
            .OrderBy(l => l.Code)
            .Select(l =>
            {
                latest.TryGetValue(l.Id, out var last);
                return new StorageLocationTemperatureDto(
                    l.Id, l.Code, l.Name, l.StorageTempMinC, l.StorageTempMaxC,
                    last?.ReadingC, last?.ReadingUtc, last?.Status,
                    counts.GetValueOrDefault(l.Id));
            })
            .ToList();
    }

    private async Task<int> CountStoredUnitsAsync(long locationId, CancellationToken ct)
    {
        var stored = StorageTemperatureRule.StoredStatuses.ToList();
        return (await _units.ListAsync(
            u => u.CurrentLocationId == locationId && stored.Contains(u.Status),
            ct)).Count;
    }

    private static string RangeText(decimal? min, decimal? max) =>
        (min, max) switch
        {
            (decimal lo, decimal hi) => $"{lo} to {hi} °C",
            (decimal lo, null) => $"at least {lo} °C",
            (null, decimal hi) => $"at most {hi} °C",
            _ => "none"
        };

    private static StorageTemperatureReadingDto ToDto(StorageTemperatureReading r, string code) =>
        new(r.Id, r.InventoryLocationId, code, r.ReadingC, r.ReadingUtc, r.RecordedBy,
            r.RangeMinC, r.RangeMaxC, r.Status, r.DeviationId, r.Comment);
}
