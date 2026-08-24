using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Parsing;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class Phase5Hl7Tests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public Phase5Hl7Tests(SqliteContextFactory factory) => _factory = factory;

    private Hl7InboundProcessor Processor(BloodBankDbContext c)
    {
        var providerSvc = new OrderingProviderService(new EfRepository<OrderingProvider>(c), c);
        var locationSvc = new OrderingLocationService(new EfRepository<OrderingLocation>(c), c);
        var encounters = new EncounterService(
            new EfRepository<Encounter>(c),
            new EfRepository<Patient>(c),
            new EfRepository<OrderingProvider>(c),
            providerSvc,
            c,
            _factory.Clock);
        var orders = new OrderService(
            new EfRepository<Order>(c),
            new EfRepository<OrderLine>(c),
            new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c),
            new EfRepository<OrderingLocation>(c),
            new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<OrderingProvider>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<TestGrouper>(c),
            _factory.Clock,
            c);
        return new(
            new EfRepository<Hl7MessageLog>(c),
            new EfRepository<InterfaceErrorQueueItem>(c),
            new EfRepository<Patient>(c),
            new EfRepository<Order>(c),
            new EfRepository<OrderingLocation>(c),
            encounters,
            orders,
            providerSvc,
            locationSvc,
            c,
            _factory.Clock,
            new EfRepository<InterfaceEndpoint>(c),
            new InterfaceFieldMappingRepository(c),
            translations: new InterfaceValueTranslationRepository(c));
    }

    private Hl7OutboundService Outbound(BloodBankDbContext c) =>
        new(
            new EfRepository<TestResult>(c),
            new EfRepository<Patient>(c),
            new EfRepository<Hl7MessageLog>(c),
            c,
            _factory.Clock,
            new EfRepository<InterfaceEndpoint>(c),
            new InterfaceFieldMappingRepository(c),
            new InterfaceValueTranslationRepository(c));

    private static string Adt(string controlId, string mrn, string last, string first, string trigger = "A01") =>
        $"MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^{trigger}|{controlId}|P|2.5\r" +
        $"PID|1||{mrn}^^^HOSP^MR||{last}^{first}||19800101|M";

    private static string AdtWithVisit(string controlId, string mrn, string last, string first, string visitNumber, string trigger = "A01")
    {
        var pv1 = new string[19];
        pv1[0] = "1";
        pv1[1] = "I";
        pv1[2] = "4W^Ward";
        pv1[6] = "12345^Adams^Amy";
        pv1[18] = visitNumber;
        return Adt(controlId, mrn, last, first, trigger) + "\rPV1|" + string.Join("|", pv1);
    }

    private static string Orm(string controlId, string mrn, string placerId, string serviceId = "TS") =>
        $"MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ORM^O01|{controlId}|P|2.5\r" +
        $"PID|1||{mrn}^^^HOSP^MR||Doe^John\r" +
        $"ORC|NW|{placerId}\r" +
        $"OBR|1|{placerId}||{serviceId}^Service";

    [Fact]
    public async Task InboundAdt_CreatesPatient_AndAcksAccept()
    {
        await using var context = _factory.Create();
        var result = await Processor(context).ProcessAsync(Adt("CTRL-A1", "HL7-100", "Newman", "Pat"));

        Assert.True(result.Accepted);
        Assert.Equal("AA", result.AckCode);
        Assert.Equal(Hl7MessageStatus.Processed, result.Log.Status);

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "HL7-100");
        Assert.NotNull(patient);
        Assert.Equal("Newman", patient!.LastName);

        // The ACK is itself a valid HL7 message echoing the control id.
        var ack = Hl7Parser.Parse(result.AckMessage);
        Assert.Equal("CTRL-A1", ack.Get("MSA-2"));
    }

    [Fact]
    public async Task InboundAdt_WithPv1_CreatesVisitAndProvider()
    {
        await using var context = _factory.Create();
        var result = await Processor(context).ProcessAsync(AdtWithVisit("CTRL-VIS", "HL7-VIS", "Visit", "Patient", "VIS-9001"));
        Assert.True(result.Accepted);

        var enc = await context.Encounters.FirstOrDefaultAsync(e => e.VisitNumber == "VIS-9001");
        Assert.NotNull(enc);
        Assert.Equal("12345", (await context.OrderingProviders.FirstOrDefaultAsync(p => p.Id == enc!.AttendingProviderId))!.ProviderId);
    }

    [Fact]
    public async Task InboundAdt_Update_ChangesDemographicsWithoutDuplicating()
    {
        await using (var c1 = _factory.Create())
        {
            await Processor(c1).ProcessAsync(Adt("CTRL-U1", "HL7-200", "Before", "Name"));
        }

        await using (var c2 = _factory.Create())
        {
            var result = await Processor(c2).ProcessAsync(Adt("CTRL-U2", "HL7-200", "After", "Name", "A08"));
            Assert.True(result.Accepted);
        }

        await using var verify = _factory.Create();
        var patients = await verify.Patients.Where(p => p.MedicalRecordNumber == "HL7-200").ToListAsync();
        Assert.Single(patients);
        Assert.Equal("After", patients[0].LastName);
    }

    [Fact]
    public async Task InboundAdt_DuplicateControlId_IsAcknowledgedButNotReprocessed()
    {
        await using (var c1 = _factory.Create())
        {
            await Processor(c1).ProcessAsync(Adt("CTRL-DUP", "HL7-300", "First", "One"));
        }

        await using (var c2 = _factory.Create())
        {
            // Same control id, different demographics: must be ignored (idempotent).
            var result = await Processor(c2).ProcessAsync(Adt("CTRL-DUP", "HL7-300", "ShouldNotApply", "Two"));
            Assert.True(result.Accepted);
            Assert.Contains("Duplicate", result.Log.ErrorDetail);
        }

        await using var verify = _factory.Create();
        var patient = await verify.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-300");
        Assert.Equal("First", patient.LastName);
    }

    [Fact]
    public async Task InboundOrm_CreatesOrder_WhenPatientExists()
    {
        await using (var c1 = _factory.Create())
        {
            await Processor(c1).ProcessAsync(Adt("CTRL-O-ADT", "HL7-400", "Order", "Patient"));
            c1.OrderingLocations.Add(new OrderingLocation { Code = "ED", Name = "ED", IsActive = true });
            await c1.SaveChangesAsync();
        }

        await using var context = _factory.Create();
        var result = await Processor(context).ProcessAsync(Orm("CTRL-O1", "HL7-400", "PLACER-400", "XM"));

        Assert.True(result.Accepted);
        var order = await context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == "PLACER-400");
        Assert.NotNull(order);
        Assert.Equal(OrderType.Crossmatch, order!.OrderType);
        Assert.Equal(OrderSource.Hl7, order.Source);
        var line = await context.OrderLines.FirstAsync(l => l.OrderId == order.Id && l.IsActive);
        Assert.Equal("XM", line.TestCode);
        Assert.True(order.EncounterId > 0);
        Assert.True(order.OrderingLocationId > 0);
    }

    [Fact]
    public async Task InboundOrm_UnknownPatient_ProducesApplicationErrorAndQueuesIt()
    {
        await using var context = _factory.Create();
        var result = await Processor(context).ProcessAsync(Orm("CTRL-O-ERR", "MISSING-MRN", "PLACER-999"));

        Assert.False(result.Accepted);
        Assert.Equal("AE", result.AckCode);
        Assert.Equal(Hl7MessageStatus.Errored, result.Log.Status);

        var queued = await context.InterfaceErrorQueue.AnyAsync(e => e.Hl7MessageId == result.Log.Id && !e.Resolved);
        Assert.True(queued);
    }

    [Fact]
    public async Task MalformedMessage_IsRejectedWithNak_AndLoggedErrored()
    {
        await using var context = _factory.Create();
        var result = await Processor(context).ProcessAsync("this is not HL7");

        Assert.False(result.Accepted);
        Assert.Equal("AR", result.AckCode);
        Assert.Equal(Hl7MessageStatus.Errored, result.Log.Status);
    }

    [Fact]
    public async Task Replay_DoesNotDuplicate_DueToBusinessKeyIdempotency()
    {
        long messageId;
        await using (var c1 = _factory.Create())
        {
            var result = await Processor(c1).ProcessAsync(Adt("CTRL-RP", "HL7-500", "Replay", "Me"));
            messageId = result.Log.Id;
        }

        await using (var c2 = _factory.Create())
        {
            var replay = await Processor(c2).ReplayAsync(messageId);
            Assert.NotNull(replay);
            Assert.True(replay!.Accepted);
            Assert.Equal(Hl7MessageStatus.Replayed, replay.Log.Status);
        }

        await using var verify = _factory.Create();
        var patients = await verify.Patients.Where(p => p.MedicalRecordNumber == "HL7-500").ToListAsync();
        Assert.Single(patients);
    }

    [Fact]
    public async Task Outbound_QueuesVerifiedResultAsValidOru()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            var patient = new Patient
            {
                MedicalRecordNumber = "HL7-600",
                LastName = "Result",
                FirstName = "Out",
                DateOfBirth = new DateOnly(1990, 6, 1),
                Sex = Sex.Female
            };
            setup.Patients.Add(patient);
            await setup.SaveChangesAsync();

            var specimen = new Specimen
            {
                AccessionNumber = "ACC-HL7-600",
                PatientId = patient.Id,
                SpecimenType = "EDTA",
                CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = SpecimenStatus.Accepted
            };
            setup.Specimens.Add(specimen);
            await setup.SaveChangesAsync();

            var result = new TestResult
            {
                PatientId = patient.Id,
                SpecimenId = specimen.Id,
                TestCode = "ABORH",
                Value = "A POS",
                Status = ResultStatus.Verified,
                VerifiedBy = "tech",
                VerifiedUtc = _factory.Clock.UtcNow
            };
            setup.TestResults.Add(result);
            await setup.SaveChangesAsync();
            resultId = result.Id;
        }

        await using var context = _factory.Create();
        var outcome = await Outbound(context).QueueResultMessageAsync(resultId);

        Assert.True(outcome.Succeeded);
        Assert.Equal(Hl7Direction.Outbound, outcome.Value!.Direction);

        var oru = Hl7Parser.Parse(outcome.Value.RawMessage);
        Assert.Equal("ORU", oru.MessageType);
        Assert.Equal("HL7-600", oru.Get("PID-3-1"));
        Assert.Equal("A POS", oru.Get("OBX-5"));
    }

    [Fact]
    public async Task InboundAdt_HonorsCustomMrnMapping()
    {
        long endpointId;
        await using (var setup = _factory.Create())
        {
            var endpoint = new InterfaceEndpoint
            {
                Name = "Meditech ADT",
                InterfaceType = InterfaceType.Adt,
                Direction = Hl7Direction.Inbound,
                Transport = InterfaceTransport.File,
                MessageTypes = "ADT",
                VendorCode = InterfaceVendorCodes.Meditech,
                MappingMode = InterfaceMappingMode.Custom,
                IsEnabled = false
            };
            setup.InterfaceEndpoints.Add(endpoint);
            await setup.SaveChangesAsync();
            setup.InterfaceFieldMappings.Add(new InterfaceFieldMapping
            {
                EndpointId = endpoint.Id,
                DataItemKey = InterfaceDataItemKeys.PatientMrn,
                Hl7Path = "PID-2",
                IsRequired = true
            });
            await setup.SaveChangesAsync();
            endpointId = endpoint.Id;
        }

        await using var context = _factory.Create();
        var raw =
            "MSH|^~\\&|MEDITECH|HOSP|BBLIS|LAB|20260530120000||ADT^A08|CTRL-MAP|P|2.5\r" +
            "PID|1|MAP-MRN||IGNORED^^^HOSP^MR||Mapped^Pat||19800101|M";
        var result = await Processor(context).ProcessAsync(raw, endpointId);

        Assert.True(result.Accepted);
        var patient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MAP-MRN");
        Assert.NotNull(patient);
        Assert.Null(await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "IGNORED"));
    }

    [Fact]
    public async Task InboundAdt_TranslatesValues_WhenEnabledEndpointExists()
    {
        await using (var setup = _factory.Create())
        {
            setup.InterfaceEndpoints.Add(new InterfaceEndpoint
            {
                Name = "Enabled ADT",
                InterfaceType = InterfaceType.Adt,
                Direction = Hl7Direction.Inbound,
                Transport = InterfaceTransport.File,
                MessageTypes = "ADT",
                IsEnabled = true
            });
            setup.InterfaceValueTranslations.Add(new InterfaceValueTranslation
            {
                DataItemKey = InterfaceDataItemKeys.PatientSex,
                InternalValue = "F",
                ExternalValue = "FEMALE",
                Direction = InterfaceTranslationDirection.Both
            });
            await setup.SaveChangesAsync();
        }

        await using var context = _factory.Create();
        var raw =
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A08|CTRL-TX|P|2.5\r" +
            "PID|1||HL7-TX^^^HOSP^MR||Translated^Pat||19800101|FEMALE";
        var result = await Processor(context).ProcessAsync(raw);

        Assert.True(result.Accepted);
        var patient = await context.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-TX");
        Assert.Equal(Sex.Female, patient.Sex);
    }

    [Fact]
    public async Task InboundAdt_DoesNotTranslate_WhenEndpointIsDisabled()
    {
        long endpointId;
        await using (var setup = _factory.Create())
        {
            var endpoint = new InterfaceEndpoint
            {
                Name = "Disabled ADT",
                InterfaceType = InterfaceType.Adt,
                Direction = Hl7Direction.Inbound,
                Transport = InterfaceTransport.File,
                MessageTypes = "ADT",
                IsEnabled = false
            };
            setup.InterfaceEndpoints.Add(endpoint);
            setup.InterfaceValueTranslations.Add(new InterfaceValueTranslation
            {
                DataItemKey = InterfaceDataItemKeys.PatientSex,
                InternalValue = "F",
                ExternalValue = "XXFEMALE",
                Direction = InterfaceTranslationDirection.Both
            });
            await setup.SaveChangesAsync();
            endpointId = endpoint.Id;
        }

        await using var context = _factory.Create();
        var raw =
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A08|CTRL-NOTX|P|2.5\r" +
            "PID|1||HL7-NOTX^^^HOSP^MR||Plain^Pat||19800101|XXFEMALE";
        var result = await Processor(context).ProcessAsync(raw, endpointId);

        Assert.True(result.Accepted);
        var patient = await context.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-NOTX");
        Assert.Equal(Sex.Unknown, patient.Sex);
    }

    [Fact]
    public async Task Outbound_TranslatesInternalResultValue()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            var patient = new Patient
            {
                MedicalRecordNumber = "HL7-TX-OUT",
                LastName = "Out",
                FirstName = "Tx",
                DateOfBirth = new DateOnly(1990, 6, 1),
                Sex = Sex.Female
            };
            setup.Patients.Add(patient);
            await setup.SaveChangesAsync();

            var specimen = new Specimen
            {
                AccessionNumber = "ACC-HL7-TX-OUT",
                PatientId = patient.Id,
                SpecimenType = "EDTA",
                CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = SpecimenStatus.Accepted
            };
            setup.Specimens.Add(specimen);
            await setup.SaveChangesAsync();

            var result = new TestResult
            {
                PatientId = patient.Id,
                SpecimenId = specimen.Id,
                TestCode = "ABORH",
                Value = "TX-A-POS",
                Status = ResultStatus.Verified,
                VerifiedBy = "tech",
                VerifiedUtc = _factory.Clock.UtcNow
            };
            setup.TestResults.Add(result);
            setup.InterfaceValueTranslations.Add(new InterfaceValueTranslation
            {
                DataItemKey = InterfaceDataItemKeys.ResultValue,
                InternalValue = "TX-A-POS",
                ExternalValue = "A+TX",
                Direction = InterfaceTranslationDirection.Outbound
            });
            await setup.SaveChangesAsync();
            resultId = result.Id;
        }

        await using var context = _factory.Create();
        var outcome = await Outbound(context).QueueResultMessageAsync(resultId);
        Assert.True(outcome.Succeeded, outcome.Error);
        var oru = Hl7Parser.Parse(outcome.Value!.RawMessage);
        Assert.Equal("A+TX", oru.Get("OBX-5"));
    }
}
