using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AntibodyScreenPanelTests : IClassFixture<SqliteContextFactory>
{
    private static readonly string[] Cells = ["Cell1", "Cell2", "Cell3"];
    private static readonly string[] InterpPhases = ["IS", "37C", "AHG"];

    private readonly SqliteContextFactory _factory;

    public AntibodyScreenPanelTests(SqliteContextFactory factory) => _factory = factory;

    private ResultService Results(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestDefinition>(c), new EfRepository<SubtestDefinition>(c), new EfRepository<Order>(c), new EfRepository<OrderLine>(c),
            new InventoryRepository(c), Compatibility(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c), new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<UnitBloodAttribute>(c), new EfRepository<SpecimenTypeDefinition>(c),
            reflexRules: new EfRepository<ReflexRule>(c),
            phaseDefinitions: new EfRepository<PhaseDefinition>(c));

    private CompatibilityService Compatibility(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new BloodAttributeCompatLoader(
                new EfRepository<AntibodyHistory>(c),
                new EfRepository<AntigenProfile>(c),
                new EfRepository<UnitBloodAttribute>(c),
                new EfRepository<BloodAttributeDefinition>(c)),
            new AntibodyScreenCompatLoader(
                new EfRepository<TestResult>(c),
                new EfRepository<TestDefinition>(c),
                new EfRepository<AntibodyHistory>(c)),
            c, _factory.Clock, _factory.CurrentUser);

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c);

    [Fact]
    public async Task SavePositiveScreen_StoresPanelAndFiresReflex()
    {
        await using var c = _factory.Create();
        var (specimen, order, line) = await SeedPhasedAbscAsync(c);
        SeedReflex(c);
        await c.SaveChangesAsync();

        var entered = AllNegative();
        entered[PhaseResultKeys.Compose("Cell1", "AHG")] = "2+";

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            specimen.Id, order.Id, line.Id, "ABSCP", null, null, "Positive", null, null, entered,
            MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));

        Assert.True(save.Succeeded, save.Error);
        Assert.Equal("Positive", save.Value!.Interpretation);
        Assert.True(PanelResultValue.TryParse(save.Value.Value, out var parsed));
        Assert.Equal("2+", parsed[PhaseResultKeys.Compose("Cell1", "AHG")]);

        var lines = await c.OrderLines.Where(l => l.OrderId == order.Id && l.IsActive).ToListAsync();
        Assert.Contains(lines, l => l.TestCode == "ABID");
    }

    [Fact]
    public async Task SaveNegativeInterpretation_WithPositiveCell_Fails()
    {
        await using var c = _factory.Create();
        var (specimen, order, line) = await SeedPhasedAbscAsync(c);

        var entered = AllNegative();
        entered[PhaseResultKeys.Compose("Cell2", "IS")] = "1+";

        var save = await Results(c).SaveTestResultAsync(new SaveTestResultRequest(
            specimen.Id, order.Id, line.Id, "ABSCP", null, null, "Negative", null, null, entered,
            MarkComplete: false, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));

        Assert.False(save.Succeeded);
        Assert.Contains("expects", save.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompatLoader_SeesPositiveInterpretation()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-PH-{Guid.NewGuid():N}"[..12],
            LastName = "Screen",
            FirstName = "Pat",
            Status = PatientStatus.Active
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABSC",
            Name = "Antibody Screen",
            Category = TestCategory.AntibodyScreen,
            ResultValueType = ResultValueType.Subtest,
            IsActive = true,
            IsDraft = false
        });
        await c.SaveChangesAsync();

        c.TestResults.Add(new TestResult
        {
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            TestCode = "ABSC",
            Value = PanelResultValue.Format(AllNegative()),
            Interpretation = "Positive",
            Status = ResultStatus.Verified
        });
        await c.SaveChangesAsync();

        var loader = new AntibodyScreenCompatLoader(
            new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<AntibodyHistory>(c));
        Assert.True(await loader.HasPositiveAntibodyScreenAsync(patient.Id));
    }

    [Fact]
    public async Task CompatLoader_SeesLegacyCodedPositiveValue()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-CD-{Guid.NewGuid():N}"[..12],
            LastName = "Legacy",
            FirstName = "Pat",
            Status = PatientStatus.Active
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABSC",
            Name = "Antibody Screen",
            Category = TestCategory.AntibodyScreen,
            ResultValueType = ResultValueType.Coded,
            IsActive = true,
            IsDraft = false
        });
        await c.SaveChangesAsync();

        c.TestResults.Add(new TestResult
        {
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            TestCode = "ABSC",
            Value = "Positive",
            Status = ResultStatus.Verified
        });
        await c.SaveChangesAsync();

        var loader = new AntibodyScreenCompatLoader(
            new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<AntibodyHistory>(c));
        Assert.True(await loader.HasPositiveAntibodyScreenAsync(patient.Id));
    }

    private async Task<(Specimen specimen, PatientOrderDto order, OrderLineDto line)> SeedPhasedAbscAsync(BloodBankDbContext c)
    {
        SeedCatalog(c);
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-PN-{Guid.NewGuid():N}"[..12],
            LastName = "Panel",
            FirstName = "Pat",
            Status = PatientStatus.Active
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{Guid.NewGuid():N}",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow.AddHours(-4)
        };
        c.Encounters.Add(encounter);
        var location = new OrderingLocation { Code = $"L{Guid.NewGuid():N}"[..8], Name = "Lab", IsActive = true };
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABSCP", null)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null,
            SpecimenId: specimen.Id));
        Assert.True(created.Succeeded, created.Error);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        var order = Assert.Single(list);
        var line = Assert.Single(order.Lines);
        return (specimen, order, line);
    }

    private void SeedCatalog(BloodBankDbContext c)
    {
        var now = _factory.Clock.UtcNow;
        var choices = SubtestChoiceDefinitions.ToJson(SubtestChoiceDefinitions.DefaultGradedReaction());
        foreach (var cell in Cells)
        {
            c.SubtestDefinitions.Add(new SubtestDefinition
            {
                Code = cell,
                Name = cell,
                ResultType = SubtestResultType.GradedReaction,
                ChoicesJson = choices,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now
            });
        }

        c.PhaseDefinitions.AddRange(
            new PhaseDefinition { Code = "IS", Name = "Immediate spin", SortOrder = 1, IncludeInInterpretation = true, IsActive = true, IsDraft = false, EffectiveUtc = now },
            new PhaseDefinition { Code = "37C", Name = "37°C", SortOrder = 2, IncludeInInterpretation = true, IsActive = true, IsDraft = false, EffectiveUtc = now },
            new PhaseDefinition { Code = "AHG", Name = "AHG", SortOrder = 3, IncludeInInterpretation = true, IsActive = true, IsDraft = false, EffectiveUtc = now },
            new PhaseDefinition { Code = "CC", Name = "Check cells", SortOrder = 4, IncludeInInterpretation = false, IsCheckCell = true, ValidatesPhaseCode = "AHG", IsActive = true, IsDraft = false, EffectiveUtc = now });

        c.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABSCP",
            Name = "Antibody Screen Panel",
            Category = TestCategory.AntibodyScreen,
            ResultValueType = ResultValueType.Subtest,
            PanelSubtestsJson = PanelSubtestAssignments.ToJson(
                Cells.Select((code, i) => new PanelSubtestAssignment(code, true, i + 1, ["IS", "37C", "AHG", "CC"])).ToList()),
            InterpretationLogicJson = InterpretationLogicDefinitions.ToJson(
                InterpretationLogicDefinitions.DefaultAntibodyScreenLogic(Cells, InterpPhases)),
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now
        });
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABID",
            Name = "Antibody Identification",
            Category = TestCategory.AntibodyIdentification,
            ResultValueType = ResultValueType.FreeText,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now
        });
    }

    private static void SeedReflex(BloodBankDbContext c)
    {
        c.ReflexRules.Add(new ReflexRule
        {
            Code = $"ABSCP-POS-ABID-{Guid.NewGuid():N}"[..20],
            Name = "Positive antibody screen reflexes antibody identification",
            TriggerTestCode = "ABSCP",
            TriggerResultValue = "Positive",
            ReflexTestCode = "ABID",
            IsActive = true,
            IsDraft = false,
            Version = 1
        });
    }

    private static Dictionary<string, string> AllNegative()
    {
        var entered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in Cells)
        {
            foreach (var phase in InterpPhases)
            {
                entered[PhaseResultKeys.Compose(cell, phase)] = "0";
            }
        }

        return entered;
    }
}
