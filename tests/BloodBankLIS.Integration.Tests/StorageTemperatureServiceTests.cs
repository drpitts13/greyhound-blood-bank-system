using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-296: storage temperature log, excursion deviation, and open-excursion board.
public class StorageTemperatureServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public StorageTemperatureServiceTests(SqliteContextFactory factory) => _factory = factory;

    private StorageTemperatureService Service(BloodBankDbContext c) =>
        new(new EfRepository<StorageTemperatureReading>(c),
            new EfRepository<InventoryLocation>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<Deviation>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            null,
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            new InventoryRepository(c));

    [Fact]
    public async Task InRangeReading_IsStored_WithoutDeviation()
    {
        await using var c = _factory.Create();
        var location = await AddLocationAsync(c, "TMP-IN", 1m, 6m);

        var result = await Service(c).RecordAsync(new RecordStorageTemperatureRequest(location.Id, 4.2m));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(StorageTemperatureStatus.InRange, result.Value!.Status);
        Assert.Null(result.Value.DeviationId);
        Assert.Null(result.Value.QuarantineAfterMinutes);
        var stored = await c.StorageTemperatureReadings.SingleAsync(r => r.Id == result.Value.Id);
        Assert.Equal(4.2m, stored.ReadingC);
        Assert.Equal("tech-test", stored.RecordedBy);
        Assert.Equal(_factory.Clock.UtcNow, stored.ReadingUtc);
        Assert.False(await c.Deviations.AnyAsync(d => d.ContextId == stored.Id && d.ContextType == StorageTemperatureService.DeviationContextType));
    }

    // TEST-BB-1032: an excursion says how long until the location's units are quarantined automatically (OCD-036; the
    // facility default is 30 minutes), so the user is warned (end-user testing pass 24).
    [Fact]
    public async Task Excursion_SaysWhenUnitsWillBeQuarantined()
    {
        await using var c = _factory.Create();
        var location = await AddLocationAsync(c, $"TMP-Q-{Guid.NewGuid():N}"[..14], 1m, 6m);
        var result = await Service(c).RecordAsync(new RecordStorageTemperatureRequest(location.Id, 9m));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(30, result.Value!.QuarantineAfterMinutes);
    }

    [Fact]
    public async Task Excursion_OpensMajorDeviation_NamingStoredUnits_AndDoesNotQuarantineAtOnce()
    {
        await using var c = _factory.Create();
        var location = await AddLocationAsync(c, "TMP-HOT", 1m, 6m);
        var product = new ProductType { ProductCode = "RBC-TMP", Name = "Red cells" };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        c.BloodUnits.AddRange(
            Unit("U-TMP-1", product.Id, location.Id, UnitStatus.Available),
            Unit("U-TMP-2", product.Id, location.Id, UnitStatus.Quarantine),
            Unit("U-TMP-3", product.Id, location.Id, UnitStatus.Transfused));
        await c.SaveChangesAsync();

        var result = await Service(c).RecordAsync(new RecordStorageTemperatureRequest(location.Id, 8.5m, Comment: "Door ajar"));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(StorageTemperatureStatus.AboveRange, result.Value!.Status);
        var deviationId = Assert.IsType<long>(result.Value.DeviationId);
        var deviation = await c.Deviations.SingleAsync(d => d.Id == deviationId);
        Assert.Equal(DeviationSeverity.Major, deviation.Severity);
        Assert.Equal(DeviationStatus.Open, deviation.Status);
        Assert.Equal(StorageTemperatureService.DeviationContextType, deviation.ContextType);
        Assert.Equal(result.Value.Id, deviation.ContextId);
        Assert.Contains("2 unit(s)", deviation.Description);
        Assert.Contains("TMP-HOT", deviation.Title);

        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.SingleAsync(u => u.UnitNumber == "U-TMP-1")).Status);
        var audits = await c.AuditEvents.ToListAsync();
        Assert.Contains(audits, e => e.EventType == AuditEventType.Deviation && e.EntityId == deviationId);
        Assert.Contains(audits, e => e.EventType == AuditEventType.Create
            && e.EntityType == nameof(StorageTemperatureReading)
            && e.EntityId == result.Value.Id);
    }

    [Fact]
    public async Task LocationWithoutRange_NeverRaisesAnExcursion()
    {
        await using var c = _factory.Create();
        var location = await AddLocationAsync(c, "TMP-NONE", null, null);

        var result = await Service(c).RecordAsync(new RecordStorageTemperatureRequest(location.Id, 30m));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(StorageTemperatureStatus.NoRange, result.Value!.Status);
        Assert.Null(result.Value.DeviationId);
    }

    [Fact]
    public async Task ImplausibleFutureOrInactive_AreRejected()
    {
        await using var c = _factory.Create();
        var location = await AddLocationAsync(c, "TMP-BAD", 1m, 6m);
        var inactive = await AddLocationAsync(c, "TMP-OFF", 1m, 6m, isActive: false);
        var service = Service(c);

        Assert.False((await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 400m))).Succeeded);
        Assert.False((await service.RecordAsync(new RecordStorageTemperatureRequest(
            location.Id, 4m, _factory.Clock.UtcNow.AddHours(1)))).Succeeded);
        Assert.False((await service.RecordAsync(new RecordStorageTemperatureRequest(inactive.Id, 4m))).Succeeded);
        Assert.False((await service.RecordAsync(new RecordStorageTemperatureRequest(999_999, 4m))).Succeeded);
        Assert.False(await c.StorageTemperatureReadings.AnyAsync(
            r => r.InventoryLocationId == location.Id || r.InventoryLocationId == inactive.Id));
    }

    [Fact]
    public async Task OpenExcursions_DropWhenTheDeviationCloses()
    {
        await using var c = _factory.Create();
        var location = await AddLocationAsync(c, "TMP-COLD", -30m, -18m);
        var service = Service(c);
        var excursion = await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, -12m));
        Assert.True(excursion.Succeeded, excursion.Error);

        Assert.Contains(await service.ListOpenExcursionsAsync(), r => r.Id == excursion.Value!.Id);

        var deviation = await c.Deviations.SingleAsync(d => d.Id == excursion.Value!.DeviationId);
        deviation.Status = DeviationStatus.Closed;
        await c.SaveChangesAsync();

        Assert.DoesNotContain(await service.ListOpenExcursionsAsync(), r => r.Id == excursion.Value!.Id);
    }

    [Fact]
    public async Task Locations_ShowLastReading_AndKeepTheRangeInForceAtTheTime()
    {
        await using var c = _factory.Create();
        var location = await AddLocationAsync(c, "TMP-LAST", 1m, 6m);
        var service = Service(c);
        await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 3m, _factory.Clock.UtcNow.AddHours(-2)));
        await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 5m, _factory.Clock.UtcNow.AddHours(-1)));

        location.StorageTempMaxC = 4m;
        await c.SaveChangesAsync();

        var row = Assert.Single(await service.ListLocationsAsync(), l => l.LocationId == location.Id);
        Assert.Equal(5m, row.LastReadingC);
        Assert.Equal(StorageTemperatureStatus.InRange, row.LastStatus);
        Assert.Equal(4m, row.RangeMaxC);

        var history = await service.ListReadingsAsync(location.Id, 7);
        Assert.Equal(2, history.Count);
        Assert.Equal(5m, history[0].ReadingC);
        Assert.All(history, h => Assert.Equal(6m, h.RangeMaxC));
    }

    // TEST-BB-304 (OCD-036): an excursion that lasts longer than 30 minutes quarantines the stored units.
    [Fact]
    public async Task SustainedExcursion_QuarantinesStoredUnits_AndSharesOneDeviation()
    {
        await using var c = _factory.Create();
        var start = _factory.Clock.UtcNow;
        var location = await AddLocationAsync(c, "TMP-SUST", 1m, 6m);
        var product = new ProductType { ProductCode = "RBC-SUST", Name = "Red cells" };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        c.BloodUnits.AddRange(
            Unit("U-SUST-1", product.Id, location.Id, UnitStatus.Available),
            Unit("U-SUST-2", product.Id, location.Id, UnitStatus.Allocated),
            Unit("U-SUST-3", product.Id, location.Id, UnitStatus.Issued));
        await c.SaveChangesAsync();
        var service = Service(c);

        var first = await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 9m, start));
        _factory.Clock.UtcNow = start.AddMinutes(20);
        var second = await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 9.5m));
        Assert.Equal(first.Value!.DeviationId, second.Value!.DeviationId);
        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.AsNoTracking().SingleAsync(u => u.UnitNumber == "U-SUST-1")).Status);

        _factory.Clock.UtcNow = start.AddMinutes(31);
        var quarantined = await service.EnforceSustainedExcursionsAsync();

        Assert.Equal(2, quarantined);
        var units = await c.BloodUnits.AsNoTracking().Where(u => u.UnitNumber.StartsWith("U-SUST")).ToDictionaryAsync(u => u.UnitNumber);
        Assert.Equal(UnitStatus.Quarantine, units["U-SUST-1"].Status);
        Assert.Equal(UnitStatus.Quarantine, units["U-SUST-2"].Status);
        Assert.Equal(UnitStatus.Issued, units["U-SUST-3"].Status);
        Assert.Equal(UnitQuarantineReason.StorageTemperatureExcursion, units["U-SUST-1"].QuarantineReasonCode);
        Assert.True(await c.InventoryStatusHistory.AnyAsync(h => h.BloodProductId == units["U-SUST-1"].Id && h.ToStatus == UnitStatus.Quarantine));
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityId == units["U-SUST-2"].Id && e.NewValueJson!.Contains("TemperatureExcursion")));
        var deviation = await c.Deviations.AsNoTracking().SingleAsync(d => d.Id == first.Value.DeviationId);
        Assert.Contains("2 unit(s) quarantined automatically", deviation.Description);
        Assert.Equal(0, await service.EnforceSustainedExcursionsAsync());
        _factory.Clock.UtcNow = start;
    }

    [Fact]
    public async Task ShortExcursion_EndedByAnInRangeReading_DoesNotQuarantine()
    {
        await using var c = _factory.Create();
        var start = _factory.Clock.UtcNow;
        var location = await AddLocationAsync(c, "TMP-SHORT", 1m, 6m);
        var product = new ProductType { ProductCode = "RBC-SHORT", Name = "Red cells" };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        c.BloodUnits.Add(Unit("U-SHORT-1", product.Id, location.Id, UnitStatus.Available));
        await c.SaveChangesAsync();
        var service = Service(c);

        await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 9m, start));
        _factory.Clock.UtcNow = start.AddMinutes(25);
        await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 4m));
        _factory.Clock.UtcNow = start.AddMinutes(90);

        Assert.Equal(0, await service.EnforceSustainedExcursionsAsync());
        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.AsNoTracking().SingleAsync(u => u.UnitNumber == "U-SHORT-1")).Status);
        _factory.Clock.UtcNow = start;
    }

    [Fact]
    public async Task QuarantineMinutesZero_TurnsAutomaticQuarantineOff()
    {
        await using var c = _factory.Create();
        var start = _factory.Clock.UtcNow;
        c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.ExcursionQuarantineMinutes, Value = "0" });
        var location = await AddLocationAsync(c, "TMP-OFF0", 1m, 6m);
        var product = new ProductType { ProductCode = "RBC-OFF0", Name = "Red cells" };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        c.BloodUnits.Add(Unit("U-OFF0-1", product.Id, location.Id, UnitStatus.Available));
        await c.SaveChangesAsync();

        await Service(c).RecordAsync(new RecordStorageTemperatureRequest(location.Id, 9m, start));
        _factory.Clock.UtcNow = start.AddHours(3);
        Assert.Equal(0, await Service(c).EnforceSustainedExcursionsAsync());
        c.SystemSettings.Remove(await c.SystemSettings.SingleAsync(s => s.Key == FacilityPolicyKeys.ExcursionQuarantineMinutes));
        await c.SaveChangesAsync();
        _factory.Clock.UtcNow = start;
    }

    [Fact]
    public async Task Report_SummarizesDevice_FiltersExcursions_AndKeepsInactiveDevices()
    {
        await using var c = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var location = await AddLocationAsync(c, "TMP-RPT", 1m, 6m);
        var service = Service(c);
        await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 4m, now.AddHours(-5)));
        await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 7.5m, now.AddHours(-4)));
        await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 5m, now.AddHours(-3)));
        await service.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 3m, now.AddDays(-10)));
        location.IsActive = false;
        await c.SaveChangesAsync();

        Assert.Contains(await service.ListDevicesAsync(), d => d.LocationId == location.Id && !d.IsActive);

        var all = await service.ReportAsync(location.Id, now.AddDays(-1), now.AddHours(1), excursionsOnly: false);
        Assert.True(all.Succeeded, all.Error);
        var device = Assert.Single(all.Value!.Devices);
        Assert.Equal("TMP-RPT", device.Code);
        Assert.False(device.IsActive);
        Assert.Equal(3, device.Summary.Readings);
        Assert.Equal(1, device.Summary.ExcursionEvents);
        Assert.Equal(7.5m, device.Summary.MaxC);
        Assert.Equal(3, all.Value.Readings.Count);
        Assert.True(all.Value.Readings.SequenceEqual(all.Value.Readings.OrderBy(r => r.ReadingUtc)));

        var excursions = await service.ReportAsync(location.Id, now.AddDays(-1), now.AddHours(1), excursionsOnly: true);
        Assert.Equal(7.5m, Assert.Single(excursions.Value!.Readings).ReadingC);
        Assert.Equal(3, Assert.Single(excursions.Value.Devices).Summary.Readings);
    }

    [Fact]
    public async Task Report_RejectsBackwardsOrOverlongPeriod()
    {
        await using var c = _factory.Create();
        var now = _factory.Clock.UtcNow;

        Assert.False((await Service(c).ReportAsync(null, now, now.AddDays(-1), false)).Succeeded);
        Assert.False((await Service(c).ReportAsync(null, now.AddDays(-400), now, false)).Succeeded);
        Assert.False((await Service(c).ReportAsync(-1, now.AddDays(-1), now, false)).Succeeded);
    }

    private static BloodUnit Unit(string number, long productTypeId, long locationId, UnitStatus status) => new()
    {
        UnitNumber = number,
        ProductTypeId = productTypeId,
        Abo = AboGroup.O,
        RhD = RhType.Positive,
        ExpiresUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        Status = status,
        CurrentLocationId = locationId
    };

    private static async Task<InventoryLocation> AddLocationAsync(
        BloodBankDbContext c, string code, decimal? min, decimal? max, bool isActive = true)
    {
        var location = new InventoryLocation
        {
            Code = code,
            Name = $"{code} storage",
            StorageTempMinC = min,
            StorageTempMaxC = max,
            IsActive = isActive
        };
        c.InventoryLocations.Add(location);
        await c.SaveChangesAsync();
        return location;
    }
}
