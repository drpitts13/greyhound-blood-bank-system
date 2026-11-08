using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-642: an order's history, newest first: placed, the audited needed-by change with who and where, a passed
// needed-by time while open, its specimen's collection and receipt, the reservation (released, with its reason), the
// issue, and the transfusion; another patient's order, or an unknown one, has none.
public class OrderTimelineTests
{
    [Fact]
    public async Task OrderHistory_FromPlacedToTransfused()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        var now = factory.Clock.UtcNow;
        var product = new ProductType { ProductCode = "RBC-OTL", Name = "Red Blood Cells", ComponentClass = ComponentClass.RedBloodCells };
        var location = new OrderingLocation { Code = "LOC-OTL", Name = "OR", IsActive = true };
        var patient = new Patient { MedicalRecordNumber = "MRN-OTL", LastName = "History", FirstName = "Order", DateOfBirth = new DateOnly(1970, 1, 1) };
        var other = new Patient { MedicalRecordNumber = "MRN-OTL-2", LastName = "Other", FirstName = "Pat", DateOfBirth = new DateOnly(1971, 1, 1) };
        c.AddRange(product, location, patient, other);
        await c.SaveChangesAsync();
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = "VIS-OTL", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = now.AddHours(-6) };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();
        var order = new Order
        {
            OrderNumber = "ORD-OTL", PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Product, OrderName = "Red cells", OrderType = OrderType.Other, ProductTypeId = product.Id,
            Priority = OrderPriority.Stat, Status = OrderStatus.InProcess, Source = OrderSource.Manual, OrderedUtc = now.AddHours(-5),
            OrderingProvider = "Dr. Surgeon", NeededByUtc = now.AddMinutes(-30)
        };
        c.Orders.Add(order);
        var specimen = new Specimen { PatientId = patient.Id, AccessionNumber = "ACC-OTL", SpecimenType = "EDTA", Status = SpecimenStatus.Accepted, CollectedUtc = now.AddHours(-4), ReceivedUtc = now.AddHours(-3.5), ExpiresUtc = now.AddDays(2), Collector = "RN Draw" };
        var unit = new BloodUnit { UnitNumber = "W-OTL-1", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = now.AddDays(20), Status = UnitStatus.Transfused };
        var released = new BloodUnit { UnitNumber = "W-OTL-2", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = now.AddDays(20), Status = UnitStatus.Available };
        c.AddRange(specimen, unit, released);
        await c.SaveChangesAsync();
        c.OrderSpecimens.Add(new OrderSpecimen { OrderId = order.Id, SpecimenId = specimen.Id });
        c.Allocations.AddRange(
            new Allocation { BloodProductId = released.Id, PatientId = patient.Id, OrderId = order.Id, SpecimenId = specimen.Id, Status = AllocationStatus.Released, ReleaseReason = "Unit damaged", AllocatedUtc = now.AddHours(-3), AllocatedBy = "tech1" },
            new Allocation { BloodProductId = unit.Id, PatientId = patient.Id, OrderId = order.Id, SpecimenId = specimen.Id, Status = AllocationStatus.Consumed, AllocatedUtc = now.AddHours(-2.5), AllocatedBy = "tech1" });
        var issue = new Issue { BloodProductId = unit.Id, PatientId = patient.Id, OrderId = order.Id, IssuedUtc = now.AddHours(-2), IssuedBy = "tech2", IssuedToLocation = "OR 3" };
        c.Issues.Add(issue);
        c.AuditEvents.Add(new AuditEvent
        {
            EventType = AuditEventType.OrderChange, EntityType = nameof(Order), EntityId = order.Id, UserName = "clerk", Workstation = "WS-7",
            OccurredUtc = now.AddHours(-4.5), Reason = "Needed-by time changed.",
            OldValueJson = "{\"NeededByUtc\":\"2026-01-01T08:00:00Z\"}", NewValueJson = "{\"NeededByUtc\":\"2026-01-01T09:00:00Z\"}"
        });
        await c.SaveChangesAsync();
        c.TransfusionEvents.Add(new TransfusionEvent { IssueId = issue.Id, BloodProductId = unit.Id, PatientId = patient.Id, StartUtc = now.AddHours(-1.5), StopUtc = now.AddHours(-1), VolumeTransfused = 300, Transfusionist = "RN Hang" });
        await c.SaveChangesAsync();

        var service = Service(c, factory);
        var timeline = await service.GetAsync(patient.Id, order.Id);
        Assert.NotNull(timeline);
        Assert.Equal("ORD-OTL", timeline.OrderNumber);
        var titles = timeline.Events.Select(e => e.Title).ToList();
        Assert.Equal(new[]
        {
            "Needed-by time passed",
            "Unit W-OTL-1 transfused",
            "Unit W-OTL-1 issued",
            "Unit W-OTL-1 reserved for this order",
            "Unit W-OTL-2 reserved for this order",
            "Specimen ACC-OTL received in the blood bank",
            "Specimen ACC-OTL collected (EDTA)",
            "Needed-by time changed",
            "Ordered: Red cells"
        }, titles);
        var ev = timeline.Events.ToDictionary(e => e.Title);
        Assert.Equal(TimelineSeverity.Warning, ev["Needed-by time passed"].Severity);
        Assert.Equal("clerk at WS-7", ev["Needed-by time changed"].By);
        Assert.Contains("NeededByUtc: 2026-01-01T08:00:00Z → 2026-01-01T09:00:00Z", ev["Needed-by time changed"].Detail);
        Assert.Equal("Now Released · Released: Unit damaged", ev["Unit W-OTL-2 reserved for this order"].Detail);
        Assert.Contains("To OR 3", ev["Unit W-OTL-1 issued"].Detail);
        Assert.Equal("300 mL", ev["Unit W-OTL-1 transfused"].Detail);
        Assert.Contains("Stat", ev["Ordered: Red cells"].Detail);
        Assert.Contains("provider Dr. Surgeon", ev["Ordered: Red cells"].Detail);

        Assert.Null(await service.GetAsync(other.Id, order.Id));
        Assert.Null(await service.GetAsync(patient.Id, -1));

        // Once the order is completed a passed needed-by time is no longer a warning on its history.
        order.Status = OrderStatus.Completed;
        await c.SaveChangesAsync();
        Assert.DoesNotContain((await service.GetAsync(patient.Id, order.Id))!.Events, e => e.Title is "Needed-by time passed" or "Needed by");
    }

    private static OrderTimelineService Service(BloodBankDbContext c, SqliteContextFactory factory) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderSpecimen>(c), new EfRepository<Specimen>(c), new EfRepository<TestResult>(c),
            new EfRepository<Allocation>(c), new EfRepository<Crossmatch>(c), new EfRepository<Issue>(c), new EfRepository<Return>(c),
            new EfRepository<TransfusionEvent>(c), new EfRepository<BloodUnit>(c), new EfRepository<Override>(c), new AuditQuery(c), factory.Clock);
}
