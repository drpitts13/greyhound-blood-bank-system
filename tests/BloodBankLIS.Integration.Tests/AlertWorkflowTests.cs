using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-299: alert inbox — raise, reconcile, acknowledge, scanner sources, and producers.
public class AlertWorkflowTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private AlertService Alerts(BloodBankDbContext c) =>
        new(new EfRepository<Alert>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private AlertScanner Scanner(BloodBankDbContext c)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        var par = new StockParService(
            new EfRepository<StockParLevel>(c), new EfRepository<ProductType>(c), new InventoryRepository(c), c);
        var inventory = new InventoryService(
            new InventoryRepository(c),
            new EfRepository<UnitBloodAttribute>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new IsbtLookupCatalog(new EfRepository<IsbtAboRhdCode>(c), new EfRepository<IsbtProductCode>(c)),
            c, _factory.Clock, _factory.CurrentUser, audit);
        var reagents = new ReagentQcService(
            new EfRepository<Reagent>(c), new EfRepository<ReagentLot>(c), new EfRepository<ReagentDailyQc>(c),
            c, _factory.Clock, _factory.CurrentUser, audit);
        return new AlertScanner(Alerts(c), par, inventory, reagents);
    }

    private static AlertCandidate Candidate(string source, string key) =>
        new(source, key, AlertSeverity.Warning, $"Title {key}", "Message", "/x");

    [Fact]
    public async Task Raise_SkipsAnActiveKey_EvenAfterAcknowledgement()
    {
        await using var c = _factory.Create();
        var alerts = Alerts(c);

        Assert.True(await alerts.RaiseAsync(Candidate(AlertRule.UnitsExpired, "EXPIRED:1")));
        Assert.False(await alerts.RaiseAsync(Candidate(AlertRule.UnitsExpired, "EXPIRED:1")));
        var id = (await c.Alerts.SingleAsync()).Id;
        Assert.True((await alerts.AcknowledgeAsync(id, new AcknowledgeAlertRequest())).Succeeded);
        Assert.False(await alerts.RaiseAsync(Candidate(AlertRule.UnitsExpired, "EXPIRED:1")));
        Assert.Equal(1, await c.Alerts.CountAsync());
        Assert.Equal(0, await alerts.CountOpenAsync());
    }

    [Fact]
    public async Task Sync_RaisesNew_ResolvesCleared_AndLeavesOtherSourcesAlone()
    {
        await using var c = _factory.Create();
        var alerts = Alerts(c);
        await alerts.RaiseAsync(Candidate(AlertRule.TemperatureExcursion, "TEMP:1"));

        var first = await alerts.SyncAsync(AlertRule.ParShortage, [Candidate(AlertRule.ParShortage, "PAR:1"), Candidate(AlertRule.ParShortage, "PAR:2")]);
        Assert.Equal((2, 0), first);
        var second = await alerts.SyncAsync(AlertRule.ParShortage, [Candidate(AlertRule.ParShortage, "PAR:2")]);
        Assert.Equal((0, 1), second);

        var resolved = await c.Alerts.SingleAsync(a => a.Key == "PAR:1");
        Assert.Equal(_factory.Clock.UtcNow, resolved.ResolvedUtc);
        Assert.Null((await c.Alerts.SingleAsync(a => a.Key == "TEMP:1")).ResolvedUtc);
        Assert.Equal(2, await alerts.CountOpenAsync());

        var open = await alerts.ListAsync(openOnly: true);
        Assert.Equal(["PAR:2", "TEMP:1"], open.Select(a => a.Title.Replace("Title ", "")).OrderBy(k => k));
        var withClosed = await alerts.ListAsync(openOnly: false);
        Assert.Equal(3, withClosed.Count);
    }

    [Fact]
    public async Task Acknowledge_RecordsWhoAndNote_IsAudited_AndOnlyOnce()
    {
        await using var c = _factory.Create();
        var alerts = Alerts(c);
        await alerts.RaiseAsync(Candidate(AlertRule.TemperatureExcursion, "TEMP:9"));
        var id = (await c.Alerts.SingleAsync()).Id;

        var ack = await alerts.AcknowledgeAsync(id, new AcknowledgeAlertRequest("  Fridge door closed  "));
        Assert.True(ack.Succeeded, ack.Error);
        Assert.Equal("tech-test", ack.Value!.AcknowledgedBy);
        Assert.Equal("Fridge door closed", ack.Value.AcknowledgeNote);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Alert) && e.EntityId == id));

        Assert.False((await alerts.AcknowledgeAsync(id, new AcknowledgeAlertRequest())).Succeeded);
        Assert.False((await alerts.AcknowledgeAsync(999_999, new AcknowledgeAlertRequest())).Succeeded);
    }

    [Fact]
    public async Task Scanner_RaisesParNearExpiryAndQc_AndResolvesParWhenStocked()
    {
        await using var c = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var product = new ProductType { ProductCode = "RBC-ALRT", Name = "Red cells" };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        c.StockParLevels.Add(new StockParLevel { ProductTypeId = product.Id, MinimumAvailable = 2 });
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-ALRT-NEAR",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = now.AddHours(6),
            Status = UnitStatus.Available
        });
        var reagent = new Reagent { Code = "ANTI-D", Name = "Anti-D" };
        c.Reagents.Add(reagent);
        await c.SaveChangesAsync();
        c.ReagentLots.Add(new ReagentLot
        {
            ReagentId = reagent.Id,
            LotNumber = "L-ALRT",
            ReceivedUtc = now.AddDays(-2),
            OpenedUtc = now.AddDays(-2),
            ExpiresUtc = now.AddDays(30),
            State = ReagentLotState.Opened
        });
        await c.SaveChangesAsync();

        var (raised, resolved) = await Scanner(c).ScanAsync();

        Assert.Equal(0, resolved);
        Assert.True(raised >= 3);
        var sources = await c.Alerts.Select(a => a.Source).Distinct().ToListAsync();
        Assert.Contains(AlertRule.ParShortage, sources);
        Assert.Contains(AlertRule.NearExpiry, sources);
        Assert.Contains(AlertRule.ReagentQc, sources);
        var par = await c.Alerts.SingleAsync(a => a.Source == AlertRule.ParShortage);
        Assert.Contains("short 1", par.Message);
        Assert.Equal("/inventory/par", par.Link);

        // A second scan with nothing changed raises nothing new.
        Assert.Equal(0, (await Scanner(c).ScanAsync()).Raised);

        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-ALRT-2",
            ProductTypeId = product.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            ExpiresUtc = now.AddDays(20),
            Status = UnitStatus.Available
        });
        await c.SaveChangesAsync();
        await Scanner(c).ScanAsync();
        Assert.NotNull((await c.Alerts.SingleAsync(a => a.Id == par.Id)).ResolvedUtc);
    }

    [Fact]
    public async Task UnitsExpiredAndExcursion_RaiseEventAlerts()
    {
        await using var c = _factory.Create();
        var scanner = Scanner(c);
        Assert.False(await scanner.RaiseUnitsExpiredAsync(0, _factory.Clock.UtcNow));
        Assert.True(await scanner.RaiseUnitsExpiredAsync(3, _factory.Clock.UtcNow));
        var expired = await c.Alerts.SingleAsync(a => a.Source == AlertRule.UnitsExpired);
        Assert.Equal("3 unit(s) expired", expired.Title);

        var location = new InventoryLocation { Code = "ALRT-FRIDGE", Name = "Alert fridge", StorageTempMinC = 1m, StorageTempMaxC = 6m };
        c.InventoryLocations.Add(location);
        await c.SaveChangesAsync();
        var temps = new StorageTemperatureService(
            new EfRepository<StorageTemperatureReading>(c),
            new EfRepository<InventoryLocation>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<Deviation>(c),
            c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            Alerts(c));
        await temps.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 4m));
        Assert.False(await c.Alerts.AnyAsync(a => a.Source == AlertRule.TemperatureExcursion));

        var excursion = await temps.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 9m));
        var alert = await c.Alerts.SingleAsync(a => a.Source == AlertRule.TemperatureExcursion);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal($"TEMP:{excursion.Value!.Id}", alert.Key);
        Assert.Contains($"DEV-{excursion.Value.DeviationId}", alert.Message);
    }
}
