using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-498 (OCD-062): rejecting a tube tells the EHR its open tests will have no result (one ORU per open test line,
// OBR-25 X, OBX-11 X, reason and "recollect") when a results interface is enabled; nothing is queued without one, and
// a failure to queue never undoes the rejection.
public class SpecimenRejectionEhrNoticeTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private Hl7OutboundService Outbound(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Patient>(c), new EfRepository<Hl7MessageLog>(c), c, _factory.Clock,
            endpoints: new EfRepository<InterfaceEndpoint>(c), orders: new EfRepository<Order>(c),
            specimens: new EfRepository<Specimen>(c), orderSpecimens: new EfRepository<OrderSpecimen>(c), orderLines: new EfRepository<OrderLine>(c));

    private SpecimenService Specimens(BloodBankDbContext c, ISpecimenRejectionPublisher? notices) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c, _factory.Clock,
            currentUser: _factory.CurrentUser, orderSpecimens: new EfRepository<OrderSpecimen>(c), orders: new EfRepository<Order>(c),
            rejectionNotices: notices);

    private async Task<long> TubeWithOrderAsync(BloodBankDbContext c, string key)
    {
        var patient = new Patient { MedicalRecordNumber = $"MRN-REJ-{key}", LastName = "Reject", FirstName = key, DateOfBirth = new DateOnly(1975, 5, 5), Sex = Sex.Female };
        var location = new OrderingLocation { Code = $"LOC-{key}", Name = "Ward", IsActive = true };
        c.Patients.Add(patient);
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();
        var visit = new Encounter { PatientId = patient.Id, VisitNumber = $"V-{key}", Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddDays(-1) };
        c.Encounters.Add(visit);
        await c.SaveChangesAsync();
        var specimen = await Specimens(c, null).AccessionAsync(new AccessionSpecimenRequest($"ACC-REJ-{key}", patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(specimen.Succeeded, specimen.Error);
        var order = new Order
        {
            OrderNumber = $"ORD-REJ-{key}", PatientId = patient.Id, EncounterId = visit.Id, OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Test, Priority = OrderPriority.Routine, OrderedUtc = _factory.Clock.UtcNow, Status = OrderStatus.InProcess
        };
        c.Orders.Add(order);
        await c.SaveChangesAsync();
        c.OrderLines.AddRange(
            new OrderLine { OrderId = order.Id, LineNumber = 1, LineName = "ABO/Rh", TestCode = "ABORH" },
            new OrderLine { OrderId = order.Id, LineNumber = 2, LineName = "Antibody screen", TestCode = "ABSC" },
            new OrderLine { OrderId = order.Id, LineNumber = 3, LineName = "DAT", TestCode = "DAT", ResultStatus = ResultStatus.Verified });
        c.OrderSpecimens.Add(new OrderSpecimen { OrderId = order.Id, SpecimenId = specimen.Value!.Id, IsPrimary = true });
        await c.SaveChangesAsync();
        return specimen.Value.Id;
    }

    [Fact]
    public async Task Rejecting_QueuesANoResultNoticePerOpenTest_OnlyWithAResultsInterface()
    {
        await using var c = _factory.Create();
        var withoutInterface = await TubeWithOrderAsync(c, "NOIF");
        var rejected = await Specimens(c, Outbound(c)).RejectAsync(withoutInterface, "Hemolyzed");
        Assert.True(rejected.Succeeded, rejected.Error);
        Assert.Equal(0, await c.Hl7Messages.CountAsync(m => m.Direction == Hl7Direction.Outbound));

        c.InterfaceEndpoints.Add(new InterfaceEndpoint
        {
            Name = "EHR results", Direction = Hl7Direction.Outbound, InterfaceType = InterfaceType.Results, MessageTypes = "ORU", IsEnabled = true
        });
        await c.SaveChangesAsync();
        var tube = await TubeWithOrderAsync(c, "IF");
        var second = await Specimens(c, Outbound(c)).RejectAsync(tube, "Clotted");
        Assert.True(second.Succeeded, second.Error);
        Assert.DoesNotContain(second.Warnings, w => w.Code == SpecimenRecollectionRule.EhrNotSentCode);

        var sent = await c.Hl7Messages.Where(m => m.Direction == Hl7Direction.Outbound && m.MessageType == "ORU").ToListAsync();
        Assert.Equal(2, sent.Count);
        Assert.All(sent, m =>
        {
            Assert.Contains("ORD-REJ-IF", m.RawMessage);
            Assert.Contains("Specimen ACC-REJ-IF rejected: Clotted. Recollect.", m.RawMessage);
        });
        Assert.Contains(sent, m => m.RawMessage.Contains("ABORH"));
        Assert.Contains(sent, m => m.RawMessage.Contains("ABSC"));
        Assert.DoesNotContain(sent, m => m.RawMessage.Contains("|DAT"));
    }

    [Fact]
    public async Task AFailureToQueue_KeepsTheRejection_AndWarns()
    {
        await using var c = _factory.Create();
        var tube = await TubeWithOrderAsync(c, "FAIL");
        var rejected = await Specimens(c, new ThrowingPublisher()).RejectAsync(tube, "Mislabeled");
        Assert.True(rejected.Succeeded, rejected.Error);
        Assert.Contains(rejected.Warnings, w => w.Code == SpecimenRecollectionRule.EhrNotSentCode);
        Assert.Equal(SpecimenStatus.Rejected, (await c.Specimens.FindAsync(tube))!.Status);
    }

    private sealed class ThrowingPublisher : ISpecimenRejectionPublisher
    {
        public Task<int> PublishSpecimenRejectedAsync(long specimenId, CancellationToken ct = default) =>
            throw new InvalidOperationException("Interface store unavailable.");
    }
}
