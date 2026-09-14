using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class IsbtLicensedCatalogImportTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public IsbtLicensedCatalogImportTests(SqliteContextFactory factory) => _factory = factory;

    private IsbtProductCodeAdminService Service(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<IsbtProductCode>(c),
            new EfRepository<IsbtAboRhdCode>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            permissions);

    [Fact]
    public async Task Import_WithoutAcknowledgment_IsHardStopped()
    {
        await using var c = _factory.Create();
        var denied = await Service(c).ImportLicensedAsync(new LicensedIsbtCatalogImportRequest(
            "ICCBBA-ST-TEST",
            LicenseAcknowledgment: false,
            Reason: "test",
            ProductCodes: [new("T0001", "Licensee row", "Other")]));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == IsbtLicensedCatalogImportRule.LicenseCode);
        Assert.False(await c.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "T0001"));
    }

    [Fact]
    public async Task Import_WithoutAdminConfigEdit_IsHardStopped()
    {
        await using var c = _factory.Create();
        var denied = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .ImportLicensedAsync(ValidRequest());
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == IsbtLicensedCatalogImportRule.PermissionCode);
    }

    [Fact]
    public async Task Import_PlaceholderVersion_IsHardStopped()
    {
        await using var c = _factory.Create();
        var denied = await Service(c).ImportLicensedAsync(new LicensedIsbtCatalogImportRequest(
            "PLACEHOLDER-REQUIRES-ICCBBA",
            LicenseAcknowledgment: true,
            Reason: "test",
            ProductCodes: [new("T0001", "Licensee row", "Other")]));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == IsbtLicensedCatalogImportRule.VersionCode);
        Assert.False(await c.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "T0001"));
    }

    [Fact]
    public async Task Import_LicensedRows_ReplacesPlaceholder_AndAudits()
    {
        await using var c = _factory.Create();
        c.IsbtProductCodes.Add(new IsbtProductCode
        {
            ProductDescriptionCode = "T0001",
            Description = "Seed placeholder",
            ComponentClass = "Other",
            StandardVersion = "PLACEHOLDER-REQUIRES-ICCBBA",
            IsPlaceholder = true
        });
        await c.SaveChangesAsync();

        var imported = await Service(c).ImportLicensedAsync(ValidRequest());
        Assert.True(imported.Succeeded, imported.Error);
        Assert.Equal(1, imported.Value!.ProductCodesImported);
        Assert.Equal(1, imported.Value.AboRhdCodesImported);
        Assert.Equal(1, imported.Value.PlaceholdersReplaced);

        var product = await c.IsbtProductCodes.SingleAsync(p => p.ProductDescriptionCode == "T0001");
        Assert.False(product.IsPlaceholder);
        Assert.Equal("ICCBBA-ST-TEST", product.StandardVersion);
        Assert.Equal("Licensee-supplied red cell", product.Description);

        var abo = await c.IsbtAboRhdCodes.SingleAsync(a => a.Code == "T1");
        Assert.False(abo.IsPlaceholder);
        Assert.Equal(AboGroup.O, abo.Abo);
        Assert.Equal(RhType.Positive, abo.RhD);

        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.Import && a.EntityType == nameof(IsbtProductCode)));
    }

    private static LicensedIsbtCatalogImportRequest ValidRequest() =>
        new(
            "ICCBBA-ST-TEST",
            LicenseAcknowledgment: true,
            Reason: "Facility licensed extract for tests.",
            ProductCodes: [new("T0001", "Licensee-supplied red cell", "RedBloodCells")],
            AboRhdCodes: [new("T1", "O", "Positive")]);
}
