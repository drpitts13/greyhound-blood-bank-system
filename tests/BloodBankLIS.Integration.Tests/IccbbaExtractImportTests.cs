using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Isbt128;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>TEST-BB-032 — drop-folder listing and licensed extract import.</summary>
public class IccbbaExtractImportTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public IccbbaExtractImportTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public void ListAvailable_EmptyDirectory_IsEmpty()
    {
        var dir = NewTempDir();
        try
        {
            var store = new IccbbaExtractDirectory(dir);
            Assert.Empty(store.ListAvailable());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ListAvailable_IgnoresAccessAndListsCsvJson()
    {
        var dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "products.csv"), "Product Code,Description\nT0001,Licensee row\n");
            File.WriteAllText(Path.Combine(dir, "catalog.json"), """{"productCodes":[],"aboRhdCodes":[]}""");
            File.WriteAllText(Path.Combine(dir, "official.mdb"), "not-an-extract");
            File.WriteAllText(Path.Combine(dir, "notes.md"), "ignore");

            var listed = new IccbbaExtractDirectory(dir).ListAvailable();
            Assert.Equal(2, listed.Count);
            Assert.Contains(listed, f => f.FileName == "products.csv" && f.Kind == nameof(IccbbaExtractKind.ProductDelimited));
            Assert.Contains(listed, f => f.FileName == "catalog.json");
            Assert.DoesNotContain(listed, f => f.FileName.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryRead_RejectsPathTraversal()
    {
        var dir = NewTempDir();
        try
        {
            var store = new IccbbaExtractDirectory(dir);
            Assert.False(store.TryRead("..\\secret.csv", out _, out var error));
            Assert.Contains("drop folder", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportExtractFiles_EmptySelection_Fails()
    {
        var dir = NewTempDir();
        await using var c = _factory.Create();
        var svc = Service(c, new IccbbaExtractDirectory(dir));
        var denied = await svc.ImportExtractFilesAsync(new LicensedIsbtExtractImportRequest(
            "ICCBBA-ST-TEST", true, "test", DropFileNames: []));
        Assert.False(denied.Succeeded);
        Assert.Contains("at least one", denied.Error, StringComparison.OrdinalIgnoreCase);
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task ImportExtractFiles_SelectedCsvAndAbo_ReplacesPlaceholder()
    {
        var dir = NewTempDir();
        File.WriteAllText(
            Path.Combine(dir, "products.csv"),
            "PDC,Description,Class\nT0001,Licensee-supplied red cell,RED BLOOD CELLS\n");
        File.WriteAllText(
            Path.Combine(dir, "facility-abo.csv"),
            "Code,Abo,RhD,SpecialMessage\nT1,O,Positive,synthetic\n");

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

        var imported = await Service(c, new IccbbaExtractDirectory(dir)).ImportExtractFilesAsync(
            new LicensedIsbtExtractImportRequest(
                "ICCBBA-ST-TEST",
                LicenseAcknowledgment: true,
                Reason: "TEST-BB-032",
                DropFileNames: ["products.csv", "facility-abo.csv"]));

        Assert.True(imported.Succeeded, imported.Error);
        Assert.Equal(1, imported.Value!.ProductCodesImported);
        Assert.Equal(1, imported.Value.AboRhdCodesImported);
        Assert.Equal(1, imported.Value.PlaceholdersReplaced);

        var product = await c.IsbtProductCodes.SingleAsync(p => p.ProductDescriptionCode == "T0001");
        Assert.False(product.IsPlaceholder);
        Assert.Equal("RedBloodCells", product.ComponentClass);
        Assert.Equal("ICCBBA-ST-TEST", product.StandardVersion);

        var abo = await c.IsbtAboRhdCodes.SingleAsync(a => a.Code == "T1");
        Assert.Equal(AboGroup.O, abo.Abo);
        Assert.Equal("synthetic", abo.SpecialMessage);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task ImportUploaded_WithoutAcknowledgment_IsHardStopped()
    {
        await using var c = _factory.Create();
        var denied = await Service(c).ImportUploadedExtractsAsync(
            "ICCBBA-ST-TEST",
            licenseAcknowledgment: false,
            reason: "test",
            files: [new IccbbaExtractUploadFile("products.csv", "PDC,Description\nT8002,Licensee row\n")]);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == IsbtLicensedCatalogImportRule.LicenseCode);
        Assert.False(await c.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "T8002"));
    }

    [Fact]
    public async Task ImportUploaded_AccessFile_FailsWithoutPersisting()
    {
        await using var c = _factory.Create();
        var denied = await Service(c).ImportUploadedExtractsAsync(
            "ICCBBA-ST-TEST",
            licenseAcknowledgment: true,
            reason: "test",
            files: [new IccbbaExtractUploadFile("tables.mdb", "binary")]);
        Assert.False(denied.Succeeded);
        Assert.Contains("Export", denied.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupCatalog_PrefersLicensedRow_WhenPlaceholderAlsoPresent()
    {
        await using var c = _factory.Create();
        c.IsbtProductCodes.AddRange(
            new IsbtProductCode
            {
                ProductDescriptionCode = "T8003",
                Description = "Placeholder description",
                ComponentClass = "Other",
                StandardVersion = "PLACEHOLDER-REQUIRES-ICCBBA",
                IsPlaceholder = true
            },
            new IsbtProductCode
            {
                ProductDescriptionCode = "T8003",
                Description = "Licensed description",
                ComponentClass = "RedBloodCells",
                StandardVersion = "ICCBBA-ST-PREF",
                IsPlaceholder = false
            });
        await c.SaveChangesAsync();

        var catalog = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(c),
            new EfRepository<IsbtProductCode>(c));
        var lookup = await catalog.GetProductLookupAsync();
        Assert.True(lookup.TryGetValue("T8003", out var row));
        Assert.Equal("Licensed description", row.Description);
        Assert.Equal("ICCBBA-ST-PREF", row.StandardVersion);
    }

    private IsbtProductCodeAdminService Service(BloodBankDbContext c, IIccbbaExtractDirectory? extracts = null) =>
        new(
            new EfRepository<IsbtProductCode>(c),
            new EfRepository<IsbtAboRhdCode>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            extracts: extracts);

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gbb-iccbba-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
