using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-526: an inbound order code is translated to a test or grouper (a grouper's tests each remember it); a code that
// names neither goes to the interface error queue and the unmapped list; a result for a grouper's test goes to the EHR
// under the grouper's procedure code, and a test ordered alone under its own.
public class InterfaceTestCodeTranslationTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private InterfaceValueSets ValueSets(BloodBankDbContext c) =>
        new(new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c));

    private InterfaceUnmappedValueService Unmapped(BloodBankDbContext c) =>
        new(new EfRepository<InterfaceUnmappedValue>(c), c, _factory.Clock, _factory.CurrentUser);

    private Hl7InboundProcessor Processor(BloodBankDbContext c)
    {
        var providers = new OrderingProviderService(new EfRepository<OrderingProvider>(c), c);
        var locations = new OrderingLocationService(new EfRepository<OrderingLocation>(c), c);
        var encounters = new EncounterService(new EfRepository<Encounter>(c), new EfRepository<Patient>(c),
            new EfRepository<OrderingProvider>(c), providers, c, _factory.Clock);
        var orders = new OrderService(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c);
        return new Hl7InboundProcessor(
            new EfRepository<Hl7MessageLog>(c), new EfRepository<InterfaceErrorQueueItem>(c), new EfRepository<Patient>(c),
            new EfRepository<Order>(c), new EfRepository<OrderingLocation>(c), encounters, orders, providers, locations, c,
            _factory.Clock, new EfRepository<InterfaceEndpoint>(c), new InterfaceFieldMappingRepository(c),
            translations: new InterfaceValueTranslationRepository(c),
            valueSets: ValueSets(c),
            unmapped: Unmapped(c));
    }

    private Hl7OutboundService Outbound(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Patient>(c), new EfRepository<Hl7MessageLog>(c), c, _factory.Clock,
            endpoints: new EfRepository<InterfaceEndpoint>(c), translations: new InterfaceValueTranslationRepository(c),
            orderLines: new EfRepository<OrderLine>(c), testDefinitions: new EfRepository<TestDefinition>(c),
            valueSets: ValueSets(c), unmapped: Unmapped(c));

    private async Task SeedAsync()
    {
        await using var c = _factory.Create();
        c.TestDefinitions.AddRange(
            new TestDefinition { Code = "ABORH", Name = "ABO/Rh", IsActive = true, IsDraft = false },
            new TestDefinition { Code = "ABSC", Name = "Antibody screen", IsActive = true, IsDraft = false },
            new TestDefinition { Code = "DAT", Name = "DAT", IsActive = true, IsDraft = false });
        c.TestGroupers.Add(new TestGrouper
        {
            Code = "TNS", Name = "Type and screen", IsActive = true, IsDraft = false,
            MemberTestsJson = TestGrouperMembers.ToJson([new TestGrouperMember("ABORH", 1), new TestGrouperMember("ABSC", 2)])
        });
        c.Patients.Add(new Patient { MedicalRecordNumber = "MRN-XL", LastName = "Code", FirstName = "Pat", DateOfBirth = new DateOnly(1980, 1, 1), Sex = Sex.Female });
        c.OrderingLocations.Add(new OrderingLocation { Code = "ICU", Name = "ICU", IsActive = true });
        c.InterfaceEndpoints.Add(new InterfaceEndpoint
        {
            Name = "Epic orders", InterfaceType = InterfaceType.Orders, Direction = Hl7Direction.Inbound,
            Transport = InterfaceTransport.File, MessageTypes = "ORM", IsEnabled = true
        });
        c.InterfaceValueTranslations.AddRange(
            new InterfaceValueTranslation { DataItemKey = InterfaceDataItemKeys.ProcedureCode, InternalValue = "TNS", ExternalValue = "LAB10117", Direction = InterfaceTranslationDirection.Both },
            new InterfaceValueTranslation { DataItemKey = InterfaceDataItemKeys.ProcedureCode, InternalValue = "DAT", ExternalValue = "LAB200", Direction = InterfaceTranslationDirection.Both });
        await c.SaveChangesAsync();
    }

    private static string Orm(string control, string placer, string code) =>
        $"MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20261107120000||ORM^O01|{control}|P|2.5\r" +
        "PID|1||MRN-XL^^^HOSP^MR||Code^Pat\r" +
        $"ORC|NW|{placer}\r" +
        $"OBR|1|{placer}||{code}^Ordered test";

    [Fact]
    public void ServiceCode_PrefersTheGroupersTranslation_ThenTheTests()
    {
        var translator = InterfaceValueTranslator.From(
        [
            new InterfaceValueTranslation { DataItemKey = InterfaceDataItemKeys.ProcedureCode, InternalValue = "TNS", ExternalValue = "LAB10117" },
            new InterfaceValueTranslation { DataItemKey = InterfaceDataItemKeys.ProcedureCode, InternalValue = "ABORH", ExternalValue = "LAB300" }
        ]);
        Assert.Equal("LAB10117", TestCodeTranslationRule.ServiceCode(translator, "ABORH", "TNS"));
        Assert.Equal("LAB300", TestCodeTranslationRule.ServiceCode(translator, "ABORH", null));
        Assert.Equal("LAB300", TestCodeTranslationRule.ServiceCode(translator, "ABORH", "OTHERGROUP"));
        Assert.Equal("ABSC", TestCodeTranslationRule.ServiceCode(translator, "ABSC", null));
    }

    [Fact]
    public async Task InboundGrouperCode_OrdersItsTests_AndTheResultGoesOutUnderIt()
    {
        await SeedAsync();
        await using (var c = _factory.Create())
        {
            var processed = await Processor(c).ProcessAsync(Orm("XL-1", "ORD-XL-1", "LAB10117"));
            Assert.True(processed.Accepted, processed.Log?.ErrorDetail);
        }

        await using var verify = _factory.Create();
        var order = await verify.Orders.SingleAsync(o => o.OrderNumber == "ORD-XL-1");
        var lines = await verify.OrderLines.Where(l => l.OrderId == order.Id).OrderBy(l => l.LineNumber).ToListAsync();
        Assert.Equal(["ABORH", "ABSC"], lines.Select(l => l.TestCode));
        Assert.All(lines, l => Assert.Equal("TNS", l.GrouperCode));

        var patient = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN-XL");
        var specimen = new Specimen { AccessionNumber = "ACC-XL", PatientId = patient.Id, SpecimenType = "EDTA", CollectedUtc = _factory.Clock.UtcNow, Status = SpecimenStatus.Accepted };
        verify.Specimens.Add(specimen);
        await verify.SaveChangesAsync();
        var aborh = new TestResult { PatientId = patient.Id, SpecimenId = specimen.Id, OrderId = order.Id, TestCode = "ABORH", Value = "O Pos", Status = ResultStatus.Verified, VerifiedUtc = _factory.Clock.UtcNow };
        var dat = new TestResult { PatientId = patient.Id, SpecimenId = specimen.Id, TestCode = "DAT", Value = "Negative", Status = ResultStatus.Verified, VerifiedUtc = _factory.Clock.UtcNow };
        verify.TestResults.AddRange(aborh, dat);
        await verify.SaveChangesAsync();

        var outbound = Outbound(verify);
        var aborhObr = (await outbound.QueueResultMessageAsync(aborh.Id)).Value!.RawMessage.Split((char)13).Single(s => s.StartsWith("OBR")).Split('|');
        Assert.Equal("LAB10117", aborhObr[4]);
        var aborhObx = (await outbound.QueueResultMessageAsync(aborh.Id)).Value!.RawMessage.Split((char)13).Single(s => s.StartsWith("OBX")).Split('|');
        Assert.Equal("ABORH", aborhObx[3]);
        var datObr = (await outbound.QueueResultMessageAsync(dat.Id)).Value!.RawMessage.Split((char)13).Single(s => s.StartsWith("OBR")).Split('|');
        Assert.Equal("LAB200", datObr[4]);
    }

    [Fact]
    public async Task UnknownOrderCode_GoesToTheErrorQueue_AndTheUnmappedList()
    {
        await SeedAsync();
        await using (var c = _factory.Create())
        {
            var first = await Processor(c).ProcessAsync(Orm("XL-9", "ORD-XL-9", "LAB99999"));
            Assert.False(first.Accepted);
            Assert.Contains(TestCodeTranslationRule.UnknownCode, first.Log?.ErrorDetail);
        }

        await using (var c = _factory.Create())
        {
            await Processor(c).ProcessAsync(Orm("XL-10", "ORD-XL-10", "LAB99999"));
        }

        await using var verify = _factory.Create();
        Assert.False(await verify.Orders.AnyAsync(o => o.OrderNumber == "ORD-XL-9"));
        Assert.True(await verify.InterfaceErrorQueue.AnyAsync(e => e.ErrorDetail.Contains("LAB99999")));
        var unmapped = Assert.Single(await verify.InterfaceUnmappedValues.Where(u => u.Value == "LAB99999").ToListAsync());
        Assert.Equal((InterfaceDataItemKeys.OrderTestCode, 2, "XL-10"), (unmapped.DataItemKey, unmapped.Count, unmapped.LastMessageControlId));
    }
}
