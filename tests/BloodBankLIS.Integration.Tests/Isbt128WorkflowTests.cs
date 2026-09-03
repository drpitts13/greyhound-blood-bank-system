using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class Isbt128WorkflowTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public Isbt128WorkflowTests(SqliteContextFactory factory) => _factory = factory;

    private async Task SeedLookupsAsync(BloodBankDbContext context)
    {
        if (!await context.IsbtAboRhdCodes.AnyAsync())
        {
            context.IsbtAboRhdCodes.Add(new IsbtAboRhdCode
            {
                Code = "DEMO",
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsPlaceholder = true
            });
        }

        if (!await context.IsbtProductCodes.AnyAsync())
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0206",
                Description = "PLACEHOLDER RBC",
                ComponentClass = "RedBloodCells",
                AttributesJson = "[]",
                IsPlaceholder = true
            });
        }

        if (!await context.ProductTypes.AnyAsync(t => t.ProductCode == "RBC-ISBT"))
        {
            context.ProductTypes.Add(new ProductType
            {
                ProductCode = "RBC-ISBT",
                Name = "ISBT Test RBC",
                ComponentClass = ComponentClass.RedBloodCells,
                RequiresCrossmatch = true
            });
        }

        await context.SaveChangesAsync();
    }

    private ManualComponentEntryService CreateManual(BloodBankDbContext context)
    {
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        var inventory = new InventoryService(
            new InventoryRepository(context),
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            lookups,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser));

        var dinCheck = new PlaceholderDinCheckCharacterValidator();

        return new ManualComponentEntryService(lookups, dinCheck, inventory, _factory.Clock, _factory.CurrentUser);
    }

    private ScanSessionService CreateScanSession(BloodBankDbContext context)
    {
        var inventoryRepo = new InventoryRepository(context);
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        var inventory = new InventoryService(
            inventoryRepo,
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            lookups,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser));

        var dinCheck = new PlaceholderDinCheckCharacterValidator();

        return new ScanSessionService(
            new EfRepository<BloodComponentScanSession>(context),
            new EfRepository<BloodComponentScanSessionLine>(context),
            lookups,
            dinCheck,
            inventoryRepo,
            inventory,
            context,
            _factory.Clock,
            _factory.CurrentUser);
    }

    [Fact]
    public async Task ManualEntry_CreatesCanonicalComponent_WithUniqueIdentity()
    {
        await using var context = _factory.Create();
        await SeedLookupsAsync(context);
        var productTypeId = (await context.ProductTypes.FirstAsync(t => t.ProductCode == "RBC-ISBT")).Id;
        var manual = CreateManual(context);
        var dinCheck = new PlaceholderDinCheckCharacterValidator();
        var check = dinCheck.ComputeCheckCharacter("G123417654321");

        var result = await manual.CreateAsync(new ManualComponentEntryRequest(
            DonationNumber: $"G1234 17 654321 {check}",
            AboRhdCode: "DEMO",
            ProductDescriptionCode: "E0206",
            CollectionTypeCode: "0",
            DivisionCode: "00",
            ExtendedDivisionCode: null,
            ExpirationLocal: _factory.Clock.UtcNow.AddDays(30),
            ExpirationHasExplicitTime: false,
            ProductTypeId: productTypeId,
            ReleaseToAvailable: true,
            SecondVerifier: "tech2"));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("G123417654321|E0206000", result.Unit!.ComponentIdentity);
        Assert.Equal(result.Unit.ComponentIdentity, result.Unit.UnitNumber);
        Assert.NotEmpty(result.Unit.RawScans);

        var dup = await manual.CreateAsync(new ManualComponentEntryRequest(
            DonationNumber: "G123417654321",
            AboRhdCode: "DEMO",
            ProductDescriptionCode: "E0206",
            CollectionTypeCode: "0",
            DivisionCode: "00",
            ExtendedDivisionCode: null,
            ExpirationLocal: _factory.Clock.UtcNow.AddDays(30),
            ExpirationHasExplicitTime: false,
            ProductTypeId: productTypeId,
            SecondVerifier: "tech2"));

        Assert.False(dup.Succeeded);
        Assert.Contains(IsbtErrorCodes.ComponentDuplicate, dup.Error);
    }

    [Fact]
    public async Task SameDin_DifferentDivision_Allowed()
    {
        await using var context = _factory.Create();
        await SeedLookupsAsync(context);
        var productTypeId = (await context.ProductTypes.FirstAsync(t => t.ProductCode == "RBC-ISBT")).Id;
        var manual = CreateManual(context);
        var dinCheck = new PlaceholderDinCheckCharacterValidator();
        var check = dinCheck.ComputeCheckCharacter("G123417654322");

        var a = await manual.CreateAsync(MakeRequest(check, "G123417654322", "00", productTypeId));
        Assert.True(a.Succeeded, a.Error);

        var b = await manual.CreateAsync(MakeRequest(check, "G123417654322", "01", productTypeId));
        Assert.True(b.Succeeded, b.Error);
        Assert.NotEqual(a.Unit!.ComponentIdentity, b.Unit!.ComponentIdentity);
    }

    [Fact]
    public async Task ScanSession_FourQuadrants_ReceivesComponent()
    {
        await using var context = _factory.Create();
        await SeedLookupsAsync(context);
        var productTypeId = (await context.ProductTypes.FirstAsync(t => t.ProductCode == "RBC-ISBT")).Id;
        var sessions = CreateScanSession(context);

        var start = await sessions.StartAsync(new StartScanSessionRequest());
        Assert.True(start.Succeeded);

        var key = start.Value!.SessionKey;
        Assert.True((await sessions.AddScanAsync(new AddScanRequest(key, "=G99991765432100"))).Succeeded);
        Assert.True((await sessions.AddScanAsync(new AddScanRequest(key, "=%DEMO"))).Succeeded);
        Assert.True((await sessions.AddScanAsync(new AddScanRequest(key, "=<E0206000"))).Succeeded);

        var exp = ExpirationParser.FromLocalDateTime(_factory.Clock.UtcNow.AddDays(20), hasExplicitTime: true);
        Assert.True(exp.Success);
        Assert.True((await sessions.AddScanAsync(new AddScanRequest(key, exp.Value!.Sanitized))).Succeeded);

        var complete = await sessions.CompleteAsync(new CompleteScanSessionRequest(
            key, productTypeId, ReleaseToAvailable: true, SecondVerifier: "tech2"));
        Assert.True(complete.Succeeded, complete.Error);
        Assert.Equal(UnitStatus.Available, complete.Unit!.Status);
        Assert.Equal(4, complete.Unit.RawScans.Count);
    }

    private ManualComponentEntryRequest MakeRequest(char check, string din13, string division, long productTypeId) =>
        new(
            DonationNumber: $"{din13}{check}",
            AboRhdCode: "DEMO",
            ProductDescriptionCode: "E0206",
            CollectionTypeCode: "0",
            DivisionCode: division,
            ExtendedDivisionCode: null,
            ExpirationLocal: _factory.Clock.UtcNow.AddDays(30),
            ExpirationHasExplicitTime: false,
            ProductTypeId: productTypeId,
            ReleaseToAvailable: true,
            SecondVerifier: "tech2");

    private ComponentIdentityCorrectionService CreateCorrection(BloodBankDbContext context)
    {
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        return new ComponentIdentityCorrectionService(
            new InventoryRepository(context),
            new EfRepository<BloodComponentIdentityCorrection>(context),
            lookups,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser));
    }

    [Fact]
    public async Task IdentityCorrection_UnknownProductCode_Fails()
    {
        await using var context = _factory.Create();
        await SeedLookupsAsync(context);
        var productTypeId = (await context.ProductTypes.FirstAsync(t => t.ProductCode == "RBC-ISBT")).Id;
        var manual = CreateManual(context);
        const string din13 = "G555517654399";
        var dinCheck = new PlaceholderDinCheckCharacterValidator();
        var check = dinCheck.ComputeCheckCharacter(din13);

        var created = await manual.CreateAsync(new ManualComponentEntryRequest(
            DonationNumber: $"{din13}{check}",
            AboRhdCode: "DEMO",
            ProductDescriptionCode: "E0206",
            CollectionTypeCode: "0",
            DivisionCode: "00",
            ExtendedDivisionCode: null,
            ExpirationLocal: _factory.Clock.UtcNow.AddDays(30),
            ExpirationHasExplicitTime: false,
            ProductTypeId: productTypeId,
            SecondVerifier: "tech2"));
        Assert.True(created.Succeeded, created.Error);

        var correction = CreateCorrection(context);
        var result = await correction.CorrectAsync(new CorrectIdentityRequest(
            created.Unit!.Id,
            "ProductCodeData",
            "EXXXX000",
            "Wrong product scanned"));

        Assert.False(result.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnknownProductCode, result.Error);
    }
}
