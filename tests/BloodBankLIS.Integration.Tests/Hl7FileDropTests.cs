using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Parsing;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class Hl7FileDropTests : IClassFixture<SqliteContextFactory>, IDisposable
{
    private readonly SqliteContextFactory _factory;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bb-hl7-drop-" + Guid.NewGuid().ToString("N"));

    public Hl7FileDropTests(SqliteContextFactory factory) => _factory = factory;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }
    }

    [Fact]
    public async Task OutboundFileDrop_WritesHl7AndAcks()
    {
        long messageId;
        await using (var setup = _factory.Create())
        {
            setup.InterfaceEndpoints.Add(new InterfaceEndpoint
            {
                Name = "ORU file out",
                Direction = Hl7Direction.Outbound,
                Transport = InterfaceTransport.File,
                InterfaceType = InterfaceType.Results,
                Path = _root,
                IsEnabled = true,
                MessageTypes = "ORU"
            });
            var log = new Hl7MessageLog
            {
                Direction = Hl7Direction.Outbound,
                MessageType = "ORU",
                TriggerEvent = "R01",
                MessageControlId = "FILE-OUT-1",
                RawMessage = "MSH|^~\\&|BBLIS|LAB|EHR|HOSP|20260530120000||ORU^R01|FILE-OUT-1|P|2.5\rPID|1||MRN1",
                Status = Hl7MessageStatus.Received,
                ReceivedUtc = _factory.Clock.UtcNow
            };
            setup.Hl7Messages.Add(log);
            await setup.SaveChangesAsync();
            messageId = log.Id;
        }

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

        var written = Directory.GetFiles(_root, "*.hl7");
        Assert.Single(written);
        Assert.Contains("FILE-OUT-1", await File.ReadAllTextAsync(written[0]));
    }

    [Fact]
    public async Task InboundFileDrop_ProcessesAdtAndArchives()
    {
        Hl7FileDropIO.EnsureLayout(_root);
        var inbox = Path.Combine(_root, "adt-in.hl7");
        await File.WriteAllTextAsync(inbox,
            "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260530120000||ADT^A01|FILE-IN-1|P|2.5\rPID|1||HL7-FILE^^^HOSP^MR||Drop^Patient||19800101|M");

        await using var context = _factory.Create();
        context.InterfaceEndpoints.Add(new InterfaceEndpoint
        {
            Name = "ADT file in",
            Direction = Hl7Direction.Inbound,
            Transport = InterfaceTransport.File,
            InterfaceType = InterfaceType.Adt,
            Path = _root,
            IsEnabled = true,
            MessageTypes = "ADT"
        });
        await context.SaveChangesAsync();

        var poller = new Hl7FileDropInboundPoller(new EfRepository<InterfaceEndpoint>(context), Processor(context));
        var processed = await poller.PollAsync();
        Assert.Equal(1, processed);

        Assert.False(File.Exists(inbox));
        Assert.True(File.Exists(Path.Combine(_root, Hl7FileDropLayout.ProcessedFolder, "adt-in.hl7")));
        Assert.True(File.Exists(Path.Combine(_root, Hl7FileDropLayout.AckFolder, "adt-in.ack")));
        Assert.Contains(await context.Patients.ToListAsync(), p => p.MedicalRecordNumber == "HL7-FILE");
    }

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
}
