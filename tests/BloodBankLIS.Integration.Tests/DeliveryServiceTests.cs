using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class DeliveryServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public DeliveryServiceTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_StampsReceivedTime_AndRejectsOrderedAfterReceipt()
    {
        await using var context = _factory.Create();
        var supplierId = await SeedSupplierAsync(context);
        var service = CreateDeliveryService(context);

        var created = await service.CreateAsync(new CreateDeliveryRequest(
            supplierId,
            _factory.Clock.UtcNow.AddHours(-3),
            "PL-100",
            [new SaveDeliveryLineRequest(ComponentClass.RedBloodCells, 4)]));

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(_factory.Clock.UtcNow, created.Value!.ReceivedUtc);
        Assert.Equal(DeliveryStatus.Open, created.Value.Status);
        Assert.Equal(4, Assert.Single(created.Value.Lines).ExpectedQuantity);
        Assert.Equal(0, created.Value.Lines[0].ReceivedQuantity);

        var future = await service.CreateAsync(new CreateDeliveryRequest(
            supplierId,
            _factory.Clock.UtcNow.AddHours(2),
            null,
            [new SaveDeliveryLineRequest(ComponentClass.RedBloodCells, 1)]));
        Assert.False(future.Succeeded);
    }

    [Fact]
    public async Task Create_RequiresALine_AndRejectsDuplicateProductType()
    {
        await using var context = _factory.Create();
        var supplierId = await SeedSupplierAsync(context, "TWO");
        var service = CreateDeliveryService(context);

        var empty = await service.CreateAsync(new CreateDeliveryRequest(
            supplierId, _factory.Clock.UtcNow.AddHours(-1), null, []));
        Assert.False(empty.Succeeded);

        var duplicate = await service.CreateAsync(new CreateDeliveryRequest(
            supplierId,
            _factory.Clock.UtcNow.AddHours(-1),
            null,
            [new SaveDeliveryLineRequest(ComponentClass.RedBloodCells, 1), new SaveDeliveryLineRequest(ComponentClass.RedBloodCells, 2)]));
        Assert.False(duplicate.Succeeded);
    }

    [Fact]
    public async Task BlankSupplierName_ShowsTheSupplierIdAndDoesNotWriteItOnTheUnit()
    {
        await using var context = _factory.Create();
        await SeedProductCodeAsync(context);
        var supplier = new BloodSupplier
        {
            Code = "BLANKSUP",
            Name = "   ",
            Registration = "REG-BLANKSUP",
            IsActive = true
        };
        context.BloodSuppliers.Add(supplier);
        await context.SaveChangesAsync();

        var productTypeId = await SeedProductAsync(context, "RBC-DEL-BLANK");
        var deliveries = CreateDeliveryService(context);
        var created = await deliveries.CreateAsync(new CreateDeliveryRequest(
            supplier.Id,
            _factory.Clock.UtcNow.AddHours(-1),
            null,
            [new SaveDeliveryLineRequest(ComponentClass.RedBloodCells, 1)]));
        Assert.True(created.Succeeded, created.Error);
        var deliveryId = created.Value!.Id;

        var open = Assert.Single(await deliveries.ListOpenAsync(), d => d.Id == deliveryId);
        Assert.Equal("   ", open.SupplierName);
        Assert.Equal($"Supplier {supplier.Id}", open.SupplierNameLabel);
        Assert.DoesNotContain("BLANKSUP", open.SupplierNameLabel);

        var detail = await deliveries.GetAsync(deliveryId);
        Assert.Equal("   ", detail!.SupplierName);
        Assert.Equal($"Supplier {supplier.Id}", detail.SupplierNameLabel);

        var receive = await deliveries.ResolveForReceiveAsync(deliveryId, productTypeId);
        Assert.True(receive.Succeeded, receive.Error);
        Assert.Equal("   ", receive.Value!.SupplierName);
        Assert.DoesNotContain("Supplier", receive.Value.SupplierName);

        var inventory = CreateInventory(context, deliveries);
        var unit = await inventory.ReceiveUnitAsync(UnitRequest("U-BLANK-SUP", productTypeId, deliveryId));
        Assert.True(unit.Succeeded, unit.Error);
        Assert.Equal("   ", unit.Unit!.Supplier);
        Assert.Equal("   ", unit.Unit.CollectionFacility);
    }

    [Fact]
    public async Task Receive_RequiresOpenDeliveryLine_AndCountsUnits()
    {
        await using var context = _factory.Create();
        await SeedProductCodeAsync(context);
        var supplierId = await SeedSupplierAsync(context, "RCV");
        var productTypeId = await SeedProductAsync(context, "RBC-DEL-RCV");
        var otherProductId = await SeedProductAsync(context, "PLS-DEL-RCV", ComponentClass.Plasma);
        var sameClassId = await SeedProductAsync(context, "RBC-DEL-RCV-2");
        var deliveries = CreateDeliveryService(context);
        var inventory = CreateInventory(context, deliveries);

        var missing = await inventory.ReceiveUnitAsync(UnitRequest("U-NO-DEL", productTypeId));
        Assert.False(missing.Succeeded);
        Assert.Contains("delivery", missing.Error, StringComparison.OrdinalIgnoreCase);

        var created = await deliveries.CreateAsync(new CreateDeliveryRequest(
            supplierId,
            _factory.Clock.UtcNow.AddHours(-1),
            "PACK-9",
            [new SaveDeliveryLineRequest(ComponentClass.RedBloodCells, 1)]));
        Assert.True(created.Succeeded, created.Error);
        var deliveryId = created.Value!.Id;

        var wrongType = await inventory.ReceiveUnitAsync(UnitRequest("U-WRONG", otherProductId, deliveryId));
        Assert.False(wrongType.Succeeded);

        var first = await inventory.ReceiveUnitAsync(UnitRequest("U-OK-1", productTypeId, deliveryId));
        Assert.True(first.Succeeded, first.Error);
        Assert.Equal(deliveryId, first.Unit!.DeliveryId);
        Assert.Equal("Receive Supplier", first.Unit.Supplier);
        Assert.Equal("PACK-9", first.Unit.ShipmentId);

        var over = await inventory.ReceiveUnitAsync(UnitRequest("U-OK-2", sameClassId, deliveryId));
        Assert.True(over.Succeeded, over.Error);

        var detail = await deliveries.GetAsync(deliveryId);
        var line = Assert.Single(detail!.Lines);
        Assert.Equal(ComponentClass.RedBloodCells, line.ComponentClass);
        Assert.Equal("RBC", line.ProductLabel);
        Assert.Equal(2, line.ReceivedQuantity);

        var completed = await deliveries.CompleteAsync(deliveryId);
        Assert.True(completed.Succeeded, completed.Error);

        var after = await inventory.ReceiveUnitAsync(UnitRequest("U-CLOSED", productTypeId, deliveryId));
        Assert.False(after.Succeeded);
        Assert.Contains("open", after.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IsbtReceive_RejectsMissingDelivery()
    {
        await using var context = _factory.Create();
        await SeedProductCodeAsync(context);
        var productTypeId = await SeedProductAsync(context, "RBC-DEL-ISBT");
        var deliveries = CreateDeliveryService(context);
        var inventory = CreateInventory(context, deliveries);
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        var manual = new ManualComponentEntryService(
            lookups,
            new Iso7064Mod37_2DinCheckCharacterValidator(),
            inventory,
            _factory.Clock,
            _factory.CurrentUser,
            permissions: null);

        const string din13 = "G555517654321";
        var check = new Iso7064Mod37_2DinCheckCharacterValidator().ComputeCheckCharacter(din13);
        var result = await manual.CreateAsync(new ManualComponentEntryRequest(
            $"{din13}{check}",
            "DEMO",
            "E0206",
            "0",
            "00",
            null,
            _factory.Clock.UtcNow.AddDays(30),
            false,
            productTypeId,
            SecondVerifier: "tech2",
            ReceiveTemperatureCelsius: 4.0m));

        Assert.False(result.Succeeded);
        Assert.Contains("delivery", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private DeliveryService CreateDeliveryService(BloodBankDbContext context) =>
        new(
            new EfRepository<BloodDelivery>(context),
            new EfRepository<BloodDeliveryLine>(context),
            new EfRepository<BloodSupplier>(context),
            new EfRepository<ProductType>(context),
            new EfRepository<BloodUnit>(context),
            new DeliveryLineStore(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser));

    private InventoryService CreateInventory(BloodBankDbContext context, DeliveryService deliveries)
    {
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        return new InventoryService(
            new InventoryRepository(context),
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            lookups,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            deliveries: deliveries);
    }

    private ReceiveUnitRequest UnitRequest(string unitNumber, long productTypeId, long? deliveryId = null) =>
        new(
            unitNumber,
            productTypeId,
            AboGroup.O,
            RhType.Positive,
            _factory.Clock.UtcNow.AddDays(30),
            Isbt128ProductCode: "E0206",
            SecondVerifier: "tech2",
            ReceiveTemperatureCelsius: 4.0m,
            DeliveryId: deliveryId);

    private static async Task<long> SeedSupplierAsync(BloodBankDbContext context, string code = "RCVS")
    {
        var existing = await context.BloodSuppliers.FirstOrDefaultAsync(s => s.Code == code);
        if (existing is not null)
        {
            return existing.Id;
        }

        var supplier = new BloodSupplier
        {
            Code = code,
            Name = "Receive Supplier",
            Registration = "REG-" + code,
            IsActive = true
        };
        context.BloodSuppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier.Id;
    }

    private static async Task<long> SeedProductAsync(
        BloodBankDbContext context,
        string code = "RBC-DEL",
        ComponentClass component = ComponentClass.RedBloodCells)
    {
        var existing = await context.ProductTypes.FirstOrDefaultAsync(t => t.ProductCode == code);
        if (existing is not null)
        {
            return existing.Id;
        }

        var type = new ProductType
        {
            ProductCode = code,
            Name = code,
            ComponentClass = component,
            RequiresCrossmatch = true
        };
        context.ProductTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
    }

    private static async Task SeedProductCodeAsync(BloodBankDbContext context)
    {
        if (await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"))
        {
            return;
        }

        context.IsbtProductCodes.Add(new IsbtProductCode
        {
            ProductDescriptionCode = "E0206",
            Description = "RED BLOOD CELLS",
            ComponentClass = nameof(ComponentClass.RedBloodCells),
            AttributesJson = "[]",
            StandardVersion = UsSupplierProductCodeSeed.StandardVersion,
            IsPlaceholder = true
        });
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

        await context.SaveChangesAsync();
    }
}
