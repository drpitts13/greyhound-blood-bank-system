using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Rules;
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
        routineOrder.NeededByUtc = now.AddMinutes(-10);
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
        // TEST-BB-632: the needed-by time is carried, and one that has passed is flagged.
        Assert.Equal((now.AddMinutes(-10), true), (routineRow.NeededByUtc, routineRow.NeededByPassed));
        Assert.Equal(((DateTime?)null, false), (statRow.NeededByUtc, statRow.NeededByPassed));
        // TEST-BB-626: the next step opens the reserve form with the order and the usable specimen chosen.
        var specimenId = c.Specimens.Single(x => x.AccessionNumber == "ACC-BO-1").Id;
        Assert.Equal((specimenId, $"/patients/{routine.Id}?tab=products&reserveOrder={routineOrder.Id}&reserveSpecimen={specimenId}"),
            (routineRow.UsableSpecimenId, routineRow.Href));
        Assert.Null(statRow.UsableSpecimenId);

        // TEST-BB-639: one order's progress, also once it is covered; an unknown order has none.
        var service = Service(c, factory);
        var routineProgress = (await service.GetProgressAsync(routineOrder.Id))!;
        Assert.Equal((3, 1, 1, 1, "1 more unit needed for this order (2 of 3 reserved or issued)."),
            (routineProgress.Requested, routineProgress.Reserved, routineProgress.Issued, routineProgress.Remaining, routineProgress.Message));
        Assert.Equal("All 1 unit for this order is reserved or issued.", (await service.GetProgressAsync(coveredOrder.Id))!.Message);
        Assert.Null(await service.GetProgressAsync(-1));

        // TEST-BB-628: with the type-and-screen check, a usable specimen whose ABO/Rh and screen are not verified says so,
        // and the next step is to finish the type and screen, not to reserve.
        var checkedRows = await Service(c, factory).ListAsync();
        var pending = Assert.Single(checkedRows, r => r.OrderNumber == "BO-1");
        Assert.Equal((true, true, "Finish type and screen", $"/patients/{routine.Id}?tab=tests"),
            (pending.HasUsableSpecimen, pending.TypeAndScreenPending, pending.NextStep, pending.Href));
        Assert.False(Assert.Single(checkedRows, r => r.OrderNumber == "BO-2").TypeAndScreenPending);

        // TEST-BB-644: the patient's active special requirements are listed with the order (antigen named; an expired or
        // inactivated one, and another patient's, are not); a patient with none has an empty list.
        c.SpecialTransfusionRequirements.AddRange(
            new SpecialTransfusionRequirement { PatientId = stat.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated, Reason = "Stem cell transplant", EffectiveUtc = now.AddDays(-1) },
            new SpecialTransfusionRequirement { PatientId = stat.Id, RequirementType = SpecialTransfusionRequirementType.AntigenNegative, AntigenCode = "K", Reason = "Anti-K", EffectiveUtc = now.AddDays(-1) },
            new SpecialTransfusionRequirement { PatientId = stat.Id, RequirementType = SpecialTransfusionRequirementType.Washed, Reason = "Old", EffectiveUtc = now.AddDays(-30), ExpiresUtc = now.AddDays(-2) },
            new SpecialTransfusionRequirement { PatientId = stat.Id, RequirementType = SpecialTransfusionRequirementType.CmvNegative, Reason = "Entered in error", EffectiveUtc = now.AddDays(-1), IsActive = false },
            new SpecialTransfusionRequirement { PatientId = covered.Id, RequirementType = SpecialTransfusionRequirementType.Washed, Reason = "Other patient", EffectiveUtc = now.AddDays(-1) });
        await c.SaveChangesAsync();
        var withRequirements = await Service(c, factory).ListAsync();
        Assert.Equal(["Antigen-negative (K)", "Irradiated"], Assert.Single(withRequirements, r => r.OrderNumber == "BO-2").Requirements!);
        Assert.Empty(Assert.Single(withRequirements, r => r.OrderNumber == "BO-1").Requirements!);
    }

    private static BloodOrderWorklistService Service(BloodBankDbContext c, SqliteContextFactory f) => new(
        new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<Allocation>(c), new EfRepository<Issue>(c),
        new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
        new EfRepository<AntibodyHistory>(c), f.Clock,
        new AntibodyScreenCompatLoader(new EfRepository<TestResult>(c), new EfRepository<TestDefinition>(c), new EfRepository<AntibodyHistory>(c),
            clock: f.Clock, specimens: new EfRepository<Specimen>(c), orders: new EfRepository<Order>(c)),
        new EfRepository<SpecialTransfusionRequirement>(c), new EfRepository<SpecialRequirementDefinition>(c));

    // TEST-BB-635: a recorded shift handoff counts the blood orders to prepare as the worklist lists them.
    [Fact]
    public async Task Handoff_CountsBloodOrdersToPrepare()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var c = factory.Create();
        var listed = await Service(c, factory).ListAsync();
        var handoff = await new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(c), c, factory.Clock, factory.CurrentUser,
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            bloodOrders: Service(c, factory)).CreateAsync(new CreateShiftHandoffRequest("Blood orders"));

        Assert.True(handoff.Succeeded, handoff.Error);
        Assert.True(listed.Count >= 1);
        Assert.Equal((listed.Count, listed.Count(o => o.NeededByPassed)), (handoff.Value!.BloodOrdersToPrepare, handoff.Value.BloodOrdersPastNeededBy));
    }

    // TEST-BB-637: the downtime sheet lists each patient's blood orders still to prepare, with what is needed and by when.
    [Fact]
    public async Task DowntimeSheet_ListsBloodOrdersToPrepare()
    {
        using var factory = new SqliteContextFactory();
        await using (var db = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var c = factory.Create();
        var summary = await new DowntimePatientSummaryService(
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<Allocation>(c), new EfRepository<Issue>(c),
            new EfRepository<BloodUnit>(c), new EfRepository<ProductType>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c), new EfRepository<SpecialTransfusionRequirement>(c), new EfRepository<SpecialRequirementDefinition>(c),
            new BloodBankLIS.Infrastructure.Audit.AuditWriter(c, factory.Clock, factory.CurrentUser), c, factory.Clock,
            bloodOrders: Service(c, factory)).GetAsync();

        var row = Assert.Single(summary.Patients, p => p.MedicalRecordNumber == "MRN0001");
        var line = Assert.Single(row.BloodOrders!, l => l.StartsWith("ORD-PREP-DEMO"));
        Assert.Contains("(Stat): 2 × ", line);
        Assert.Contains("2 still needed, needed by ", line);
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
        var rows = await Service(c, factory).ListAsync();
        var demo = Assert.Single(rows, r => r.OrderNumber == "ORD-PREP-DEMO");
        Assert.Equal((true, 2, 2, "MRN0001"), (demo.Rush, demo.Requested, demo.Remaining, demo.Mrn));
        // The demo patient's type and screen is complete on ACC0001: the reserve form opens on that specimen.
        var acc0001 = c.Specimens.Single(s => s.AccessionNumber == "ACC0001").Id;
        Assert.Equal(("Reserve 2 units", (long?)acc0001, false), (demo.NextStep, demo.UsableSpecimenId, demo.TypeAndScreenPending));
    }
}
