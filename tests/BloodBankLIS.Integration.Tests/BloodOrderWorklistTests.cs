using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-622: open blood orders with units still to prepare are listed across patients, rush first then oldest; each
// says how many are still needed (reserved and issued units count, released and returned ones do not) and whether a
// usable specimen is on file; covered, cancelled, and completed orders are not listed.
public class BloodOrderWorklistTests
{
    [Fact]
    public async Task OrdersToPrepare_AreListedWithWhatIsStillNeeded()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        var now = factory.Clock.UtcNow;
        var product = new ProductType { ProductCode = "RBC-BO", Name = "Red Blood Cells", ComponentClass = ComponentClass.RedBloodCells };
        var location = new OrderingLocation { Code = "LOC-BO", Name = "OR", IsActive = true };
        c.ProductTypes.Add(product);
        c.OrderingLocations.Add(location);
        Patient Pat(string mrn, string last) => new() { MedicalRecordNumber = mrn, LastName = last, FirstName = "Pat", DateOfBirth = new DateOnly(1970, 1, 1) };
        var routine = Pat("MRN-BO-1", "Routine");
        var stat = Pat("MRN-BO-2", "Stat");
        var covered = Pat("MRN-BO-3", "Covered");
        c.Patients.AddRange(routine, stat, covered);
        await c.SaveChangesAsync();
        foreach (var p in new[] { routine, stat, covered })
        {
            c.Encounters.Add(new Encounter { PatientId = p.Id, VisitNumber = $"VIS-{p.MedicalRecordNumber}", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = now.AddHours(-5) });
        }

        await c.SaveChangesAsync();
        Order NewOrder(Patient p, string number, OrderPriority priority, int hoursAgo, OrderStatus status = OrderStatus.InProcess) => new()
        {
            OrderNumber = number, PatientId = p.Id, EncounterId = c.Encounters.Single(e => e.PatientId == p.Id).Id, OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Product, OrderName = "Red cells", OrderType = OrderType.Other, ProductTypeId = product.Id,
            Priority = priority, Status = status, OrderedUtc = now.AddHours(-hoursAgo)
        };
        var routineOrder = NewOrder(routine, "BO-1", OrderPriority.Routine, 4);
        var statOrder = NewOrder(stat, "BO-2", OrderPriority.Stat, 1);
        var coveredOrder = NewOrder(covered, "BO-3", OrderPriority.Routine, 6);
        var cancelledOrder = NewOrder(routine, "BO-4", OrderPriority.Routine, 7, OrderStatus.Cancelled);
        c.Orders.AddRange(routineOrder, statOrder, coveredOrder, cancelledOrder);
        await c.SaveChangesAsync();
        void Lines(Order o, int n)
        {
            for (var i = 1; i <= n; i++)
            {
                c.OrderLines.Add(new OrderLine { OrderId = o.Id, LineNumber = i, LineCategory = OrderCategory.Product, LineName = "Red Blood Cells", ProductTypeId = product.Id, OrderType = OrderType.Other, FulfillmentStatus = FulfillmentStatus.Ordered });
            }
        }

        Lines(routineOrder, 3);
        Lines(statOrder, 2);
        Lines(coveredOrder, 1);
        Lines(cancelledOrder, 2);
        var units = Enumerable.Range(1, 4).Select(i => new BloodUnit { UnitNumber = $"W-BO-{i}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = now.AddDays(20), Status = UnitStatus.Assigned }).ToList();
        c.BloodUnits.AddRange(units);
        c.Specimens.Add(new Specimen { PatientId = routine.Id, AccessionNumber = "ACC-BO-1", SpecimenType = "EDTA", Status = SpecimenStatus.Accepted, CollectedUtc = now.AddHours(-5), ExpiresUtc = now.AddDays(2) });
        c.Specimens.Add(new Specimen { PatientId = stat.Id, AccessionNumber = "ACC-BO-2", SpecimenType = "EDTA", Status = SpecimenStatus.Accepted, CollectedUtc = now.AddDays(-5), ExpiresUtc = now.AddHours(-1) });
        await c.SaveChangesAsync();
        c.Allocations.AddRange(
            new Allocation { BloodProductId = units[0].Id, PatientId = routine.Id, OrderId = routineOrder.Id, Status = AllocationStatus.Reserved, AllocatedUtc = now },
            new Allocation { BloodProductId = units[1].Id, PatientId = routine.Id, OrderId = routineOrder.Id, Status = AllocationStatus.Released, AllocatedUtc = now },
            new Allocation { BloodProductId = units[2].Id, PatientId = covered.Id, OrderId = coveredOrder.Id, Status = AllocationStatus.Reserved, AllocatedUtc = now });
        c.Issues.Add(new Issue { BloodProductId = units[3].Id, PatientId = routine.Id, OrderId = routineOrder.Id, IssuedUtc = now, IssuedBy = "tech" });
        await c.SaveChangesAsync();

        var rows = await new BloodOrderWorklistService(
            new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<Allocation>(c), new EfRepository<Issue>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c), factory.Clock).ListAsync();

        Assert.Equal(["BO-2", "BO-1"], rows.Select(r => r.OrderNumber));
        var statRow = rows[0];
        Assert.Equal((true, 2, 2, false, "Collect a specimen", $"/patients/{stat.Id}?tab=specimens"),
            (statRow.Rush, statRow.Requested, statRow.Remaining, statRow.HasUsableSpecimen, statRow.NextStep, statRow.Href));
        var routineRow = rows[1];
        Assert.Equal((3, 1, 1, 1, true, "Reserve 1 unit", "3 × Red Blood Cells"),
            (routineRow.Requested, routineRow.Reserved, routineRow.Issued, routineRow.Remaining, routineRow.HasUsableSpecimen, routineRow.NextStep, routineRow.Products));
    }

    // TEST-BB-624: the demo has one STAT order for two red cell units with nothing reserved, listed to prepare; added once.
    [Fact]
    public async Task Demo_HasAStatOrderToPrepare()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var c = factory.Create();
        var rows = await new BloodOrderWorklistService(
            new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<Allocation>(c), new EfRepository<Issue>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c), factory.Clock).ListAsync();
        var demo = Assert.Single(rows, r => r.OrderNumber == "ORD-PREP-DEMO");
        Assert.Equal((true, 2, 2, "MRN0001"), (demo.Rush, demo.Requested, demo.Remaining, demo.Mrn));
    }
}
