using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Demo data for the reaction warnings of Cycles 626–630 (Cycle 631): while MRN0006's reaction is under review, another
/// red cell unit is ordered and reserved for the patient, so the warnings show on the development data.
/// </summary>
public class SeederReactionNextUnitTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SeederReactionNextUnitTests(SqliteContextFactory factory) => _factory = factory;

    // TEST-BB-762: the seed adds ORD-RXN-NEXT with one reserved A+ red cell unit for MRN0006, whose investigation is not
    // closed; seeding again adds nothing, and a unit released later is not reserved again.
    [Fact]
    public async Task ReactionPatient_HasAnotherUnitReserved_Once()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var context = _factory.Create())
        {
            var patient = await context.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0006");
            Assert.True(await context.ReactionInvestigations.AnyAsync(r => r.PatientId == patient.Id && r.Status != ReactionInvestigationStatus.Closed));
            var order = await context.Orders.SingleAsync(o => o.OrderNumber == "ORD-RXN-NEXT");
            Assert.Equal((patient.Id, OrderStatus.InProcess), (order.PatientId, order.Status));
            var reserved = await context.Allocations.SingleAsync(a => a.OrderId == order.Id);
            Assert.Equal((patient.Id, AllocationStatus.Reserved), (reserved.PatientId, reserved.Status));
            var unit = await context.BloodUnits.SingleAsync(u => u.Id == reserved.BloodProductId);
            Assert.Equal((AboGroup.A, RhType.Positive, UnitStatus.Assigned), (unit.Abo, unit.RhD, unit.Status));

            // The bench releases it.
            reserved.Status = AllocationStatus.Released;
            unit.Status = UnitStatus.Available;
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        Assert.Equal(1, await verify.Orders.CountAsync(o => o.OrderNumber == "ORD-RXN-NEXT"));
        var orderId = (await verify.Orders.SingleAsync(o => o.OrderNumber == "ORD-RXN-NEXT")).Id;
        Assert.Equal(0, await verify.Allocations.CountAsync(a => a.OrderId == orderId && a.Status == AllocationStatus.Reserved));
    }

    // TEST-BB-763: a database seeded by the first version, where the demo reserved another demo's unit (the ISBT divide
    // unit W123425000002), is repaired once: that reservation is released, the unit is available again, and the case
    // gets its own unit W000123RXN0001.
    [Fact]
    public async Task FirstVersionBorrowedUnit_IsGivenBack()
    {
        using var factory = new SqliteContextFactory();
        await using (var context = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        long borrowedId;
        await using (var context = factory.Create())
        {
            // The first version's state: the reservation on the divide unit and no dedicated unit.
            var order = await context.Orders.SingleAsync(o => o.OrderNumber == "ORD-RXN-NEXT");
            var allocation = await context.Allocations.SingleAsync(a => a.OrderId == order.Id);
            var own = await context.BloodUnits.SingleAsync(u => u.UnitNumber == "W000123RXN0001");
            var divide = await context.BloodUnits.SingleAsync(u => u.Din == "W123425000002");
            allocation.BloodProductId = divide.Id;
            divide.Status = UnitStatus.Assigned;
            await context.SaveChangesAsync();
            context.BloodUnits.Remove(own);
            await context.SaveChangesAsync();
            borrowedId = divide.Id;
        }

        await using (var context = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = factory.Create();
        Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.SingleAsync(u => u.Id == borrowedId)).Status);
        var orderId = (await verify.Orders.SingleAsync(o => o.OrderNumber == "ORD-RXN-NEXT")).Id;
        var allocations = await verify.Allocations.Where(a => a.OrderId == orderId).ToListAsync();
        Assert.Equal(AllocationStatus.Released, allocations.Single(a => a.BloodProductId == borrowedId).Status);
        var dedicated = await verify.BloodUnits.SingleAsync(u => u.UnitNumber == "W000123RXN0001");
        Assert.Equal(AllocationStatus.Reserved, allocations.Single(a => a.BloodProductId == dedicated.Id).Status);
    }

    // TEST-BB-774: the seed adds one collected tube waiting for receipt (ACC-DEMO-RCV1, MRN0022, ICU visit); once it has
    // been received, seeding again does not add another.
    [Fact]
    public async Task TubeAwaitingReceipt_IsSeededOnce()
    {
        using var factory = new SqliteContextFactory();
        await using (var context = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var context = factory.Create())
        {
            var tube = await context.Specimens.SingleAsync(s => s.AccessionNumber == "ACC-DEMO-RCV1");
            var patient = await context.Patients.SingleAsync(p => p.Id == tube.PatientId);
            Assert.Equal(("MRN0022", SpecimenStatus.Collected), (patient.MedicalRecordNumber, tube.Status));
            Assert.True(tube.ExpiresUtc > DateTime.UtcNow);
            tube.Status = SpecimenStatus.Received;
            tube.ReceivedUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        await using (var context = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = factory.Create();
        Assert.Equal(1, await verify.Specimens.CountAsync(s => s.AccessionNumber == "ACC-DEMO-RCV1"));
    }
}
