using BloodBankLIS.Application.Patients;
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

    private Hl7InboundProcessor Processor(BloodBankDbContext c, bool withMerge = true)
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
            translations: new InterfaceValueTranslationRepository(c),
            merges: withMerge ? Merge(c) : null);
    }

    private PatientMergeService Merge(BloodBankDbContext c) =>
        new(
            new EfRepository<Patient>(c),
            new EfRepository<PatientIdentifier>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<Encounter>(c),
            new EfRepository<Order>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Issue>(c),
            new EfRepository<Crossmatch>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<TransfusionEvent>(c),
            new EfRepository<ReactionInvestigation>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<BillingEvent>(c),
            new EfRepository<TestResult>(c),
            c);

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

    private static string AdtMerge(string controlId, string survivorMrn, string priorMrn, string last, string first) =>
        $"MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A40|{controlId}|P|2.5\r" +
        $"PID|1||{survivorMrn}^^^HOSP^MR||{last}^{first}||19800101|M\r" +
        $"MRG|{priorMrn}^^^HOSP^MR";

    private static string Orm(
        string controlId, string mrn, string placerId, string serviceId = "TS", string orderControl = "NW") =>
        $"MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ORM^O01|{controlId}|P|2.5\r" +
        $"PID|1||{mrn}^^^HOSP^MR||Doe^John\r" +
        $"ORC|{orderControl}|{placerId}\r" +
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
    public async Task InboundAdt_A40_MergesPriorPatientIntoSurvivor()
    {
        await using (var setup = _factory.Create())
        {
            await Processor(setup).ProcessAsync(Adt("CTRL-M-S", "HL7-SURV", "Survivor", "Pat"));
            await Processor(setup).ProcessAsync(Adt("CTRL-M-D", "HL7-DUP", "Duplicate", "Pat"));
            var duplicate = await setup.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-DUP");
            setup.AntibodyHistory.Add(new AntibodyHistory
            {
                PatientId = duplicate.Id,
                AntibodySpecificity = "anti-K",
                Status = AntibodyStatus.Identified,
                IsActive = true
            });
            setup.Encounters.Add(new Encounter
            {
                PatientId = duplicate.Id,
                VisitNumber = "VIS-MERGE-1",
                EncounterType = EncounterType.Inpatient,
                Status = EncounterStatus.Active,
                AdmitUtc = _factory.Clock.UtcNow,
                SourceSystem = "HL7"
            });
            await setup.SaveChangesAsync();
        }

        await using var context = _factory.Create();
        var result = await Processor(context).ProcessAsync(AdtMerge("CTRL-M40", "HL7-SURV", "HL7-DUP", "Survivor", "Pat"));
        Assert.True(result.Accepted);
        Assert.Contains("merged", result.AckMessage, StringComparison.OrdinalIgnoreCase);

        var survivor = await context.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-SURV");
        var loser = await context.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-DUP");
        Assert.Equal(PatientStatus.Merged, loser.Status);
        Assert.Equal(survivor.Id, loser.MergedIntoPatientId);
        Assert.True(await context.AntibodyHistory.AnyAsync(
            a => a.PatientId == survivor.Id && a.AntibodySpecificity == "anti-K"));
        Assert.True(await context.PatientIdentifiers.AnyAsync(
            i => i.PatientId == survivor.Id
                 && i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber
                 && i.Value == "HL7-DUP"));
        Assert.True(await context.Encounters.AnyAsync(e => e.VisitNumber == "VIS-MERGE-1" && e.PatientId == survivor.Id));
    }

    [Fact]
    public async Task InboundAdt_A40_DiscordantAbo_IsApplicationError()
    {
        await using (var setup = _factory.Create())
        {
            await Processor(setup).ProcessAsync(Adt("CTRL-ABO-S", "HL7-ABO-S", "Type", "One"));
            await Processor(setup).ProcessAsync(Adt("CTRL-ABO-D", "HL7-ABO-D", "Type", "Two"));
            var survivor = await setup.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-ABO-S");
            var duplicate = await setup.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-ABO-D");
            setup.PatientBloodTypeHistory.AddRange(
                new PatientBloodTypeHistory
                {
                    PatientId = survivor.Id,
                    Abo = AboGroup.O,
                    RhD = RhType.Positive,
                    IsCurrent = true,
                    Source = BloodTypeSource.TestResult
                },
                new PatientBloodTypeHistory
                {
                    PatientId = duplicate.Id,
                    Abo = AboGroup.A,
                    RhD = RhType.Positive,
                    IsCurrent = true,
                    Source = BloodTypeSource.TestResult
                });
            await setup.SaveChangesAsync();
        }

        await using var context = _factory.Create();
        var result = await Processor(context).ProcessAsync(AdtMerge("CTRL-ABO-M", "HL7-ABO-S", "HL7-ABO-D", "Type", "One"));
        Assert.False(result.Accepted);
        Assert.Equal("AE", result.AckCode);
        var loser = await context.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-ABO-D");
        Assert.NotEqual(PatientStatus.Merged, loser.Status);
    }

    [Fact]
    public async Task InboundAdt_A08OnPriorMrn_AfterMerge_UpdatesSurvivor()
    {
        await using (var setup = _factory.Create())
        {
            await Processor(setup).ProcessAsync(Adt("CTRL-F1", "HL7-CANON", "Canon", "Pat"));
            await Processor(setup).ProcessAsync(Adt("CTRL-F2", "HL7-ALIAS", "Alias", "Pat"));
            await Processor(setup).ProcessAsync(AdtMerge("CTRL-F3", "HL7-CANON", "HL7-ALIAS", "Canon", "Pat"));
        }

        await using var context = _factory.Create();
        var result = await Processor(context).ProcessAsync(Adt("CTRL-F4", "HL7-ALIAS", "Resolved", "Pat", "A08"));
        Assert.True(result.Accepted);

        var survivor = await context.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-CANON");
        var loser = await context.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-ALIAS");
        Assert.Equal("Resolved", survivor.LastName);
        Assert.Equal("Alias", loser.LastName);
        Assert.Equal(PatientStatus.Merged, loser.Status);
    }

    [Fact]
    public async Task InboundAdt_A08OnMergedMrn_WithoutMergeService_UpdatesSurvivor()
    {
        await using (var setup = _factory.Create())
        {
            var surviving = new Patient
            {
                MedicalRecordNumber = "HL7-FB-SURV",
                LastName = "Survivor",
                FirstName = "Pat",
                DateOfBirth = new DateOnly(1980, 1, 1)
            };
            var losing = new Patient
            {
                MedicalRecordNumber = "HL7-FB-LOSE",
                LastName = "Loser",
                FirstName = "Pat",
                DateOfBirth = new DateOnly(1980, 1, 1),
                Status = PatientStatus.Merged
            };
            setup.Patients.AddRange(surviving, losing);
            await setup.SaveChangesAsync();
            losing.MergedIntoPatientId = surviving.Id;
            await setup.SaveChangesAsync();
        }

        await using var context = _factory.Create();
        var result = await Processor(context, withMerge: false)
            .ProcessAsync(Adt("CTRL-FB", "HL7-FB-LOSE", "Followed", "Pat", "A08"));
        Assert.True(result.Accepted);

        var followed = await context.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-FB-SURV");
        var retired = await context.Patients.FirstAsync(p => p.MedicalRecordNumber == "HL7-FB-LOSE");
        Assert.Equal("Followed", followed.LastName);
        Assert.Equal("Loser", retired.LastName);
    }

    [Fact]
    public async Task InboundAdt_A11_CancelsExistingVisit()
    {
        await using (var setup = _factory.Create())
        {
            await Processor(setup).ProcessAsync(AdtWithVisit("CTRL-C1", "HL7-CAN", "Cancel", "Visit", "VIS-CAN-1"));
        }

        await using var context = _factory.Create();
        var result = await Processor(context).ProcessAsync(AdtWithVisit("CTRL-C2", "HL7-CAN", "Cancel", "Visit", "VIS-CAN-1", "A11"));
        Assert.True(result.Accepted);

        var visit = await context.Encounters.FirstAsync(e => e.VisitNumber == "VIS-CAN-1");
        Assert.Equal(EncounterStatus.Cancelled, visit.Status);
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
            if (!await c1.OrderingLocations.AnyAsync(l => l.Code == "ED"))
            {
                c1.OrderingLocations.Add(new OrderingLocation { Code = "ED", Name = "ED", IsActive = true });
                await c1.SaveChangesAsync();
            }
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
    public async Task InboundOrm_HoldReleaseAndDiscontinue_UpdateExistingOrder()
    {
        await using (var setup = _factory.Create())
        {
            await Processor(setup).ProcessAsync(Adt("CTRL-ORC-ADT", "HL7-ORC", "Control", "Order"));
            if (!await setup.OrderingLocations.AnyAsync(l => l.Code == "ED"))
            {
                setup.OrderingLocations.Add(new OrderingLocation { Code = "ED", Name = "ED", IsActive = true });
                await setup.SaveChangesAsync();
            }
            var created = await Processor(setup).ProcessAsync(Orm("CTRL-ORC-NW", "HL7-ORC", "PLACER-ORC", "XM"));
            Assert.True(created.Accepted);
        }

        await using (var holdCtx = _factory.Create())
        {
            var held = await Processor(holdCtx).ProcessAsync(
                Orm("CTRL-ORC-HD", "HL7-ORC", "PLACER-ORC", "XM", "HD"));
            Assert.True(held.Accepted);
            Assert.Equal(OrderStatus.OnHold, (await holdCtx.Orders.SingleAsync(o => o.OrderNumber == "PLACER-ORC")).Status);
        }

        await using (var releaseCtx = _factory.Create())
        {
            var released = await Processor(releaseCtx).ProcessAsync(
                Orm("CTRL-ORC-RL", "HL7-ORC", "PLACER-ORC", "XM", "RL"));
            Assert.True(released.Accepted);
            Assert.Equal(OrderStatus.InProcess, (await releaseCtx.Orders.SingleAsync(o => o.OrderNumber == "PLACER-ORC")).Status);
        }

        await using (var dcCtx = _factory.Create())
        {
            var discontinued = await Processor(dcCtx).ProcessAsync(
                Orm("CTRL-ORC-DC", "HL7-ORC", "PLACER-ORC", "XM", "DC"));
            Assert.True(discontinued.Accepted);
            var order = await dcCtx.Orders.SingleAsync(o => o.OrderNumber == "PLACER-ORC");
            Assert.Equal(OrderStatus.Discontinued, order.Status);
            Assert.Equal(FulfillmentStatus.Cancelled, order.FulfillmentStatus);
        }

        await using var deny = _factory.Create();
        var holdClosed = await Processor(deny).ProcessAsync(
            Orm("CTRL-ORC-HD2", "HL7-ORC", "PLACER-ORC", "XM", "HD"));
        Assert.False(holdClosed.Accepted);
        Assert.Equal("AE", holdClosed.AckCode);
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

    [Fact]
    public async Task OutboundSender_TransmitsQueuedMessage_AndRecordsAa()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        long messageId;
        await using (var setup = _factory.Create())
        {
            setup.InterfaceEndpoints.Add(new InterfaceEndpoint
            {
                Name = "EHR-ORU",
                Direction = Hl7Direction.Outbound,
                Transport = InterfaceTransport.Mllp,
                InterfaceType = InterfaceType.Results,
                Host = "127.0.0.1",
                Port = port,
                IsEnabled = true,
                AckTimeoutSeconds = 5,
                MessageTypes = "ORU"
            });
            var log = new Hl7MessageLog
            {
                Direction = Hl7Direction.Outbound,
                MessageType = "ORU",
                TriggerEvent = "R01",
                MessageControlId = "OUT-SEND-1",
                RawMessage = "MSH|^~\\&|BBLIS|LAB|EHR|HOSP|20260530120000||ORU^R01|OUT-SEND-1|P|2.5\rPID|1||MRN1",
                Status = Hl7MessageStatus.Received,
                ReceivedUtc = _factory.Clock.UtcNow
            };
            setup.Hl7Messages.Add(log);
            await setup.SaveChangesAsync();
            messageId = log.Id;
        }

        var accept = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var buffer = new byte[8192];
            var read = await stream.ReadAsync(buffer);
            var frames = BloodBankLIS.HL7.Mllp.MllpFraming.Extract(buffer.AsSpan(0, read), out _);
            var inbound = Hl7Parser.Parse(frames[0]);
            var ack = Hl7AckBuilder.BuildAck(inbound, AckCode.Accept, "ok", "ACK-SEND", _factory.Clock.UtcNow);
            await stream.WriteAsync(BloodBankLIS.HL7.Mllp.MllpFraming.Wrap(ack));
        });

        await using (var context = _factory.Create())
        {
            var sender = new Hl7OutboundSender(
                new EfRepository<Hl7MessageLog>(context),
                new EfRepository<InterfaceEndpoint>(context),
                new EfRepository<InterfaceErrorQueueItem>(context),
                context,
                _factory.Clock);
            var result = await sender.SendOneAsync(messageId);
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(Hl7MessageStatus.Acked, result.Value!.Status);
            Assert.Equal(AckCode.Accept, result.Value.AckCode);
        }

        await accept;
        listener.Stop();

        await using var verify = _factory.Create();
        var stored = await verify.Hl7Messages.FindAsync(messageId);
        Assert.Equal(Hl7MessageStatus.Acked, stored!.Status);
    }
}
