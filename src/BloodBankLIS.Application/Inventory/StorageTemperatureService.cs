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

public sealed record RecordStorageTemperatureRequest(
    long LocationId,
    decimal ReadingC,
    DateTime? ReadingUtc = null,
    string? Comment = null);

/// <summary>
/// Storage temperature log. A reading outside the location's range opens a deviation
/// so the excursion is investigated. Units are not quarantined automatically: that
/// decision stays with staff (OCD-036).
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

    public StorageTemperatureService(
        IRepository<StorageTemperatureReading> readings,
        IRepository<InventoryLocation> locations,
        IRepository<BloodUnit> units,
        IRepository<Deviation> deviations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        AlertService? alerts = null)
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

        if (StorageTemperatureRule.IsExcursion(status))
        {
            var stored = await CountStoredUnitsAsync(location.Id, ct);
            var deviation = new Deviation
            {
                Title = $"Storage temperature excursion at {location.Code}",
                Description =
                    $"{location.Name} read {reading.ReadingC} °C at {readingUtc:u} against a range of {RangeText(location.StorageTempMinC, location.StorageTempMaxC)}. " +
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
            await _alerts.RaiseAsync(new AlertCandidate(
                AlertRule.TemperatureExcursion,
                $"TEMP:{reading.Id}",
                AlertSeverity.Critical,
                $"Temperature excursion at {location.Code}",
                $"{location.Name} read {reading.ReadingC} °C at {readingUtc:yyyy-MM-dd HH:mm} UTC. Deviation DEV-{reading.DeviationId} is open.",
                "/storage-temps"), ct);
        }

        return OperationResult<StorageTemperatureReadingDto>.Ok(ToDto(reading, location.Code));
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
