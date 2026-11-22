using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-480: the ward requests a reserved unit for a destination; a second request, a blank destination, an
// unreserved unit, and a user without transfusion or issue rights are refused; cancelling needs a reason and is audited
// with the request it ends.
public class IssueRequestServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public IssueRequestServiceTests(SqliteContextFactory factory) => _factory = factory;

    private IssueRequestService Service(BloodBankDbContext c, params string[] permissions) =>
        new(new EfRepository<Allocation>(c), new FixedPermissionEvaluator(1, permissions), _factory.CurrentUser, _factory.Clock,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), c);

    [Fact]
    public async Task Request_ThenCancel_KeepsTheTrail_AndRefusesWhatItShould()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var product = new ProductType { ProductCode = $"RBC-IRQ-{key}", Name = "Request RBC", ComponentClass = ComponentClass.RedBloodCells };
        var patient = new Patient { MedicalRecordNumber = $"MRN-IRQ-{key}", LastName = "Request", FirstName = "Ward", DateOfBirth = new DateOnly(1970, 1, 1) };
        c.AddRange(product, patient);
        await c.SaveChangesAsync();
        BloodUnit Unit(string s) => new() { UnitNumber = $"U-IRQ{s}-{key}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = _factory.Clock.UtcNow.AddDays(10), Status = UnitStatus.Crossmatched };
        var (a, b) = (Unit("A"), Unit("B"));
        c.BloodUnits.AddRange(a, b);
        await c.SaveChangesAsync();
        var reserved = new Allocation { BloodProductId = a.Id, PatientId = patient.Id, Status = AllocationStatus.Reserved, AllocatedUtc = _factory.Clock.UtcNow.AddHours(-1), AllocatedBy = "tech" };
        var released = new Allocation { BloodProductId = b.Id, PatientId = patient.Id, Status = AllocationStatus.Released, AllocatedUtc = _factory.Clock.UtcNow.AddHours(-2), AllocatedBy = "tech" };
        c.Allocations.AddRange(reserved, released);
        await c.SaveChangesAsync();
        var nurse = Service(c, PermissionCodes.TransfusionDocument);

        Assert.False((await nurse.RequestAsync(reserved.Id, new IssueRequestRequest(" "))).Succeeded);
        Assert.False((await Service(c, PermissionCodes.AuditRead).RequestAsync(reserved.Id, new IssueRequestRequest("OR 3"))).Succeeded);
        Assert.Contains("not reserved", (await nurse.RequestAsync(released.Id, new IssueRequestRequest("OR 3"))).Error);

        var requested = await nurse.RequestAsync(reserved.Id, new IssueRequestRequest(" OR 3 "));
        Assert.True(requested.Succeeded, requested.Error);
        Assert.Equal(("OR 3", _factory.CurrentUser.UserName), (requested.Value!.Destination, requested.Value.RequestedBy));
        var again = await nurse.RequestAsync(reserved.Id, new IssueRequestRequest("ICU"));
        Assert.Contains("Already requested", again.Error);
        Assert.Equal("OR 3", (await c.Allocations.AsNoTracking().SingleAsync(x => x.Id == reserved.Id)).IssueRequestedTo);

        Assert.False((await nurse.CancelAsync(reserved.Id, new CancelIssueRequestRequest(""))).Succeeded);
        var cancelled = await nurse.CancelAsync(reserved.Id, new CancelIssueRequestRequest("Surgery postponed"));
        Assert.True(cancelled.Succeeded, cancelled.Error);
        var row = await c.Allocations.AsNoTracking().SingleAsync(x => x.Id == reserved.Id);
        Assert.Null(row.IssueRequestedUtc);
        var audit = await c.AuditEvents.SingleAsync(e => e.EntityType == nameof(Allocation) && e.EntityId == reserved.Id && e.Reason == "Issue request cancelled: Surgery postponed");
        Assert.Contains("OR 3", audit.OldValueJson);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Allocation) && e.EntityId == reserved.Id && e.Reason == "Issue requested to OR 3."));
        Assert.False((await nurse.CancelAsync(reserved.Id, new CancelIssueRequestRequest("again"))).Succeeded);
    }
}
