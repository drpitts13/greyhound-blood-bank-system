using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-573: an RhIG evaluation is recorded and edited (immuno.record), shows the facts on file, is completed only
// with the decision recorded (result.verify), is audited, and is not changed once completed.
public class RhigEvaluationServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public RhigEvaluationServiceTests(SqliteContextFactory factory) => _factory = factory;

    private RhigEvaluationService Service(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<RhigEvaluation>(c), new EfRepository<Patient>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), permissions);

    [Fact]
    public async Task Evaluation_IsRecorded_Completed_AndThenFixed()
    {
        await using var c = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = $"MRN-RHIG-{Guid.NewGuid():N}"[..20], LastName = "Mother", FirstName = "Ann", DateOfBirth = new DateOnly(1992, 2, 3) };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory { PatientId = patient.Id, Abo = AboGroup.O, RhD = RhType.Negative, IsCurrent = true });
        await c.SaveChangesAsync();

        var recorder = Service(c, new FixedPermissionEvaluator(1, PermissionCodes.ImmunoRecord));
        var delivery = _factory.Clock.UtcNow.AddHours(-6);
        var saved = await recorder.SaveAsync(patient.Id, new SaveRhigEvaluationRequest(
            null, RhigContext.Postpartum, delivery, RhType.Positive, FetalBleedScreen.Positive, null, RhigDecision.Candidate, 1, null));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Contains("Patient's current Rh type on file: negative.", saved.Value!.Facts);
        Assert.Contains("Fetal bleed screen positive; no quantitation recorded.", saved.Value.Facts);

        // Completing needs result.verify, and a positive screen needs its quantitation.
        Assert.False((await recorder.CompleteAsync(patient.Id, saved.Value.Id)).Succeeded);
        var verifier = Service(c, new FixedPermissionEvaluator(1, PermissionCodes.ResultVerify));
        var incomplete = await verifier.CompleteAsync(patient.Id, saved.Value.Id);
        Assert.False(incomplete.Succeeded);
        Assert.Contains("quantitation", incomplete.Error);

        // TEST-BB-574: the open queue lists it, not yet ready.
        var queued = Assert.Single(await Service(c).ListOpenAsync(), i => i.Evaluation.Id == saved.Value.Id);
        Assert.False(queued.ReadyToComplete);
        Assert.Equal("Mother, Ann", queued.PatientName);

        var future = await recorder.SaveAsync(patient.Id, new SaveRhigEvaluationRequest(
            saved.Value.Id, RhigContext.Postpartum, _factory.Clock.UtcNow.AddDays(1), RhType.Positive, FetalBleedScreen.Positive, 0.6m, RhigDecision.Candidate, 2, null));
        Assert.False(future.Succeeded);
        var edited = await recorder.SaveAsync(patient.Id, new SaveRhigEvaluationRequest(
            saved.Value.Id, RhigContext.Postpartum, delivery, RhType.Positive, FetalBleedScreen.Positive, 0.6m, RhigDecision.Candidate, 2, "Per procedure"));
        Assert.True(edited.Succeeded, edited.Error);

        Assert.True(Assert.Single(await Service(c).ListOpenAsync(), i => i.Evaluation.Id == saved.Value.Id).ReadyToComplete);
        var completed = await verifier.CompleteAsync(patient.Id, saved.Value.Id);
        Assert.True(completed.Succeeded, completed.Error);
        Assert.True(completed.Value!.IsCompleted);
        Assert.False((await recorder.SaveAsync(patient.Id, new SaveRhigEvaluationRequest(
            saved.Value.Id, RhigContext.Postpartum, delivery, RhType.Positive, FetalBleedScreen.Positive, 0.6m, RhigDecision.Candidate, 3, null))).Succeeded);

        // TEST-BB-575: a completed candidate waits on the queue to be given; giving needs issue permission and a lot.
        Assert.True(Assert.Single(await Service(c).ListOpenAsync(), i => i.Evaluation.Id == saved.Value.Id).Evaluation.AwaitsGiving);
        Assert.False((await recorder.RecordGivenAsync(patient.Id, saved.Value.Id, new RecordRhigGivenRequest("LOT-7", 2, _factory.Clock.UtcNow, null))).Succeeded);
        var issuer = Service(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate));
        Assert.False((await issuer.RecordGivenAsync(patient.Id, saved.Value.Id, new RecordRhigGivenRequest(" ", 2, _factory.Clock.UtcNow, null))).Succeeded);
        var given = await issuer.RecordGivenAsync(patient.Id, saved.Value.Id, new RecordRhigGivenRequest(" LOT-7 ", 1, _factory.Clock.UtcNow.AddMinutes(-30), "Second vial on hold"));
        Assert.True(given.Succeeded, given.Error);
        Assert.Equal("LOT-7", given.Value!.GivenLot);
        Assert.Equal("1 vial given; 2 recommended.", given.Value.GivenDifference);
        Assert.DoesNotContain(await Service(c).ListOpenAsync(), i => i.Evaluation.Id == saved.Value.Id);
        Assert.False((await issuer.RecordGivenAsync(patient.Id, saved.Value.Id, new RecordRhigGivenRequest("LOT-8", 1, _factory.Clock.UtcNow, null))).Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(RhigEvaluation) && a.EntityId == saved.Value.Id && a.Reason == "RhIG given recorded. 1 vial given; 2 recommended."));

        // TEST-BB-576: a recall finds who received the lot (any case, trimmed); the timeline shows the RhIG events.
        var recipients = await Service(c).FindByLotAsync(" lot-7 ");
        var recipient = Assert.Single(recipients, r => r.EvaluationId == saved.Value.Id);
        Assert.Equal((patient.Id, patient.MedicalRecordNumber, 1), (recipient.PatientId, recipient.Mrn, recipient.Vials));
        Assert.Empty(await Service(c).FindByLotAsync("NO-SUCH-LOT"));
        Assert.Empty(await Service(c).FindByLotAsync(" "));

        var timeline = await new PatientTimelineService(
            new EfRepository<Encounter>(c), new EfRepository<Order>(c), new EfRepository<Specimen>(c),
            new EfRepository<TestResult>(c), new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<SpecialTransfusionRequirement>(c), new EfRepository<SpecialRequirementDefinition>(c),
            new EfRepository<Allocation>(c), new EfRepository<Crossmatch>(c), new EfRepository<Issue>(c),
            new EfRepository<Return>(c), new EfRepository<TransfusionEvent>(c), new EfRepository<ReactionInvestigation>(c),
            new EfRepository<BloodUnit>(c), new EfRepository<Override>(c), new EfRepository<RhigEvaluation>(c)).GetAsync(patient.Id);
        var rhigEvents = timeline.Where(e => e.Category == TimelineCategory.Rhig).ToList();
        Assert.Equal(3, rhigEvents.Count);
        var givenEvent = Assert.Single(rhigEvents, e => e.Title == "RhIG given: 1 vial, lot LOT-7");
        Assert.Equal(TimelineSeverity.Warning, givenEvent.Severity);
        Assert.Equal("1 vial given; 2 recommended.", givenEvent.Detail);
        Assert.Contains(rhigEvents, e => e.Title == "RhIG evaluation completed" && e.Detail == "Candidate, 2 vials recommended");

        var audits = await c.AuditEvents.Where(a => a.EntityType == nameof(RhigEvaluation) && a.EntityId == saved.Value.Id).Select(a => a.Reason).ToListAsync();
        Assert.Contains("RhIG evaluation recorded.", audits);
        Assert.Contains("RhIG evaluation updated.", audits);
        Assert.Contains("RhIG evaluation completed.", audits);
        Assert.Single(await Service(c).ListAsync(patient.Id));
    }
}
