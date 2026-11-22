using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ProductCatalogAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ProductCatalogAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ProductAdminService Products(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ProductAdminService(
            new EfRepository<ProductType>(c),
            new EfRepository<ProductAttribute>(c),
            new EfRepository<ProductAttributeAssignment>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveProductDefinitionRequest Request(string code) =>
        new(
            code,
            "Temp product",
            ComponentClass.RedBloodCells,
            null,
            24,
            false,
            true,
            true,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminProductsManage_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var denied = await Products(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ProductCatalogAuthorizationRule.CreateCode);
        Assert.False(await c.ProductTypes.AnyAsync(p => p.ProductCode == code));

        var allowed = await Products(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminProductsManage))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.ProductCode);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Products(c).CreateAsync(Request($"P{Guid.NewGuid():N}"[..8].ToUpperInvariant()));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Products(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminProductsManage))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ProductCatalogAuthorizationRule.ActivateCode);
        Assert.False((await c.ProductTypes.SingleAsync(p => p.Id == created.Value.Id)).IsActive);

        var allowed = await Products(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }

    [Fact]
    public async Task CreateAndUpdate_WriteConfigure()
    {
        await using var c = _factory.Create();
        var code = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var svc = Products(c);

        var created = await svc.CreateAsync(Request(code));
        Assert.True(created.Succeeded, created.Error ?? created.Evaluation?.HardStops.FirstOrDefault()?.Message);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(ProductType)
            && a.EntityId == created.Value!.Id
            && a.EventType == AuditEventType.Configure));

        var test = new TestDefinition
        {
            Code = $"RT{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Name = "Retype catalog",
            Category = TestCategory.AboRhRetype,
            ResultValueType = ResultValueType.AboRh,
            IsActive = true,
            IsDraft = false,
            Version = 1
        };
        c.TestDefinitions.Add(test);
        await c.SaveChangesAsync();

        var updated = await svc.UpdateAsync(created.Value!.Id, Request(code) with
        {
            RequiresRetype = true,
            RhPositiveRetypeTestId = test.Id,
            RhNegativeRetypeTestId = test.Id,
            ChangeReason = "Require ABO/Rh retype."
        });
        Assert.True(updated.Succeeded, updated.Error ?? updated.Evaluation?.HardStops.FirstOrDefault()?.Message);
        Assert.Equal(test.Id, updated.Value!.RhPositiveRetypeTestId);
        Assert.Equal(test.Id, updated.Value.RhNegativeRetypeTestId);

        var events = await c.AuditEvents
            .Where(a => a.EntityType == nameof(ProductType) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, events.Count(a => a.EventType == AuditEventType.Configure));
    }

    // TEST-BB-1403 (end-user testing loop 2 tick 23): retiring a product still the source of an active modification rule,
    // with a unit in inventory and a minimum stock level, succeeds and names all three.
    [Fact]
    public async Task Deactivate_NamesWhatStillUsesTheProduct()
    {
        await using var c = _factory.Create();
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var svc = new ProductAdminService(
            new EfRepository<ProductType>(c), new EfRepository<ProductAttribute>(c), new EfRepository<ProductAttributeAssignment>(c),
            c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env), new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            modificationRules: new EfRepository<ModificationRule>(c), units: new EfRepository<BloodUnit>(c),
            minimumStock: new EfRepository<MinimumStockLevel>(c));
        var key = $"R{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var created = await svc.CreateAsync(Request(key));
        Assert.True(created.Succeeded, created.Error);
        var productId = created.Value!.Id;
        var target = new ProductType { ProductCode = key + "T", Name = "Target", IsActive = true };
        c.ProductTypes.Add(target);
        var code = new ExpirationModificationCode { Code = key, OffsetAmount = 24, OffsetUnit = BloodBankLIS.Domain.ValueObjects.ExpirationOffsetUnit.Hours, IsActive = true };
        c.ExpirationModificationCodes.Add(code);
        await c.SaveChangesAsync();
        c.ModificationRules.Add(new ModificationRule { ModificationCode = "IRR-" + key, SourceProductTypeId = productId, TargetProductTypeId = target.Id,
            ModificationType = ModificationType.Irradiate, ExpirationModificationCodeId = code.Id, IsActive = true });
        c.BloodUnits.Add(new BloodUnit { UnitNumber = "U-" + key, ProductTypeId = productId, Abo = AboGroup.O, RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(5), Status = UnitStatus.Available });
        c.MinimumStockLevels.Add(new MinimumStockLevel { ProductTypeId = productId, MinimumAvailable = 2 });
        await c.SaveChangesAsync();

        var retired = await svc.DeactivateAsync(productId, "Replaced");

        Assert.True(retired.Succeeded, retired.Error);
        Assert.Contains(retired.Warnings, w => w.Message.Contains("IRR-" + key) && w.Message.Contains("source of"));
        Assert.Contains(retired.Warnings, w => w.Message.Contains("1 unit of") && w.Message.Contains("still in inventory"));
        Assert.Contains(retired.Warnings, w => w.Message.Contains("1 minimum stock level"));
    }
}
