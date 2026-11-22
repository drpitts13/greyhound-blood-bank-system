using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// An order from the EHR that repeats an open test was filed with the duplicate warning computed and thrown away: no one
/// is at a screen for an interface order. The bench now gets an alert and the EHR's acknowledgment says so (Cycle 654).
/// </summary>
public class DuplicateEhrOrderAlertTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private Hl7InboundProcessor Processor(BloodBankDbContext c)
    {
        var providers = new OrderingProviderService(new EfRepository<OrderingProvider>(c), c);
        var locations = new OrderingLocationService(new EfRepository<OrderingLocation>(c), c);
        var encounters = new EncounterService(new EfRepository<Encounter>(c), new EfRepository<Patient>(c),
            new EfRepository<OrderingProvider>(c), providers, c, _factory.Clock);
        var alerts = new AlertService(new EfRepository<Alert>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));
        var orders = new OrderService(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c, alerts: alerts);
        return new Hl7InboundProcessor(
            new EfRepository<Hl7MessageLog>(c), new EfRepository<InterfaceErrorQueueItem>(c), new EfRepository<Patient>(c),
            new EfRepository<Order>(c), new EfRepository<OrderingLocation>(c), encounters, orders, providers, locations, c,
            _factory.Clock, new EfRepository<InterfaceEndpoint>(c), new InterfaceFieldMappingRepository(c));
    }

    private static string Orm(string control, string placer, string code) =>
        $"MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20261110120000||ORM^O01|{control}|P|2.5\r" +
        "PID|1||MRN-DUP^^^HOSP^MR||Repeat^Pat\r" +
        $"ORC|NW|{placer}\r" +
        $"OBR|1|{placer}||{code}^Ordered test";

    // TEST-BB-810: the first EHR order for a test raises nothing; a second for the same test while the first is open is
    // filed (never blocked) and raises one warning alert naming both orders and the patient, linked to the new order,
    // and the acknowledgment says so; an order for another test raises nothing.
    [Fact]
    public async Task EhrOrderRepeatingAnOpenTest_RaisesAnAlert()
    {
        await using (var c = _factory.Create())
        {
            c.TestDefinitions.AddRange(
                new TestDefinition { Code = "ABORH", Name = "ABO/Rh", IsActive = true, IsDraft = false },
                new TestDefinition { Code = "DAT", Name = "DAT", IsActive = true, IsDraft = false });
            c.Patients.Add(new Patient { MedicalRecordNumber = "MRN-DUP", LastName = "Repeat", FirstName = "Pat", DateOfBirth = new DateOnly(1980, 1, 1), Sex = Sex.Female });
            c.OrderingLocations.Add(new OrderingLocation { Code = "ICU", Name = "ICU", IsActive = true });
            await c.SaveChangesAsync();
        }

        await using (var c = _factory.Create())
        {
            var first = await Processor(c).ProcessAsync(Orm("DUP-1", "ORD-DUP-1", "ABORH"));
            Assert.True(first.Accepted, first.Log?.ErrorDetail);
            Assert.DoesNotContain("Already ordered", first.AckMessage);
            Assert.False(await c.Alerts.AnyAsync(a => a.Source == AlertRule.DuplicateEhrOrder));

            var repeat = await Processor(c).ProcessAsync(Orm("DUP-2", "ORD-DUP-2", "ABORH"));
            Assert.True(repeat.Accepted, repeat.Log?.ErrorDetail);
            Assert.Contains("Already ordered and still open: ABORH on ORD-DUP-1", repeat.AckMessage);

            var other = await Processor(c).ProcessAsync(Orm("DUP-3", "ORD-DUP-3", "DAT"));
            Assert.True(other.Accepted, other.Log?.ErrorDetail);
        }

        await using var verify = _factory.Create();
        Assert.True(await verify.Orders.AnyAsync(o => o.OrderNumber == "ORD-DUP-2"));
        var alert = Assert.Single(await verify.Alerts.Where(a => a.Source == AlertRule.DuplicateEhrOrder).ToListAsync());
        var patientId = (await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN-DUP")).Id;
        Assert.Equal(("DUPORDER:ORD-DUP-2", AlertSeverity.Warning, "EHR order ORD-DUP-2 repeats an open test",
                $"/patients/{patientId}?tab=orders&order=ORD-DUP-2"),
            (alert.Key, alert.Severity, alert.Title, alert.Link));
        Assert.StartsWith("Order ORD-DUP-2 for Repeat, Pat (MRN MRN-DUP) came from the EHR. Already ordered and still open: ABORH on ORD-DUP-1.", alert.Message);
    }

    // TEST-BB-811: the alert names whoever it can: a missing name or MRN is left out rather than invented.
    [Fact]
    public void Alert_NamesThePatientAsRecorded()
    {
        Assert.Contains("for MRN M1 came", DuplicateTestOrderRule.Alert(5, "O1", "M1", null, "W.").Message);
        Assert.Contains("for Doe, J came", DuplicateTestOrderRule.Alert(5, "O1", null, "Doe, J", "W.").Message);
        Assert.Contains("for patient 5 came", DuplicateTestOrderRule.Alert(5, "O1", " ", null, "W.").Message);
    }
}
