using System.Text;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
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

// TEST-BB-301: supplier order from the minimum stock shortfall through submit, receive, export, and cancel.
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
            new MinimumStockService(new EfRepository<MinimumStockLevel>(c), new EfRepository<ProductType>(c), new InventoryRepository(c), c,
                supplierOrders: new EfRepository<SupplierOrder>(c), supplierOrderLines: new EfRepository<SupplierOrderLine>(c), clock: _factory.Clock),
            c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            _signatures,
            new TabularReportRenderer(),
            new EfRepository<BloodUnit>(c),
            patients: new EfRepository<Patient>(c));

    // TEST-BB-1024: a special order starts empty, takes a patient-specific line (Rh, requirements, patient by MRN) beside
    // a stock line, refuses an unknown MRN, keeps the lines apart, and exports them for the supplier (end-user testing
    // pass 15).
    [Fact]
    public async Task SpecialOrder_TakesAPatientSpecificLine()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var patient = new Patient { MedicalRecordNumber = "MRN-SO-SPEC", LastName = "Special", FirstName = "Avery", DateOfBirth = new DateOnly(1969, 7, 9) };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var service = Service(c);

        var created = await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id, FromShortfall: false));
        Assert.True(created.Succeeded, created.Error);
        Assert.Empty(created.Value!.Lines);

        Assert.Contains("No active patient has the MRN MRN-NOPE", (await service.UpdateAsync(created.Value.Id, new UpdateSupplierOrderRequest(
            [new SupplierOrderLineInput(f.RedCells.Id, AboGroup.O, 2, RhType.Positive, "irradiated, CMV-negative", "MRN-NOPE")], null))).Error);

        var saved = await service.UpdateAsync(created.Value.Id, new UpdateSupplierOrderRequest(
        [
            new SupplierOrderLineInput(f.RedCells.Id, AboGroup.O, 2, RhType.Positive, " irradiated, CMV-negative ", "mrn-so-spec"),
            new SupplierOrderLineInput(f.RedCells.Id, AboGroup.O, 3)
        ], "For Avery Special, ONC 6"));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(2, saved.Value!.Lines.Count);
        var special = Assert.Single(saved.Value.Lines, l => l.PatientMrn is not null);
        Assert.Equal((RhType.Positive, "irradiated, CMV-negative", "MRN-SO-SPEC", 2), (special.RhD!.Value, special.Requirements!, special.PatientMrn!, special.Quantity));
        Assert.Equal("Special, Avery", special.PatientName);

        var csv = await service.ExportAsync(created.Value.Id, "csv");
        Assert.True(csv.Succeeded, csv.Error);
        var text = Encoding.UTF8.GetString(csv.Value!.Bytes);
        Assert.Contains("rh", text);
        Assert.Contains("irradiated, CMV-negative", text);
        Assert.Contains("Special, Avery (MRN-SO-SPEC)", text);
    }

    [Fact]
    public async Task Order_FromMinimumStockShortfall_EditSubmitReceive()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var service = Service(c);

        var created = await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id));
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

        // TEST-BB-468: the delivery brought 3 of the 5 group O red cells (and an A unit not ordered): 2 short, audited.
        c.BloodUnits.AddRange(Enumerable.Range(1, 4).Select(i => new BloodUnit
        {
            UnitNumber = $"U-SO-D{i}", ProductTypeId = f.RedCells.Id, Abo = i == 4 ? AboGroup.A : AboGroup.O, RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(30), Status = UnitStatus.Quarantine, DeliveryId = f.Delivery.Id
        }));
        await c.SaveChangesAsync();
        Assert.Null(Assert.Single((await service.GetAsync(order.Id))!.Lines).Received);

        var received = await service.ReceiveAsync(order.Id, new ReceiveSupplierOrderRequest(f.Delivery.Id));
        Assert.True(received.Succeeded, received.Error);
        Assert.Equal(SupplierOrderStatus.Received, received.Value!.Status);
        Assert.Equal(f.Delivery.Id, received.Value.DeliveryId);
        var receivedLine = Assert.Single(received.Value.Lines);
        Assert.Equal((3, 2), (receivedLine.Received, receivedLine.Short));
        Assert.Equal(2, received.Value.UnitsShort);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(SupplierOrder) && e.EntityId == order.Id
            && e.Reason!.Contains("2 unit(s) short: RBC-SO O: 2 of 5 short")));
        Assert.False((await service.CancelAsync(order.Id, new CancelSupplierOrderRequest("too late"))).Succeeded);
    }

    // TEST-BB-509: units on a submitted, not yet received order count as on order: the next order drafts only what is
    // still short, a draft or cancelled order does not count, and a fully covered shortage is not ordered again.
    [Fact]
    public async Task SubmittedOrders_CountAsOnOrder_SoAShortageIsNotOrderedTwice()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var service = Service(c);
        var minimum = new MinimumStockService(new EfRepository<MinimumStockLevel>(c), new EfRepository<ProductType>(c), new InventoryRepository(c), c,
            supplierOrders: new EfRepository<SupplierOrder>(c), supplierOrderLines: new EfRepository<SupplierOrderLine>(c), clock: _factory.Clock);

        // Group O red cells: minimum 3, one on hand, short 2. Order one and submit it.
        var first = (await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id))).Value!;
        await service.UpdateAsync(first.Id, new UpdateSupplierOrderRequest([new SupplierOrderLineInput(f.RedCells.Id, AboGroup.O, 1)], null));
        Assert.True((await service.SubmitAsync(first.Id, new SubmitSupplierOrderRequest(FakeSignatures.ValidId))).Succeeded);

        // A draft does not count as on order; nor does a cancelled order.
        var draft = (await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id))).Value!;
        Assert.Equal(1, Assert.Single(draft.Lines).Quantity);
        var row = (await minimum.ListAsync()).Single(r => r.ProductTypeId == f.RedCells.Id);
        Assert.Equal((2, 1, 1), (row.Shortfall, row.OnOrder, row.StillToOrder));
        Assert.True((await service.CancelAsync(draft.Id, new CancelSupplierOrderRequest("duplicate"))).Succeeded);
        Assert.Equal(1, (await minimum.ListAsync()).Single(r => r.ProductTypeId == f.RedCells.Id).OnOrder);

        // Order the last one: the shortage is covered and nothing more is drafted, but the product is still short.
        var second = (await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id))).Value!;
        _signatures.Consumed.Clear();
        Assert.True((await service.SubmitAsync(second.Id, new SubmitSupplierOrderRequest(FakeSignatures.ValidId))).Succeeded);
        var covered = (await minimum.ListAsync()).Single(r => r.ProductTypeId == f.RedCells.Id);
        Assert.Equal((2, 2, 0), (covered.Shortfall, covered.OnOrder, covered.StillToOrder));
        var none = await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id));
        Assert.False(none.Succeeded);
        Assert.Contains("already on a submitted order", none.Error);
        Assert.Equal("RBC-SO O: 1 of 3, 2 on order", DashboardQueueCatalog.LowStockCard([covered]).Note);
    }

    // TEST-BB-510: a submitted order is due 24 hours (the default arrival window) after submit; past that it is overdue on
    // the order, on Minimum stock, and in the home card note.
    [Fact]
    public async Task SubmittedOrder_PastItsArrivalWindow_IsOverdue()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var service = Service(c);
        var minimum = new MinimumStockService(new EfRepository<MinimumStockLevel>(c), new EfRepository<ProductType>(c), new InventoryRepository(c), c,
            supplierOrders: new EfRepository<SupplierOrder>(c), supplierOrderLines: new EfRepository<SupplierOrderLine>(c), clock: _factory.Clock);

        var order = (await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id))).Value!;
        Assert.Null(order.ArrivalDueUtc);
        var submitted = (await service.SubmitAsync(order.Id, new SubmitSupplierOrderRequest(FakeSignatures.ValidId))).Value!;
        Assert.Equal(_factory.Clock.UtcNow.AddHours(24), submitted.ArrivalDueUtc);
        Assert.False(submitted.ArrivalOverdue);
        Assert.Equal(0, (await minimum.ListAsync()).Single(r => r.ProductTypeId == f.RedCells.Id).OnOrderOverdue);

        _factory.Clock.UtcNow = _factory.Clock.UtcNow.AddHours(25);
        Assert.True((await service.GetAsync(order.Id))!.ArrivalOverdue);
        var row = (await minimum.ListAsync()).Single(r => r.ProductTypeId == f.RedCells.Id);
        Assert.Equal((2, 2), (row.OnOrder, row.OnOrderOverdue));
        Assert.Equal("RBC-SO O: 1 of 3, 2 on order (overdue)", DashboardQueueCatalog.LowStockCard([row]).Note);

        // TEST-BB-1061: the follow-up with the supplier is recorded with what was said; a promised arrival replaces the
        // due time, so the order is no longer overdue until then (end-user testing pass 45).
        Assert.Contains("Say who", (await service.RecordFollowUpAsync(order.Id, new SupplierOrderFollowUpRequest(" "))).Error);
        Assert.Contains("later than now", (await service.RecordFollowUpAsync(order.Id,
            new SupplierOrderFollowUpRequest("Called ARC", _factory.Clock.UtcNow.AddHours(-1)))).Error);
        var promised = _factory.Clock.UtcNow.AddHours(6);
        var followed = await service.RecordFollowUpAsync(order.Id, new SupplierOrderFollowUpRequest("Called ARC: on the 14:00 courier", promised));
        Assert.True(followed.Succeeded, followed.Error);
        Assert.Equal((false, promised, true, "Called ARC: on the 14:00 courier", "tech-test"),
            (followed.Value!.ArrivalOverdue, followed.Value.ArrivalDueUtc, followed.Value.ArrivalPromised, followed.Value.FollowUpNote, followed.Value.FollowUpBy));
        Assert.Equal(0, (await minimum.ListAsync()).Single(r => r.ProductTypeId == f.RedCells.Id).OnOrderOverdue);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(SupplierOrder) && a.Reason!.StartsWith("Supplier followed up")));
    }

    // TEST-BB-468: named-ABO lines take matching units first, any-ABO lines take what is left, and no unit counts twice.
    [Fact]
    public void Reconcile_CountsEachDeliveredUnitOnce()
    {
        var lines = new[]
        {
            new SupplierOrderLineDto(1, 10, "RBC", "Red cells", AboGroup.O, 2),
            new SupplierOrderLineDto(2, 10, "RBC", "Red cells", null, 3),
            new SupplierOrderLineDto(3, 20, "FFP", "Plasma", AboGroup.AB, 2)
        };
        BloodUnit Unit(long product, AboGroup abo) => new() { ProductTypeId = product, Abo = abo };
        var delivered = new[] { Unit(10, AboGroup.O), Unit(10, AboGroup.O), Unit(10, AboGroup.O), Unit(10, AboGroup.A), Unit(20, AboGroup.A) };

        var result = SupplierOrderService.Reconcile(lines, delivered);
        Assert.Equal(new int?[] { 2, 2, 0 }, result.Select(l => l.Received).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, result.Select(l => l.Short).ToArray());
    }

    [Fact]
    public async Task Export_CsvAndPdf_AreAudited()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var service = Service(c);
        var order = (await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id))).Value!;

        var csv = await service.ExportAsync(order.Id, "csv");
        Assert.True(csv.Succeeded, csv.Error);
        Assert.Equal($"{order.OrderNumber}.csv", csv.Value!.FileName);
        var text = Encoding.UTF8.GetString(csv.Value.Bytes, 3, csv.Value.Bytes.Length - 3);
        Assert.Equal("product_code,product,abo,rh,requirements,for_patient,quantity\r\nRBC-SO,Red cells,O,Any,,,2\r\n", text);

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
        Assert.False((await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(inactive.Id))).Succeeded);

        var order = (await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id))).Value!;
        Assert.False((await service.CancelAsync(order.Id, new CancelSupplierOrderRequest(" "))).Succeeded);
        var cancelled = await service.CancelAsync(order.Id, new CancelSupplierOrderRequest("Covered by a standing order"));
        Assert.Equal(SupplierOrderStatus.Cancelled, cancelled.Value!.Status);

        var level = await c.MinimumStockLevels.SingleAsync(p => p.ProductTypeId == f.RedCells.Id);
        level.MinimumAvailable = 1;
        await c.SaveChangesAsync();
        var none = await service.CreateFromShortfallAsync(new CreateSupplierOrderRequest(f.Supplier.Id));
        Assert.False(none.Succeeded);
        Assert.Contains("below its minimum stock", none.Error);
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

        c.MinimumStockLevels.Add(new MinimumStockLevel { ProductTypeId = redCells.Id, Abo = AboGroup.O, MinimumAvailable = 3 });
        c.MinimumStockLevels.Add(new MinimumStockLevel { ProductTypeId = plasma.Id, MinimumAvailable = 0 });
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
