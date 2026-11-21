using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-318 (OCD-004): what still relies on placeholder or unknown ISBT codes.
public class IsbtReadinessTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static IsbtReadinessService Service(BloodBankDbContext c) =>
        new(new EfRepository<IsbtProductCode>(c), new EfRepository<IsbtAboRhdCode>(c), new EfRepository<IsbtCollectionType>(c),
            new EfRepository<ProductType>(c), new EfRepository<BloodUnit>(c));

    [Fact]
    public async Task PlaceholderCatalogOnly_IsNotReady_AndNamesWhatIsInUse()
    {
        await using var c = _factory.Create();
        var product = await SeedAsync(c, licensed: false);

        var r = await Service(c).GetAsync();

        Assert.False(r.Ready);
        Assert.Empty(r.LicensedVersions);
        Assert.Contains("no licensed ICCBBA extract", r.Summary);
        Assert.Equal(new IsbtCatalogCountDto(1, 1, 0), r.ProductCodes);
        Assert.Contains(r.Problems, p => p.Kind == "Product" && p.Id == product.Id && p.Problem.Contains("placeholder"));
        Assert.Contains(r.Problems, p => p.Kind == "Unit" && p.Label == "W9999RD000001" && p.Problem.StartsWith("Product code"));
        Assert.Contains(r.Problems, p => p.Kind == "Unit" && p.Label == "W9999RD000001" && p.Problem.StartsWith("ABO/RhD code"));
    }

    [Fact]
    public async Task LicensedImport_WithEverythingMapped_IsReady()
    {
        await using var c = _factory.Create();
        await SeedAsync(c, licensed: true);

        var r = await Service(c).GetAsync();

        Assert.True(r.Ready, r.Summary);
        Assert.Equal(["ICCBBA-TEST-2026"], r.LicensedVersions);
        Assert.Empty(r.Problems);
        Assert.Equal(0, r.PlaceholderInUse + r.UnknownInUse);
    }

    [Fact]
    public async Task UnknownCodes_AndUnmappedProducts_BlockReadiness()
    {
        await using var c = _factory.Create();
        await SeedAsync(c, licensed: true);
        c.ProductTypes.Add(new ProductType { ProductCode = "PLT-RD", Name = "Platelets", ComponentClass = ComponentClass.Platelets });
        var rbc = c.ProductTypes.Single(p => p.ProductCode == "RBC-RD");
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "W9999RD000002", ProductTypeId = rbc.Id, ProductDescriptionCode = "E9999", AboRhdCode = "51",
            Abo = AboGroup.O, RhD = RhType.Positive, Status = UnitStatus.Available, ExpiresUtc = _factory.Clock.UtcNow.AddDays(9)
        });
        await c.SaveChangesAsync();

        var r = await Service(c).GetAsync();

        Assert.False(r.Ready);
        Assert.Contains(r.Problems, p => p.Label.StartsWith("PLT-RD") && p.Problem == "No ISBT product code mapped");
        Assert.Contains(r.Problems, p => p.Label == "W9999RD000002" && p.Problem == "Product code E9999 is not in the catalog");
        Assert.Contains("not in the catalog at all", r.Summary);
    }

    [Fact]
    public async Task UnitsWithoutIsbtCodes_AndUnitsNotOnHand_AreNotCounted()
    {
        await using var c = _factory.Create();
        await SeedAsync(c, licensed: true);
        var rbc = c.ProductTypes.Single(p => p.ProductCode == "RBC-RD");
        c.BloodUnits.AddRange(
            new BloodUnit { UnitNumber = "LEGACY-1", ProductTypeId = rbc.Id, Abo = AboGroup.A, RhD = RhType.Positive, Status = UnitStatus.Available, ExpiresUtc = _factory.Clock.UtcNow.AddDays(9) },
            new BloodUnit { UnitNumber = "W9999RD000003", ProductTypeId = rbc.Id, ProductDescriptionCode = "E9999", Abo = AboGroup.O, RhD = RhType.Positive, Status = UnitStatus.Transfused, ExpiresUtc = _factory.Clock.UtcNow.AddDays(9) });
        await c.SaveChangesAsync();

        var r = await Service(c).GetAsync();

        Assert.True(r.Ready, r.Summary);
        Assert.Equal(1, r.UnitsWithoutIsbtCodes);
        Assert.DoesNotContain(r.Problems, p => p.Label == "W9999RD000003");
    }

    private async Task<ProductType> SeedAsync(BloodBankDbContext c, bool licensed)
    {
        var version = licensed ? "ICCBBA-TEST-2026" : "PLACEHOLDER-REQUIRES-ICCBBA";
        c.IsbtProductCodes.Add(new IsbtProductCode { ProductDescriptionCode = "E0206", Description = "Red cells", StandardVersion = version, IsPlaceholder = !licensed });
        c.IsbtAboRhdCodes.Add(new IsbtAboRhdCode { Code = "51", Abo = AboGroup.O, RhD = RhType.Positive, StandardVersion = version, IsPlaceholder = !licensed });
        var product = new ProductType { ProductCode = "RBC-RD", Name = "Red cells", ComponentClass = ComponentClass.RedBloodCells, Isbt128ProductCode = "E0206" };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "W9999RD000001", ProductTypeId = product.Id, ProductDescriptionCode = "E0206", AboRhdCode = "51",
            Abo = AboGroup.O, RhD = RhType.Positive, Status = UnitStatus.Available, ExpiresUtc = _factory.Clock.UtcNow.AddDays(9)
        });
        await c.SaveChangesAsync();
        return product;
    }
}
