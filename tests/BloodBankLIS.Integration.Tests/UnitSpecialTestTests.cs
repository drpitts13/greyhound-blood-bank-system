using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class UnitSpecialTestTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public UnitSpecialTestTests(SqliteContextFactory factory) => _factory = factory;

    private UnitSpecialTestService Service(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<BloodComponentSpecialTest>(c),
            new InventoryRepository(c),
            c,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            permissions);

    private static async Task<BloodUnit> SeedUnitAsync(BloodBankDbContext c)
    {
        var product = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells
        };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        var unit = new BloodUnit
        {
            UnitNumber = $"UT{Guid.NewGuid():N}"[..14],
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Negative,
            ExpiresUtc = DateTime.UtcNow.AddDays(30),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        return unit;
    }

    // TEST-BB-413: a result is recorded and audited; the same result again is refused.
    [Fact]
    public async Task Record_KeepsTheResult_AuditsIt_AndRefusesTheSameResultAgain()
    {
        await using var c = _factory.Create();
        var unit = await SeedUnitAsync(c);

        var recorded = await Service(c).RecordAsync(unit.Id, new RecordUnitSpecialTestRequest(" cmv ", "neg"));
        Assert.True(recorded.Succeeded, recorded.Error);
        Assert.Equal("CMV", recorded.Value!.TestCode);
        Assert.Equal("Negative", recorded.Value.Result);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(BloodComponentSpecialTest) && a.EntityId == recorded.Value.Id && a.EventType == AuditEventType.Create));

        var again = await Service(c).RecordAsync(unit.Id, new RecordUnitSpecialTestRequest("CMV", "Negative"));
        Assert.False(again.Succeeded);
        Assert.Contains("already recorded", again.Error);

        var unknown = await Service(c).RecordAsync(unit.Id + 100000, new RecordUnitSpecialTestRequest("CMV", "Negative"));
        Assert.False(unknown.Succeeded);
    }

    // TEST-BB-413: a correction needs a reason, keeps the earlier row, and the latest result is the unit's.
    [Fact]
    public async Task Correction_NeedsAReason_KeepsHistory_AndTheLatestResultCounts()
    {
        await using var c = _factory.Create();
        var unit = await SeedUnitAsync(c);
        var service = Service(c);
        Assert.True((await service.RecordAsync(unit.Id, new RecordUnitSpecialTestRequest("CMV", "Negative"))).Succeeded);

        var negatives = await SpecialRequirementLoader.NegativeTestCodesByUnitAsync(
            new EfRepository<BloodComponentSpecialTest>(c), [unit.Id], default);
        Assert.Contains("CMV", negatives[unit.Id]);

        var noReason = await service.RecordAsync(unit.Id, new RecordUnitSpecialTestRequest("CMV", "Positive"));
        Assert.False(noReason.Succeeded);
        Assert.Contains("reason", noReason.Error);

        var corrected = await service.RecordAsync(unit.Id,
            new RecordUnitSpecialTestRequest("CMV", "Positive", "Supplier corrected report 4471"));
        Assert.True(corrected.Succeeded, corrected.Error);

        var history = await service.ListAsync(unit.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal("Positive", Assert.Single(history, t => t.IsCurrent).Result);
        Assert.Contains(history, t => !t.IsCurrent && t.Result == "Negative");

        var correction = await c.AuditEvents.SingleAsync(a =>
            a.EntityType == nameof(BloodComponentSpecialTest) && a.EventType == AuditEventType.Correct && a.EntityId == corrected.Value!.Id);
        Assert.Equal("Supplier corrected report 4471", correction.Reason);
        Assert.Contains("Negative", correction.OldValueJson);

        // A later Positive means the unit no longer meets a CMV-negative typing.
        var after = await SpecialRequirementLoader.NegativeTestCodesByUnitAsync(
            new EfRepository<BloodComponentSpecialTest>(c), [unit.Id], default);
        Assert.False(after.ContainsKey(unit.Id));
    }

    // TEST-BB-413: recording needs the inventory receive permission.
    [Fact]
    public async Task Record_WithoutInventoryReceive_IsRefused()
    {
        await using var c = _factory.Create();
        var unit = await SeedUnitAsync(c);
        var denied = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.InventoryTransfer))
            .RecordAsync(unit.Id, new RecordUnitSpecialTestRequest("HBS", "Negative"));
        Assert.False(denied.Succeeded);
        Assert.Empty(await c.BloodComponentSpecialTests.Where(t => t.BloodProductId == unit.Id).ToListAsync());
    }
}
