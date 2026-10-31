using System.Text;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Printing.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-301: supplier order from the par shortfall through submit, receive, export, and cancel.
public class SupplierOrderTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly FakeSignatures _signatures = new();

    public void Dispose() => _factory.Dispose();

    private SupplierOrderService Service(BloodBankDbContext c) =>
        new(new EfRepository<SupplierOrder>(c),
            new EfRepository<SupplierOrderLine>(c),
            new EfRepository<BloodSupplier>(c),
            new EfRepository<BloodDelivery>(c),
            new EfRepository<ProductType>(c),
            new StockParService(new EfRepository<StockParLevel>(c), new EfRepository<ProductType>(c), new InventoryRepository(c), c),
            c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            _signatures,
            new TabularReportRenderer());

    [Fact]
    public async Task Order_FromParShortfall_EditSubmitReceive()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var service = Service(c);

        var created = await service.CreateFromParAsync(new CreateSupplierOrderRequest(f.Supplier.Id));
        Assert.True(created.Succeeded, created.Error);
        var order = created.Value!;
        Assert.Equal(SupplierOrderStatus.Draft, order.Status);
        Assert.Equal("SO-20260530-001", order.OrderNumber);
        var line = Assert.Single(order.Lines);
        Assert.Equal((f.RedCells.Id, (AboGroup?)AboGroup.O, 2), (line.ProductTypeId, line.Abo, line.Quantity));

        var edited = await service.UpdateAsync(order.Id, new UpdateSupplierOrderRequest(
            [new SupplierOrderLineInput(f.RedCells.Id, AboGroup.O, 5), new SupplierOrderLineInput(f.Plasma.Id, null, 4)],
            "Deliver by 06:00"));
        Assert.True(edited.Succeeded, edited.Error);
        Assert.Equal(9, edited.Value!.UnitsOrdered);
        Assert.Equal("Deliver by 06:00", edited.Value.Notes);

        var removed = await service.UpdateAsync(order.Id, new UpdateSupplierOrderRequest(
            [new SupplierOrderLineInput(f.RedCells.Id, AboGroup.O, 5), new SupplierOrderLineInput(f.Plasma.Id, null, 0)], null));
        Assert.Equal(5, removed.Value!.UnitsOrdered);
        Assert.Single(removed.Value.Lines);
        Assert.False((await service.UpdateAsync(order.Id, new UpdateSupplierOrderRequest(
            [new SupplierOrderLineInput(f.RedCells.Id, AboGroup.O, 1000)], null))).Succeeded);

        var unsigned = await service.SubmitAsync(order.Id, new SubmitSupplierOrderRequest(999));
        Assert.False(unsigned.Succeeded);
        Assert.Contains(SupplierOrderRule.SignatureCode, unsigned.Error);

        var submitted = await service.SubmitAsync(order.Id, new SubmitSupplierOrderRequest(FakeSignatures.ValidId));
        Assert.True(submitted.Succeeded, submitted.Error);
        Assert.Equal(SupplierOrderStatus.Submitted, submitted.Value!.Status);
        Assert.Equal("tech-test", submitted.Value.SubmittedBy);
        Assert.Contains(FakeSignatures.ValidId, _signatures.Consumed);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(SupplierOrder) && e.SignatureId == FakeSignatures.ValidId));
        Assert.False((await service.UpdateAsync(order.Id, new UpdateSupplierOrderRequest([], null))).Succeeded);

        var wrongSupplier = await service.ReceiveAsync(order.Id, new ReceiveSupplierOrderRequest(f.OtherDelivery.Id));
        Assert.False(wrongSupplier.Succeeded);
        Assert.Contains(SupplierOrderRule.SupplierMismatchCode, wrongSupplier.Error);

        var received = await service.ReceiveAsync(order.Id, new ReceiveSupplierOrderRequest(f.Delivery.Id));
        Assert.True(received.Succeeded, received.Error);
        Assert.Equal(SupplierOrderStatus.Received, received.Value!.Status);
        Assert.Equal(f.Delivery.Id, received.Value.DeliveryId);
        Assert.False((await service.CancelAsync(order.Id, new CancelSupplierOrderRequest("too late"))).Succeeded);
    }

    [Fact]
    public async Task Export_CsvAndPdf_AreAudited()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var service = Service(c);
        var order = (await service.CreateFromParAsync(new CreateSupplierOrderRequest(f.Supplier.Id))).Value!;

        var csv = await service.ExportAsync(order.Id, "csv");
        Assert.True(csv.Succeeded, csv.Error);
        Assert.Equal($"{order.OrderNumber}.csv", csv.Value!.FileName);
        var text = Encoding.UTF8.GetString(csv.Value.Bytes, 3, csv.Value.Bytes.Length - 3);
        Assert.Equal("product_code,product,abo,quantity\r\nRBC-SO,Red cells,O,2\r\n", text);

        var pdf = await service.ExportAsync(order.Id, "pdf");
        Assert.True(pdf.Succeeded, pdf.Error);
        Assert.Equal("application/pdf", pdf.Value!.ContentType);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf.Value.Bytes, 0, 4));

        Assert.False((await service.ExportAsync(order.Id, "xml")).Succeeded);
        Assert.Equal(2, await c.AuditEvents.CountAsync(e => e.EntityType == nameof(SupplierOrder) && e.EventType == AuditEventType.Export));
    }

    [Fact]
    public async Task NothingBelowPar_InactiveSupplier_AndCancelWithoutReason_AreRejected()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var service = Service(c);
        var inactive = new BloodSupplier { Code = "OLD", Name = "Old supplier", Registration = "R-OLD", IsActive = false };
        c.BloodSuppliers.Add(inactive);
        await c.SaveChangesAsync();
        Assert.False((await service.CreateFromParAsync(new CreateSupplierOrderRequest(inactive.Id))).Succeeded);

        var order = (await service.CreateFromParAsync(new CreateSupplierOrderRequest(f.Supplier.Id))).Value!;
        Assert.False((await service.CancelAsync(order.Id, new CancelSupplierOrderRequest(" "))).Succeeded);
        var cancelled = await service.CancelAsync(order.Id, new CancelSupplierOrderRequest("Covered by a standing order"));
        Assert.Equal(SupplierOrderStatus.Cancelled, cancelled.Value!.Status);

        var level = await c.StockParLevels.SingleAsync(p => p.ProductTypeId == f.RedCells.Id);
        level.MinimumAvailable = 1;
        await c.SaveChangesAsync();
        var none = await service.CreateFromParAsync(new CreateSupplierOrderRequest(f.Supplier.Id));
        Assert.False(none.Succeeded);
        Assert.Contains("below par", none.Error);
    }

    private sealed record Fixture(BloodSupplier Supplier, ProductType RedCells, ProductType Plasma, BloodDelivery Delivery, BloodDelivery OtherDelivery);

    private async Task<Fixture> SeedAsync(BloodBankDbContext c)
    {
        var supplier = new BloodSupplier { Code = "SUP-A", Name = "Regional Blood Center", Registration = "R-1" };
        var other = new BloodSupplier { Code = "SUP-B", Name = "Second Center", Registration = "R-2" };
        var redCells = new ProductType { ProductCode = "RBC-SO", Name = "Red cells" };
        var plasma = new ProductType { ProductCode = "FFP-SO", Name = "Plasma" };
        c.AddRange(supplier, other, redCells, plasma);
        await c.SaveChangesAsync();

        c.StockParLevels.Add(new StockParLevel { ProductTypeId = redCells.Id, Abo = AboGroup.O, MinimumAvailable = 3 });
        c.StockParLevels.Add(new StockParLevel { ProductTypeId = plasma.Id, MinimumAvailable = 0 });
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-SO-1",
            ProductTypeId = redCells.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        });
        var delivery = new BloodDelivery { BloodSupplierId = supplier.Id, PackingListNumber = "PL-1", OrderedUtc = _factory.Clock.UtcNow, ReceivedUtc = _factory.Clock.UtcNow };
        var otherDelivery = new BloodDelivery { BloodSupplierId = other.Id, PackingListNumber = "PL-2", OrderedUtc = _factory.Clock.UtcNow, ReceivedUtc = _factory.Clock.UtcNow };
        c.BloodDeliveries.AddRange(delivery, otherDelivery);
        await c.SaveChangesAsync();
        return new Fixture(supplier, redCells, plasma, delivery, otherDelivery);
    }

    /// <summary>Only <see cref="ValidId"/> is valid for the submit action; records what was consumed.</summary>
    private sealed class FakeSignatures : ISignatureService
    {
        public const long ValidId = 7;

        public List<long> Consumed { get; } = new();

        public Task<OperationResult<long>> RecordAsync(string action, string meaningOfSignature, string? contextType = null, long? contextId = null, string? reauthenticationSecret = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<long>.Ok(ValidId));

        public Task<bool> IsValidForCurrentUserAsync(long signatureId, string? expectedAction = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(signatureId == ValidId && expectedAction == SupplierOrderRule.SignatureAction && !Consumed.Contains(signatureId));

        public Task ConsumeAsync(long signatureId, CancellationToken cancellationToken = default)
        {
            Consumed.Add(signatureId);
            return Task.CompletedTask;
        }
    }
}
