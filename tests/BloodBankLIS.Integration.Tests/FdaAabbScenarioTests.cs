using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Seed-backed FDA/AABB validation evidence. These tests exercise catalog and
/// demo rows produced by <see cref="DatabaseSeeder"/>; they do not claim the
/// software is FDA-cleared or AABB-accredited.
/// </summary>
public class FdaAabbScenarioTests : IClassFixture<SqliteContextFactory>
{
    private const string LookbackDin = "W123426000001";

    private readonly SqliteContextFactory _factory;

    public FdaAabbScenarioTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task PregnantPatientSpecimen_ExpiresAt72HourAlloimmunizationWindow()
    {
        await using var c = await SeededAsync();
        var patient = await c.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0007");
        var specimen = await c.Specimens.SingleAsync(s => s.AccessionNumber == "ACC0017");

        Assert.True(SpecimenValidityPolicy.HasAlloimmunizationRisk(
            specimen.CollectedUtc, recentTransfusionUtc: null, patient.RecentPregnancyUtc));
        Assert.Equal(
            SpecimenValidityPolicy.ComputeExpiresUtc(specimen.CollectedUtc, alloimmunizationRisk: true),
            specimen.ExpiresUtc);
        Assert.Equal(
            specimen.CollectedUtc.AddHours(SpecimenValidityPolicy.DefaultAlloimmunizationRiskHours),
            specimen.ExpiresUtc);
        Assert.NotEqual(
            SpecimenValidityPolicy.ComputeExpiresUtc(specimen.CollectedUtc, alloimmunizationRisk: false),
            specimen.ExpiresUtc);
    }

    [Fact]
    public async Task AutologousUnit_IssuesOnlyToReservedPatient()
    {
        await using var c = await SeededAsync();
        var reserved = await c.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0008");
        var other = await c.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0001");
        var unit = await c.BloodUnits.SingleAsync(u => u.UnitNumber == "W000123AUTO001");

        var toReserved = IssueGate.Evaluate(ReadyIssue(reserved.Id) with
        {
            DonationRestriction = unit.DonationRestriction,
            ReservedPatientId = unit.ReservedPatientId,
            IssuePatientId = reserved.Id
        });
        Assert.DoesNotContain(toReserved.HardStops, r => r.Code == AutologousDirectedRule.IssueCode);

        var toOther = IssueGate.Evaluate(ReadyIssue(other.Id) with
        {
            DonationRestriction = unit.DonationRestriction,
            ReservedPatientId = unit.ReservedPatientId,
            IssuePatientId = other.Id
        });
        Assert.True(toOther.IsHardStopped);
        Assert.Contains(toOther.HardStops, r => r.Code == AutologousDirectedRule.IssueCode);
    }

    [Fact]
    public async Task DirectedUnit_ConvertWithoutSecondVerifier_IsHardStopped()
    {
        await using var c = await SeededAsync();
        var directed = await c.BloodUnits.SingleAsync(u => u.UnitNumber == "W000123DIR0001");
        var autologous = await c.BloodUnits.SingleAsync(u => u.UnitNumber == "W000123AUTO001");

        Assert.Equal(
            RuleSeverity.HardStop,
            AutologousDirectedRule.EvaluateConvert(autologous.DonationRestriction, autologous.Status).Severity);

        var convert = AutologousDirectedRule.EvaluateConvert(directed.DonationRestriction, directed.Status);
        Assert.Equal(RuleSeverity.Pass, convert.Severity);

        var blocked = await Inventory(c).ConvertDirectedToAllogeneicAsync(
            directed.Id, "Release unused directed unit to volunteer inventory.");
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Evaluation!.HardStops, r => r.Code == DirectedConversionVerifierRule.Code);
        Assert.Equal(DonationRestriction.Directed, (await c.BloodUnits.FindAsync(directed.Id))!.DonationRestriction);
    }

    [Fact]
    public async Task LookbackByDin_ListsSeededRecipientAndPendingNotification()
    {
        await using var c = await SeededAsync();
        var report = await Lookback(c).FindByDinAsync(LookbackDin);
        Assert.True(report.Succeeded, report.Error);
        Assert.Equal(LookbackDin, report.Value!.Din);
        Assert.Contains(report.Value.Recipients, r => r.MedicalRecordNumber == "MRN0001");
        Assert.Contains(report.Value.Notifications, n =>
            n.Status == LookbackNotificationStatus.Pending && n.Din == LookbackDin);
        Assert.Contains(report.Value.Units, u => u.Status == UnitStatus.Issued || u.Status == UnitStatus.Transfused);
        Assert.Contains(report.Value.Units, u => u.Status == UnitStatus.Available);

        var trace = await Lookback(c).FindByRecipientAsync("MRN0001", null);
        Assert.True(trace.Succeeded, trace.Error);
        Assert.Contains(trace.Value!.RelatedComponents, r => r.Din == LookbackDin);
        Assert.DoesNotContain(trace.Value.Units, u => u.UnitNumber == "W000123LSIB001");
    }

    [Fact]
    public async Task EmergencyRelease_CarriesIncompleteTestingStatement()
    {
        await using var c = await SeededAsync();
        var patient = await c.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0005");
        var issue = await c.Issues.SingleAsync(i => i.PatientId == patient.Id && i.IssueType == IssueType.EmergencyRelease);

        Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency, issue.CrossmatchStatus);
        Assert.True(issue.TestsIncompleteAtIssue);
        Assert.False(string.IsNullOrWhiteSpace(issue.EmergencyReleaseDetails));
        Assert.NotNull(issue.OverrideId);
        Assert.True(await c.Overrides.AnyAsync(o =>
            o.Id == issue.OverrideId && o.Action == OverrideAction.EmergencyRelease));
    }

    [Fact]
    public async Task AntiKPatient_UntypedOrKPositiveUnit_RequiresAntigenNegOverride()
    {
        await using var c = await SeededAsync();
        var patient = await c.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0004");
        var kell = await c.BloodAttributeDefinitions.SingleAsync(a => a.Code == "K");
        var antibodies = await c.AntibodyHistory
            .Where(a => a.PatientId == patient.Id && a.IsActive)
            .ToListAsync();
        Assert.Contains(antibodies, a => a.BloodAttributeDefinitionId == kell.Id);

        var kNegativeIds = await c.UnitBloodAttributes
            .Where(a => a.BloodAttributeDefinitionId == kell.Id && a.Result == AntigenResult.Negative)
            .Select(a => a.BloodProductId)
            .ToListAsync();
        var untyped = await c.BloodUnits.FirstAsync(u =>
            u.Status == UnitStatus.Available
            && u.Abo == AboGroup.O
            && u.RhD == RhType.Positive
            && !kNegativeIds.Contains(u.Id));

        var untypedEval = IssueGate.Evaluate(ReadyIssue(patient.Id) with
        {
            PatientAboRh = new AboRh(AboGroup.O, RhType.Positive),
            UnitAboRh = new AboRh(untyped.Abo, untyped.RhD),
            PatientSignificantAntibodies = [new BloodAttributeCompatibilityRule.AntibodyRef("K", "anti-K")],
            UnitAntigens = []
        });
        Assert.Contains(untypedEval.Warnings, r => r.Code == BloodAttributeCompatibilityRule.AntigenNegCode);

        var kPositiveEval = IssueGate.Evaluate(ReadyIssue(patient.Id) with
        {
            PatientAboRh = new AboRh(AboGroup.O, RhType.Positive),
            UnitAboRh = new AboRh(AboGroup.O, RhType.Positive),
            PatientSignificantAntibodies = [new BloodAttributeCompatibilityRule.AntibodyRef("K", "anti-K")],
            UnitAntigens = [new BloodAttributeCompatibilityRule.AntigenRef("K", AntigenResult.Positive)]
        });
        Assert.True(kPositiveEval.RequiresOverride);
        Assert.Contains(kPositiveEval.Warnings, r => r.Code == BloodAttributeCompatibilityRule.AntigenNegCode);
    }

    [Fact]
    public async Task QuarantineMissingAndExpiredUnits_AreNotIssuable()
    {
        await using var c = await SeededAsync();
        var patient = await c.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0001");
        var quarantine = await c.BloodUnits.FirstAsync(u => u.Status == UnitStatus.Quarantine);
        var missing = await c.BloodUnits.SingleAsync(u => u.UnitNumber == "W000123MISS001");
        var expired = await c.BloodUnits.FirstAsync(u => u.Status == UnitStatus.Expired);

        foreach (var unit in new[] { quarantine, missing, expired })
        {
            var evaluation = IssueGate.Evaluate(ReadyIssue(patient.Id) with
            {
                UnitStatus = unit.Status,
                UnitExpiresUtc = unit.ExpiresUtc,
                NowUtc = DateTime.UtcNow
            });
            Assert.True(evaluation.IsHardStopped, unit.UnitNumber);
            Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.UnitStatusCode);
        }
    }

    [Fact]
    public async Task AboSelfVerify_IsBlockedBySeededFacilityPolicy()
    {
        await using var c = await SeededAsync();
        var policy = new FacilityPolicyService(new EfRepository<SystemSetting>(c));
        Assert.True(await policy.GetBlockAboSelfVerifyAsync());

        var specimen = await c.Specimens.SingleAsync(s => s.AccessionNumber == "ACC0017");
        var entered = await Results(c, policy).EnterAboRhAsync(
            new EnterAboRhRequest(specimen.Id, AboGroup.O, RhType.Positive));
        Assert.True(entered.Succeeded, entered.Error);

        var self = await Results(c, policy).VerifyResultAsync(entered.Value!.Id);
        Assert.False(self.Succeeded);
        Assert.Contains(self.Evaluation!.HardStops, r => r.Code == SelfVerifyRule.Code);
    }

    [Fact]
    public async Task ReactionInvestigation_HasClericalCheckAndDat()
    {
        await using var c = await SeededAsync();
        var patient = await c.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0006");
        var reaction = await c.TransfusionEvents.SingleAsync(t => t.PatientId == patient.Id && t.ReactionSuspected);
        Assert.Equal(TransfusionDisposition.Stopped, reaction.FinalDisposition);

        var investigation = await c.ReactionInvestigations.SingleAsync(i => i.TransfusionEventId == reaction.Id);
        Assert.True(investigation.ClericalCheckCompleted);
        Assert.Equal(DatWorkupResult.Negative, investigation.DatResult);
        Assert.True(await c.Orders.AnyAsync(o =>
            o.PatientId == patient.Id && o.OrderType == OrderType.TransfusionReactionWorkup));
    }

    [Fact]
    public async Task SeededClinicalActions_ProduceAuditEvents()
    {
        await using var c = await SeededAsync();
        Assert.True(await c.AuditEvents.AnyAsync());
        Assert.True(await c.Issues.AnyAsync());
        Assert.True(await c.TransfusionEvents.AnyAsync());
        Assert.True(await c.LookbackNotifications.AnyAsync());
    }

    [Fact]
    public async Task PatriciaDemo_IsElectronicXmEligible_WhenFacilityAllows()
    {
        await using var c = await SeededAsync();
        var patient = await c.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0001");
        var dto = await ElectronicXm(c).AssessAsync(patient.Id);
        Assert.NotNull(dto);
        Assert.True(dto!.FacilityAllowsElectronicCrossmatch);
        Assert.True(dto.Eligible, dto.BlockingReason);
        Assert.All(dto.Criteria, criterion => Assert.True(criterion.Satisfied, criterion.Code + ": " + criterion.Detail));
    }

    private async Task<BloodBankDbContext> SeededAsync()
    {
        var context = _factory.Create();
        await DatabaseSeeder.SeedAsync(context);
        return context;
    }

    private static IssueGateContext ReadyIssue(long patientId) => new()
    {
        IdentityConfirmed = true,
        SpecimenExists = true,
        SpecimenBelongsToPatient = true,
        SpecimenExpiresUtc = DateTime.UtcNow.AddDays(2),
        PatientBloodTypeKnown = true,
        PatientAboRh = new AboRh(AboGroup.O, RhType.Positive),
        UnitAboRh = new AboRh(AboGroup.O, RhType.Positive),
        ComponentClass = ComponentClass.RedBloodCells,
        UnitStatus = UnitStatus.Allocated,
        UnitExpiresUtc = DateTime.UtcNow.AddDays(10),
        AllocatedToThisPatient = true,
        RequiresCrossmatch = true,
        HasValidCrossmatch = true,
        IsEmergencyRelease = false,
        ProductTypeMatchesOrder = true,
        SpecialRequirementsMet = true,
        UnresolvedAboRhDiscrepancy = false,
        HasSecondConcordantAboRh = true,
        DonationRestriction = DonationRestriction.Allogeneic,
        IssuePatientId = patientId,
        OrderLinked = true,
        NowUtc = DateTime.UtcNow
    };

    private LookbackService Lookback(BloodBankDbContext c)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        var inventory = new InventoryService(
            new InventoryRepository(c),
            new EfRepository<UnitBloodAttribute>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new IsbtLookupCatalog(
                new EfRepository<IsbtAboRhdCode>(c),
                new EfRepository<IsbtProductCode>(c)),
            c, _factory.Clock, _factory.CurrentUser, audit);
        return new LookbackService(
            new InventoryRepository(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<UnitModificationUnit>(c),
            new EfRepository<Issue>(c),
            new EfRepository<TransfusionEvent>(c),
            new EfRepository<Patient>(c),
            new EfRepository<LookbackNotification>(c),
            inventory,
            c, _factory.Clock, _factory.CurrentUser, audit);
    }

    private InventoryService Inventory(BloodBankDbContext c) =>
        new(
            new InventoryRepository(c),
            new EfRepository<UnitBloodAttribute>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new IsbtLookupCatalog(
                new EfRepository<IsbtAboRhdCode>(c),
                new EfRepository<IsbtProductCode>(c)),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<BloodBankLIS.Domain.Entities.Identity.User>(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    private ResultService Results(BloodBankDbContext c, FacilityPolicyService policy) =>
        new(
            new EfRepository<TestResult>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            exceptionDefinitions: new EfRepository<ExceptionDefinition>(c),
            overrides: new EfRepository<Override>(c),
            permissions: new FixedPermissionEvaluator(2),
            policy: policy);

    private static ElectronicCrossmatchEligibilityService ElectronicXm(BloodBankDbContext c) =>
        new(
            new EfRepository<Patient>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            new AntibodyScreenCompatLoader(
                new EfRepository<TestResult>(c),
                new EfRepository<TestDefinition>(c),
                new EfRepository<AntibodyHistory>(c)),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)));
}
