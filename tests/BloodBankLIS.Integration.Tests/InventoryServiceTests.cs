using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Audit;
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

public class InventoryServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InventoryServiceTests(SqliteContextFactory factory) => _factory = factory;

    private InventoryService CreateService(BloodBankDbContext context)
    {
        var repository = new InventoryRepository(context);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser);
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
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
            new FacilityPolicyService(new EfRepository<SystemSetting>(context)));
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
        }

        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0336"))
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0336",
                Description = "RED BLOOD CELLS|CPD>AS1/500mL/refg|ResLeu:<5E6",
                ComponentClass = nameof(ComponentClass.RedBloodCells),
                AttributesJson = "[]",
                StandardVersion = UsSupplierProductCodeSeed.StandardVersion,
                IsPlaceholder = true
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task<long> EnsureProductTypeAsync()
    {
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

    private ReceiveUnitRequest NewUnitRequest(
        string unitNumber,
        long productTypeId,
        DateTime? expires = null,
        string? productCode = "E0206") =>
        new(unitNumber, productTypeId, AboGroup.O, RhType.Positive,
            expires ?? _factory.Clock.UtcNow.AddDays(30),
            Isbt128ProductCode: productCode);

    [Fact]
    public async Task ReceiveUnit_CreatesQuarantineUnit_WithInitialHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.ReceiveUnitAsync(NewUnitRequest("U-INTAKE-1", productTypeId));

            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Quarantine, result.Unit!.Status);
            Assert.Equal("E0206", result.Unit.ProductDescriptionCode);
            unitId = result.Unit.Id;
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            var initial = Assert.Single(history);
            Assert.Null(initial.FromStatus);
            Assert.Equal(UnitStatus.Quarantine, initial.ToStatus);
        }
    }

    [Fact]
    public async Task ReceiveUnit_MissingProductCode_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-NOPDC", productTypeId, productCode: null));

        Assert.False(result.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnknownProductCode, result.Error);
    }

    [Fact]
    public async Task ReceiveUnit_UnknownProductCode_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-BADPDC", productTypeId, productCode: "EXXXX"));

        Assert.False(result.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnknownProductCode, result.Error);
    }

    [Fact]
    public async Task ReceiveUnit_EightCharProductCodeData_StoresComponents()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-8CHAR", productTypeId, productCode: "E0336000"));

        Assert.True(result.Succeeded);
        Assert.Equal("E0336", result.Unit!.ProductDescriptionCode);
        Assert.Equal("E0336000", result.Unit.ProductCodeData);
        Assert.Equal("0", result.Unit.CollectionTypeCode);
        Assert.Equal("00", result.Unit.DivisionCode);
    }

    [Fact]
    public async Task ReceiveUnit_DuplicateUnitNumber_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        await service.ReceiveUnitAsync(NewUnitRequest("U-DUP", productTypeId));

        var second = await service.ReceiveUnitAsync(NewUnitRequest("U-DUP", productTypeId));
        Assert.False(second.Succeeded);
        Assert.Contains("already exists", second.Error);
    }

    [Fact]
    public async Task ReceiveUnit_PastExpiration_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(
            NewUnitRequest("U-PASTEXP", productTypeId, _factory.Clock.UtcNow.AddHours(-1)));

        Assert.False(result.Succeeded);
        Assert.Contains("future", result.Error);
    }

    [Fact]
    public async Task Release_QuarantineToAvailable_AppendsHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var received = await service.ReceiveUnitAsync(NewUnitRequest("U-REL", productTypeId));
            unitId = received.Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.ReleaseFromQuarantineAsync(unitId, "tech2");
            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Available, result.Unit!.Status);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Equal(2, history.Count);
            Assert.Contains(history, h => h.FromStatus == UnitStatus.Quarantine && h.ToStatus == UnitStatus.Available);
            Assert.Contains(history, h => h.Reason != null && h.Reason.Contains("tech2"));
        }
    }

    [Fact]
    public async Task Release_WithoutSecondVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-REL-NO2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var result = await CreateService(ctx).ReleaseFromQuarantineAsync(unitId);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == QuarantineReleaseVerifierRule.Code);
    }

    [Fact]
    public async Task Release_SameUserOrUnknownVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-REL-BAD2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var service = CreateService(ctx);
        var same = await service.ReleaseFromQuarantineAsync(unitId, "tech-test");
        Assert.Contains(same.Evaluation!.HardStops, r => r.Code == QuarantineReleaseVerifierRule.Code);

        var unknown = await service.ReleaseFromQuarantineAsync(unitId, "not-a-user");
        Assert.Contains(unknown.Evaluation!.HardStops, r => r.Code == SecondVerifierDirectoryRule.Code);
    }

    [Fact]
    public async Task Hold_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-NR", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).HoldAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("hold reason is required", result.Error);
        }
    }

    [Fact]
    public async Task Hold_ThenRelease_ReturnsAvailable_AndClearsReason()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-REL", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var held = await CreateService(context).HoldAsync(unitId, "Pending packing slip");
            Assert.True(held.Succeeded);
            Assert.Equal(UnitStatus.OnHold, held.Unit!.Status);
            Assert.Equal("Pending packing slip", held.Unit.HoldReason);
        }

        await using (var context = _factory.Create())
        {
            var released = await CreateService(context).ReleaseFromHoldAsync(unitId);
            Assert.True(released.Succeeded);
            Assert.Equal(UnitStatus.Available, released.Unit!.Status);
            Assert.Null(released.Unit.HoldReason);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Contains(history, h => h.FromStatus == UnitStatus.Available && h.ToStatus == UnitStatus.OnHold);
            Assert.Contains(history, h => h.FromStatus == UnitStatus.OnHold && h.ToStatus == UnitStatus.Available);
        }
    }

    [Fact]
    public async Task Hold_FromQuarantine_IsBlocked()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-Q", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).HoldAsync(unitId, "Should not bypass quarantine");
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Evaluation);
            Assert.True(result.Evaluation!.IsHardStopped);
        }
    }

    [Fact]
    public async Task ReleaseFromHold_WhenNotOnHold_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-NA", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).ReleaseFromHoldAsync(unitId);
            Assert.False(result.Succeeded);
            Assert.Contains("operational hold", result.Error);
        }
    }

    [Fact]
    public async Task Discard_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task Discard_SetsStatus_AndWritesDiscardAudit()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "Bag integrity compromised");
            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Discarded, result.Unit!.Status);
        }

        await using (var verify = _factory.Create())
        {
            var discardAudit = await verify.AuditEvents
                .Where(a => a.EntityType == nameof(BloodUnit) && a.EntityId == unitId && a.EventType == AuditEventType.Discard)
                .SingleAsync();
            Assert.Equal("Bag integrity compromised", discardAudit.Reason);
        }
    }

    [Fact]
    public async Task Discard_TransfusedUnit_IsBlockedByTransitionGuard()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var unit = new BloodUnit
            {
                UnitNumber = "U-TX",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
                Status = UnitStatus.Transfused
            };
            context.BloodUnits.Add(unit);
            await context.SaveChangesAsync();
            unitId = unit.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "attempt");
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Evaluation);
            Assert.True(result.Evaluation!.IsHardStopped);
        }
    }

    [Fact]
    public async Task Transfer_ChangesLocation_AndAppendsHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        long toLocationId;

        await using (var context = _factory.Create())
        {
            var location = new InventoryLocation { Code = "LOC-XFER", Name = "Transfer Target", LocationType = LocationType.Refrigerator };
            context.InventoryLocations.Add(location);
            await context.SaveChangesAsync();
            toLocationId = location.Id;

            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-XFER", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).TransferAsync(unitId, toLocationId, "Move to issue fridge");
            Assert.True(result.Succeeded);
            Assert.Equal(toLocationId, result.Unit!.CurrentLocationId);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Contains(history, h => h.ToLocationId == toLocationId && h.FromStatus == h.ToStatus);
        }
    }

    [Fact]
    public async Task ExpireDueUnits_MovesOnlyPastDueUnits()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long pastDueId;
        long futureId;

        await using (var context = _factory.Create())
        {
            var pastDue = new BloodUnit
            {
                UnitNumber = "U-PASTDUE",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = UnitStatus.Available
            };
            var future = new BloodUnit
            {
                UnitNumber = "U-FUTURE",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(5),
                Status = UnitStatus.Available
            };
            context.BloodUnits.AddRange(pastDue, future);
            await context.SaveChangesAsync();
            pastDueId = pastDue.Id;
            futureId = future.Id;
        }

        await using (var context = _factory.Create())
        {
            var expired = await CreateService(context).ExpireDueUnitsAsync();
            Assert.True(expired >= 1);
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(UnitStatus.Expired, (await verify.BloodUnits.FindAsync(pastDueId))!.Status);
            Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(futureId))!.Status);
        }
    }

    [Fact]
    public async Task Search_FiltersByStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            await service.ReceiveUnitAsync(NewUnitRequest("U-SEARCH-Q", productTypeId)); // stays Quarantine
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var quarantined = await service.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Quarantine));
            Assert.Contains(quarantined, u => u.UnitNumber == "U-SEARCH-Q");
            Assert.All(quarantined, u => Assert.Equal(UnitStatus.Quarantine, u.Status));
        }
    }

    [Fact]
    public async Task ReceiveUnit_RequiresRetype_CreatesReceivedUnit()
    {
        await EnsureProductCodesAsync();
        long productTypeId;
        await using (var context = _factory.Create())
        {
            var type = new ProductType
            {
                ProductCode = "RBC-RETYPE",
                Name = "Retype RBC",
                ComponentClass = ComponentClass.RedBloodCells,
                RequiresRetype = true
            };
            context.ProductTypes.Add(type);
            await context.SaveChangesAsync();
            productTypeId = type.Id;
        }

        await using var receive = _factory.Create();
        var service = CreateService(receive);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-RETYPE-1", productTypeId));

        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.Received, result.Unit!.Status);
        var history = await receive.InventoryStatusHistory.Where(h => h.BloodProductId == result.Unit.Id).ToListAsync();
        var initial = Assert.Single(history);
        Assert.Equal(UnitStatus.Received, initial.ToStatus);
        Assert.Contains("retype", initial.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
