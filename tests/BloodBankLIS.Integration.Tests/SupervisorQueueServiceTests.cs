using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class SupervisorQueueServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SupervisorQueueServiceTests(SqliteContextFactory factory) => _factory = factory;

    private static SupervisorQueueService CreateService(
        ICurrentUser user,
        IPermissionEvaluator? permissions = null,
        InventoryService? inventory = null) =>
        new(user, inventory: inventory, permissions: permissions);

    private InventoryService CreateInventory(BloodBankDbContext context, IPermissionEvaluator? permissions = null)
    {
        var repository = new InventoryRepository(context);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser);
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        var retype = new ProductRetypeService(
            repository,
            new EfRepository<ProductRetypeResult>(context),
            new EfRepository<TestDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit);
        return new InventoryService(
            repository,
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            lookups,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            new EfRepository<User>(context),
            new FacilityPolicyService(new EfRepository<SystemSetting>(context)),
            new EfRepository<Patient>(context),
            productTypes: new EfRepository<ProductType>(context),
            permissions: permissions,
            retype: retype,
            retypeResults: new EfRepository<ProductRetypeResult>(context));
    }

    private async Task EnsureSecondVerifierAsync(string userName = "tech2")
    {
        await using var context = _factory.Create();
        if (await context.Users.AnyAsync(u => u.UserName == userName))
        {
            return;
        }

        context.Users.Add(new User
        {
            UserName = userName,
            DisplayName = "Tech Two",
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    private async Task EnsureProductCodesAsync()
    {
        await using var context = _factory.Create();
        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"))
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0206",
                Description = "RED BLOOD CELLS|CPDA-1/450mL/refg|Irradiated",
                ComponentClass = nameof(ComponentClass.RedBloodCells),
                AttributesJson = "[]",
                StandardVersion = UsSupplierProductCodeSeed.StandardVersion,
                IsPlaceholder = true
            });
            await context.SaveChangesAsync();
        }
    }

    private async Task<long> EnsureProductTypeAsync()
    {
        await EnsureSecondVerifierAsync();
        await EnsureProductCodesAsync();
        await using var context = _factory.Create();
        var existing = await context.ProductTypes.FirstOrDefaultAsync(t => t.ProductCode == "RBC-TEST");
        if (existing is not null)
        {
            return existing.Id;
        }

        var type = new ProductType
        {
            ProductCode = "RBC-TEST",
            Name = "Test RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        context.ProductTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
    }

    private ReceiveUnitRequest NewUnitRequest(string unitNumber, long productTypeId) =>
        new(unitNumber, productTypeId, AboGroup.O, RhType.Positive,
            _factory.Clock.UtcNow.AddDays(30),
            Isbt128ProductCode: "E0206",
            SecondVerifier: "tech2",
            ReceiveTemperatureCelsius: 4.0m);

    [Fact]
    public async Task List_WithoutSupervisorPermissions_IsHardStopped()
    {
        var denied = await CreateService(
                _factory.CurrentUser,
                permissions: new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .ListAsync();
        Assert.False(denied.Succeeded);
        Assert.Equal(SupervisorQueueRule.EvaluateAccess(false, false, false).Message, denied.Error);
    }

    [Fact]
    public async Task List_WithInventoryRelease_IncludesQuarantineUnit()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var inventory = CreateInventory(
            context,
            new FixedPermissionEvaluator(2, PermissionCodes.InventoryReceive, PermissionCodes.InventoryRelease));
        var unitNumber = $"U-SUPQ{Guid.NewGuid():N}"[..16];
        var received = await inventory.ReceiveUnitAsync(NewUnitRequest(unitNumber, productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var queue = await CreateService(
                _factory.CurrentUser,
                permissions: new FixedPermissionEvaluator(2, PermissionCodes.InventoryRelease),
                inventory: inventory)
            .ListAsync();
        Assert.True(queue.Succeeded, queue.Error);
        var row = Assert.Single(queue.Value!, i => i.ContextId == received.Unit!.Id);
        Assert.Equal(SupervisorQueueRule.KindQuarantineRelease, row.Kind);
        Assert.Equal("Release", row.NextAction);
        Assert.Contains(unitNumber, row.Title, StringComparison.Ordinal);
        Assert.Equal($"/inventory/units/{received.Unit.Id}", row.Href);
    }

    [Fact]
    public async Task List_WithResultVerifyOnly_OmitsQuarantine()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var inventory = CreateInventory(
            context,
            new FixedPermissionEvaluator(2, PermissionCodes.InventoryReceive, PermissionCodes.InventoryRelease));
        var received = await inventory.ReceiveUnitAsync(
            NewUnitRequest($"U-SUPV{Guid.NewGuid():N}"[..16], productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var queue = await CreateService(
                _factory.CurrentUser,
                permissions: new FixedPermissionEvaluator(2, PermissionCodes.ResultVerify),
                inventory: inventory)
            .ListAsync();
        Assert.True(queue.Succeeded, queue.Error);
        Assert.DoesNotContain(queue.Value!, i => i.ContextId == received.Unit!.Id);
    }
}
