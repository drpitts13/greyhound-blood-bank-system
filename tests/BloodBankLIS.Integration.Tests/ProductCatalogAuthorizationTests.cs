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
}
