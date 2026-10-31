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
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    [Fact]
    public async Task InRangeReading_IsStored_WithoutDeviation()
    {
        await using var c = _factory.Create();
        var location = await AddLocationAsync(c, "TMP-IN", 1m, 6m);

        var result = await Service(c).RecordAsync(new RecordStorageTemperatureRequest(location.Id, 4.2m));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(StorageTemperatureStatus.InRange, result.Value!.Status);
        Assert.Null(result.Value.DeviationId);
        var stored = await c.StorageTemperatureReadings.SingleAsync(r => r.Id == result.Value.Id);
        Assert.Equal(4.2m, stored.ReadingC);
        Assert.Equal("tech-test", stored.RecordedBy);
        Assert.Equal(_factory.Clock.UtcNow, stored.ReadingUtc);
        Assert.False(await c.Deviations.AnyAsync(d => d.ContextId == stored.Id && d.ContextType == StorageTemperatureService.DeviationContextType));
    }

    [Fact]
    public async Task Excursion_OpensMajorDeviation_NamingStoredUnits_AndDoesNotQuarantine()
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
