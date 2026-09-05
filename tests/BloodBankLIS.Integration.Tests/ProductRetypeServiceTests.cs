using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ProductRetypeServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ProductRetypeServiceTests(SqliteContextFactory factory) => _factory = factory;

    private InventoryService Inventory(BloodBankDbContext context) =>
        new(
            new InventoryRepository(context),
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            new IsbtLookupCatalog(
                new EfRepository<IsbtAboRhdCode>(context),
                new EfRepository<IsbtProductCode>(context)),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser));

    private ProductRetypeService Retype(
        BloodBankDbContext context,
        ICurrentUser? user = null,
        IPermissionEvaluator? permissions = null) =>
        new(
            new InventoryRepository(context),
            new EfRepository<ProductRetypeResult>(context),
            new EfRepository<TestDefinition>(context),
            context,
            _factory.Clock,
            user ?? _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, user ?? _factory.CurrentUser),
            permissions: permissions);

    private ProductRetypeService RetypeWithPolicy(BloodBankDbContext context, ICurrentUser? user = null) =>
        new(
            new InventoryRepository(context),
            new EfRepository<ProductRetypeResult>(context),
            new EfRepository<TestDefinition>(context),
            context,
            _factory.Clock,
            user ?? _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, user ?? _factory.CurrentUser),
            new FacilityPolicyService(new EfRepository<SystemSetting>(context)));

    private static RecordProductRetypeRequest MatchOPos() => new(
        AboGroup.O,
        null,
        new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "0",
            [AboRhPanelSubtestCodes.AntiB] = "0"
        });

    private static ICurrentUser Verifier => new TestCurrentUser("tech-verify", "WORKSTATION-2");

    private async Task<(long ProductTypeId, long UnitId)> SeedReceivedUnitAsync(
        string unitNumber,
        AboGroup abo,
        RhType rh,
        bool requiresRetype = true)
    {
        await using var context = _factory.Create();
        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"))
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0206",
                Description = "RED BLOOD CELLS",
                ComponentClass = nameof(ComponentClass.RedBloodCells),
                AttributesJson = "[]",
                StandardVersion = UsSupplierProductCodeSeed.StandardVersion,
                IsPlaceholder = true
            });
        }

        if (!await context.TestDefinitions.AnyAsync(t => t.Code == AboRhRetypeRule.TestCode))
        {
            context.TestDefinitions.Add(new TestDefinition
            {
                Code = AboRhRetypeRule.TestCode,
                Name = "ABO/Rh Retype",
                Category = TestCategory.AboRhRetype,
                ResultValueType = ResultValueType.AboRh,
                IsActive = true,
                IsDraft = false,
                Version = 1
            });
        }

        var type = new ProductType
        {
            ProductCode = $"RBC-{unitNumber}",
            Name = "Retype product",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresRetype = requiresRetype
        };
        context.ProductTypes.Add(type);
        await context.SaveChangesAsync();

        var receive = await Inventory(context).ReceiveUnitAsync(new ReceiveUnitRequest(
            unitNumber, type.Id, abo, rh, _factory.Clock.UtcNow.AddDays(30),
            Isbt128ProductCode: "E0206",
            SecondVerifier: "tech2",
            ReceiveTemperatureCelsius: 4.0m));
        Assert.True(receive.Succeeded, receive.Error);
        return (type.Id, receive.Unit!.Id);
    }

    [Fact]
    public async Task MatchingRetype_StaysReceivedUntilVerifiedBySecondUser()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-MATCH", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var entered = await Retype(context).RecordAsync(unitId, MatchOPos());

        Assert.True(entered.Succeeded);
        Assert.Equal(UnitStatus.Received, entered.Value!.Status);
        Assert.True(entered.Value.CanVerify);
        Assert.Equal(ResultStatus.Entered, entered.Value.Latest!.Status);

        var self = await Retype(context).VerifyAsync(unitId, entered.Value.Latest.Id);
        Assert.False(self.Succeeded);
        Assert.Contains(self.Evaluation!.HardStops, r => r.Code == SelfVerifyRule.Code);

        var verified = await Retype(context, Verifier).VerifyAsync(unitId, entered.Value.Latest.Id);
        Assert.True(verified.Succeeded, verified.Error);
        Assert.Equal(UnitStatus.Available, verified.Value!.Status);
        Assert.Equal("tech-verify", verified.Value.Latest!.VerifiedBy);

        var unit = await context.BloodUnits.AsNoTracking().FirstAsync(u => u.Id == unitId);
        Assert.Equal(UnitStatus.Available, unit.Status);
        Assert.Contains(await context.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync(),
            h => h.ToStatus == UnitStatus.Available && h.Reason != null && h.Reason.Contains("retype", StringComparison.OrdinalIgnoreCase));

        var retypeId = entered.Value.Latest.Id;
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.Result && a.EntityType == nameof(ProductRetypeResult) && a.EntityId == retypeId));
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.Verify && a.EntityType == nameof(ProductRetypeResult) && a.EntityId == retypeId));
        Assert.Contains(
            await context.AuditEvents
                .Where(a => a.EventType == AuditEventType.ProductStatus && a.EntityType == nameof(BloodUnit) && a.EntityId == unitId)
                .ToListAsync(),
            a => a.Reason != null && a.Reason.Contains("retype", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Verify_WithoutResultVerify_IsHardStopped()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-PERM", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var entered = await Retype(context).RecordAsync(unitId, MatchOPos());
        Assert.True(entered.Succeeded);

        var denied = await Retype(
            context,
            Verifier,
            new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter)).VerifyAsync(unitId, entered.Value!.Latest!.Id);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ResultAuthorizationRule.VerifyCode);
        Assert.Equal(UnitStatus.Received, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await Retype(
            context,
            Verifier,
            new FixedPermissionEvaluator(1, PermissionCodes.ResultVerify)).VerifyAsync(unitId, entered.Value.Latest.Id);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Available, allowed.Value!.Status);
    }

    [Fact]
    public async Task Record_WithoutResultEnter_IsHardStopped()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-ENT", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var denied = await Retype(
            context,
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.ResultVerify)).RecordAsync(unitId, MatchOPos());
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ResultAuthorizationRule.EnterCode);
        Assert.False(await context.ProductRetypeResults.AnyAsync(r => r.BloodProductId == unitId));

        var allowed = await Retype(
            context,
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter)).RecordAsync(unitId, MatchOPos());
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(ResultStatus.Entered, allowed.Value!.Latest!.Status);
    }

    [Fact]
    public async Task MismatchRetype_MovesReceivedToQuarantine()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-MISMATCH", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var result = await Retype(context).RecordAsync(unitId, new RecordProductRetypeRequest(
            AboGroup.A,
            null,
            new Dictionary<string, string>
            {
                [AboRhPanelSubtestCodes.AntiA] = "4+",
                [AboRhPanelSubtestCodes.AntiB] = "0"
            }));

        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.Received, result.Value!.Status);
        Assert.False(result.Value.Latest!.MatchesLabel);
        Assert.NotNull(result.Value.Latest.DiscrepancyDetail);

        var verified = await Retype(context, Verifier).VerifyAsync(unitId, result.Value.Latest.Id);
        Assert.True(verified.Succeeded, verified.Error);
        Assert.Equal(UnitStatus.Quarantine, verified.Value!.Status);

        var unit = await context.BloodUnits.AsNoTracking().FirstAsync(u => u.Id == unitId);
        Assert.Equal(UnitStatus.Quarantine, unit.Status);
        Assert.Contains("discrepancy", unit.QuarantineReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PendingList_IncludesOnlyReceivedRetypeUnits()
    {
        var (_, pendingId) = await SeedReceivedUnitAsync("RT-PEND", AboGroup.B, RhType.Negative);
        await SeedReceivedUnitAsync("RT-SKIP", AboGroup.O, RhType.Positive, requiresRetype: false);

        await using var context = _factory.Create();
        var pending = await Retype(context).ListPendingAsync();
        Assert.Contains(pending, u => u.UnitId == pendingId);
        Assert.DoesNotContain(pending, u => u.UnitNumber == "RT-SKIP");
    }

    [Fact]
    public async Task IsbtReleaseToAvailable_IgnoredWhenRequiresRetype()
    {
        await using var context = _factory.Create();
        if (!await context.IsbtAboRhdCodes.AnyAsync(c => c.Code == "DEMO"))
        {
            context.IsbtAboRhdCodes.Add(new IsbtAboRhdCode
            {
                Code = "DEMO",
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsPlaceholder = true
            });
        }

        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"))
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
        var type = new ProductType
        {
            ProductCode = "RBC-ISBT-RT",
            Name = "ISBT retype RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresRetype = true
        };
        context.ProductTypes.Add(type);
        await context.SaveChangesAsync();

        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        var inventory = Inventory(context);
        var dinCheck = new PlaceholderDinCheckCharacterValidator();
        var manual = new ManualComponentEntryService(lookups, dinCheck, inventory, _factory.Clock, _factory.CurrentUser);
        var check = dinCheck.ComputeCheckCharacter("G123417654399");

        var result = await manual.CreateAsync(new ManualComponentEntryRequest(
            DonationNumber: $"G1234 17 654399 {check}",
            AboRhdCode: "DEMO",
            ProductDescriptionCode: "E0206",
            CollectionTypeCode: "0",
            DivisionCode: "00",
            ExtendedDivisionCode: null,
            ExpirationLocal: _factory.Clock.UtcNow.AddDays(30),
            ExpirationHasExplicitTime: false,
            ProductTypeId: type.Id,
            ReleaseToAvailable: true,
            SecondVerifier: "tech2",
            ReceiveTemperatureCelsius: 4.0m));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(UnitStatus.Received, result.Unit!.Status);
    }

    [Fact]
    public async Task SameUserMayVerifyWhenRetypeSelfVerifyPolicyIsOff()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-SELF-OK", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        context.SystemSettings.Add(new SystemSetting
        {
            Key = FacilityPolicyKeys.BlockRetypeSelfVerify,
            Value = "false",
            Category = "Inventory",
            Description = "Test override"
        });
        await context.SaveChangesAsync();

        var entered = await RetypeWithPolicy(context).RecordAsync(unitId, MatchOPos());
        Assert.True(entered.Succeeded);

        var verified = await RetypeWithPolicy(context).VerifyAsync(unitId, entered.Value!.Latest!.Id);
        Assert.True(verified.Succeeded, verified.Error);
        Assert.Equal(UnitStatus.Available, verified.Value!.Status);
        Assert.Equal(_factory.CurrentUser.UserName, verified.Value.Latest!.VerifiedBy);
    }
}
