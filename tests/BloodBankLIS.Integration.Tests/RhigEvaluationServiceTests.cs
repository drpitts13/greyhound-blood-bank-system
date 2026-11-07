using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Immunohematology;
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

        var future = await recorder.SaveAsync(patient.Id, new SaveRhigEvaluationRequest(
            saved.Value.Id, RhigContext.Postpartum, _factory.Clock.UtcNow.AddDays(1), RhType.Positive, FetalBleedScreen.Positive, 0.6m, RhigDecision.Candidate, 2, null));
        Assert.False(future.Succeeded);
        var edited = await recorder.SaveAsync(patient.Id, new SaveRhigEvaluationRequest(
            saved.Value.Id, RhigContext.Postpartum, delivery, RhType.Positive, FetalBleedScreen.Positive, 0.6m, RhigDecision.Candidate, 2, "Per procedure"));
        Assert.True(edited.Succeeded, edited.Error);

        var completed = await verifier.CompleteAsync(patient.Id, saved.Value.Id);
        Assert.True(completed.Succeeded, completed.Error);
        Assert.True(completed.Value!.IsCompleted);
        Assert.False((await recorder.SaveAsync(patient.Id, new SaveRhigEvaluationRequest(
            saved.Value.Id, RhigContext.Postpartum, delivery, RhType.Positive, FetalBleedScreen.Positive, 0.6m, RhigDecision.Candidate, 3, null))).Succeeded);

        var audits = await c.AuditEvents.Where(a => a.EntityType == nameof(RhigEvaluation) && a.EntityId == saved.Value.Id).Select(a => a.Reason).ToListAsync();
        Assert.Contains("RhIG evaluation recorded.", audits);
        Assert.Contains("RhIG evaluation updated.", audits);
        Assert.Contains("RhIG evaluation completed.", audits);
        Assert.Single(await Service(c).ListAsync(patient.Id));
    }
}
