using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Astm.Messaging;
using BloodBankLIS.Astm.Simulator;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloodBankLIS.Integration.Tests;

public class InstrumentInterfaceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InstrumentInterfaceTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task AdminCreate_PersistsConnectionMapsAndRules()
    {
        await using var context = _factory.Create();
        var svc = Admin(context);
        var result = await svc.CreateAsync(new SaveInstrumentInterfaceRequest(
            $"Echo Test {Guid.NewGuid():N}"[..20],
            "Development",
            "seed",
            new InstrumentConnectionDto(null, 2577, true),
            [new InstrumentFieldMappingDto(InstrumentDataItemKeys.PatientId, "P.3", true)],
            [new InstrumentTestMappingDto("Group", "ABORH", InstrumentMappingKind.Assay, InterfaceTranslationDirection.Both, 1)],
            [new InstrumentRuleDto("forty", "message.subtest_result >= 40", "set('subtest_result','4+')", 1, true, false)]));

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Value!.Connection.IsServer);
        Assert.Equal(2577, result.Value.Connection.Port);
        Assert.Single(result.Value.FieldMappings);
        Assert.Single(result.Value.TestMappings);
        Assert.Single(result.Value.Rules);
        Assert.False(result.Value.IsEnabled);
    }

    [Fact]
    public async Task InboundGroupResult_PostsInstrumentPendingVerification()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        await SeedOrderAsync(context, "MRN-INST-GRP", "ACC-INST-GRP", "ABORH");

        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-GRP", "MRN-INST-GRP", "Patient", "Test", "19590422", "M", 3));
        var outcome = await Processor(context).ProcessAsync(generated.RawMessage, iface.Id);

        Assert.True(outcome.Accepted, string.Join("; ", outcome.Notes));
        var posted = await PostedResultAsync(context, "ACC-INST-GRP", "ABORH");
        Assert.Equal(ResultSource.Instrument, posted.Source);
        Assert.Equal(ResultStatus.PendingVerification, posted.Status);
        Assert.True(AboRhResultValueLooksLikePanel(posted.Value));
        Assert.True(await context.InstrumentMessages.AnyAsync(m => m.SpecimenId == "ACC-INST-GRP"));
    }

    [Fact]
    public async Task InboundScreenResult_PostsInstrumentPendingVerification()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        await SeedOrderAsync(context, "MRN-INST-SCR", "ACC-INST-SCR", "ABSC");

        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Screen", "ACC-INST-SCR", "MRN-INST-SCR", "Patient", "Test", "19590422", "M", 5));
        var outcome = await Processor(context).ProcessAsync(generated.RawMessage, iface.Id);

        Assert.True(outcome.Accepted, string.Join("; ", outcome.Notes));
        var posted = await PostedResultAsync(context, "ACC-INST-SCR", "ABSC");
        Assert.Equal(ResultSource.Instrument, posted.Source);
        Assert.Equal(ResultStatus.PendingVerification, posted.Status);
        Assert.Contains("AHG", posted.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cell1", posted.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InboundCrossmatchResult_PostsInstrumentPendingVerification()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        await SeedOrderAsync(context, "MRN-INST-XM", "ACC-INST-XM", "XM");

        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Crossmatch", "ACC-INST-XM", "MRN-INST-XM", "Patient", "Test", "19590422", "M", 6));
        var outcome = await Processor(context).ProcessAsync(generated.RawMessage, iface.Id);

        Assert.True(outcome.Accepted, string.Join("; ", outcome.Notes));
        var posted = await PostedResultAsync(context, "ACC-INST-XM", "XM");
        Assert.Equal(ResultSource.Instrument, posted.Source);
        Assert.Equal(ResultStatus.PendingVerification, posted.Status);
        Assert.True(
            posted.Value.Contains("Compatible", StringComparison.OrdinalIgnoreCase)
            || posted.Value.Contains("Incompatible", StringComparison.OrdinalIgnoreCase)
            || posted.Interpretation is { } interp
                && (interp.Contains("Compatible", StringComparison.OrdinalIgnoreCase)
                    || interp.Contains("Incompatible", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task InboundQuery_BuildsOrderDownload()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        await SeedOrderAsync(context, "MRN-INST-Q", "ACC-INST-Q", "ABORH");

        const string query = "H|\\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219140243\rQ|1|ACC-INST-Q||ALL\rL|1|N\r";
        var outcome = await Processor(context).ProcessAsync(query, iface.Id);

        Assert.True(outcome.Accepted, string.Join("; ", outcome.Notes));
        Assert.Contains("^^^Group", outcome.ReplyMessage);
        Assert.Contains("ACC-INST-Q", outcome.ReplyMessage);
    }

    [Fact]
    public async Task VerifiedResult_IsNotOverwritten()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        var ids = await SeedOrderAsync(context, "MRN-INST", "ACC-INST-1", "ABORH");
        var specimen = await context.Specimens.SingleAsync(s => s.Id == ids.SpecimenId);
        context.TestResults.Add(new TestResult
        {
            SpecimenId = ids.SpecimenId,
            PatientId = specimen.PatientId,
            OrderId = ids.OrderId,
            TestCode = "ABORH",
            Value = "O|Positive",
            Status = ResultStatus.Verified,
            Source = ResultSource.Manual,
            EnteredUtc = _factory.Clock.UtcNow,
            EnteredBy = "tech",
            VerifiedUtc = _factory.Clock.UtcNow,
            VerifiedBy = "sup"
        });
        await context.SaveChangesAsync();

        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-1", "MRN-INST", "Patient", "Test", "19590422", "M", 4));
        var outcome = await Processor(context).ProcessAsync(generated.RawMessage, iface.Id);

        Assert.Contains(outcome.Notes, n => n.Contains("verified", StringComparison.OrdinalIgnoreCase)
            || n.Contains("cannot be overwritten", StringComparison.OrdinalIgnoreCase)
            || !outcome.Accepted);
        var remaining = await PostedResultAsync(context, "ACC-INST-1", "ABORH");
        Assert.Equal(ResultStatus.Verified, remaining.Status);
        Assert.Equal("O|Positive", remaining.Value);
    }

    [Fact]
    public async Task Create_WithoutAdminInstrumentsManage_IsRejected()
    {
        await using var context = _factory.Create();
        var name = $"INST-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var request = new SaveInstrumentInterfaceRequest(
            name,
            "Development",
            "seed",
            new InstrumentConnectionDto(null, 2580, true),
            [new InstrumentFieldMappingDto(InstrumentDataItemKeys.PatientId, "P.3", true)],
            [new InstrumentTestMappingDto("Group", "ABORH", InstrumentMappingKind.Assay, InterfaceTranslationDirection.Both, 1)],
            []);

        var denied = await Admin(context, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InstrumentInterfaceAuthorizationRule.CreateCode);
        Assert.False(await context.InstrumentInterfaces.AnyAsync(i => i.Name == name));

        var allowed = await Admin(context, new FixedPermissionEvaluator(1, PermissionCodes.AdminInstrumentsManage))
            .CreateAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(name, allowed.Value!.Name);
    }

    private InstrumentInterfaceAdminService Admin(
        BloodBankDbContext context,
        IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new InstrumentInterfaceAdminService(
            new InstrumentInterfaceRepository(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private AstmInboundProcessor Processor(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var results = new ResultService(
            new EfRepository<TestResult>(context),
            new EfRepository<Specimen>(context),
            new EfRepository<PatientBloodTypeHistory>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env),
            orders: new EfRepository<Order>(context),
            orderLines: new EfRepository<OrderLine>(context));

        return new AstmInboundProcessor(
            new InstrumentInterfaceRepository(context),
            new EfRepository<InstrumentMessageLog>(context),
            new EfRepository<Specimen>(context),
            new EfRepository<Order>(context),
            new EfRepository<OrderSpecimen>(context),
            new EfRepository<OrderLine>(context),
            new EfRepository<Patient>(context),
            _factory.Clock,
            context,
            results,
            NullLogger<AstmInboundProcessor>.Instance);
    }

    private async Task<(long SpecimenId, long OrderId)> SeedOrderAsync(
        BloodBankDbContext context,
        string mrn,
        string accession,
        string testCode)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = "Patient",
            FirstName = "Test",
            DateOfBirth = new DateOnly(1959, 4, 22),
            Sex = Sex.Male
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var location = new OrderingLocation { Code = $"BB-{accession}", Name = "Blood Bank", IsActive = true };
        context.OrderingLocations.Add(location);
        await context.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{accession}",
            Status = EncounterStatus.Active
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync();

        var specimen = new Specimen
        {
            AccessionNumber = accession,
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
            Status = SpecimenStatus.Accepted
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync();

        var order = new Order
        {
            OrderNumber = $"ORD-{accession}",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            TestCode = testCode,
            Status = OrderStatus.InProcess,
            OrderedUtc = _factory.Clock.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        context.OrderLines.Add(new OrderLine
        {
            OrderId = order.Id,
            LineNumber = 1,
            LineName = testCode,
            TestCode = testCode,
            ResultStatus = ResultStatus.Pending,
            IsActive = true
        });
        context.OrderSpecimens.Add(new OrderSpecimen
        {
            OrderId = order.Id,
            SpecimenId = specimen.Id,
            IsPrimary = true
        });
        await context.SaveChangesAsync();
        return (specimen.Id, order.Id);
    }

    private static async Task<TestResult> PostedResultAsync(
        BloodBankDbContext context,
        string accession,
        string testCode)
    {
        var specimen = await context.Specimens.SingleAsync(s => s.AccessionNumber == accession);
        return await context.TestResults.SingleAsync(r => r.SpecimenId == specimen.Id && r.TestCode == testCode);
    }

    private static bool AboRhResultValueLooksLikePanel(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains("\"subtests\"", StringComparison.OrdinalIgnoreCase);
}
