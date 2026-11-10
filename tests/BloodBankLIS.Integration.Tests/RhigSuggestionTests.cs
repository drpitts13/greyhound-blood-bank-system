using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>OCD-069 (decided 2026-11-10): the RhIG evaluation's suggested decision and calculated dose.</summary>
public class RhigSuggestionTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private RhigEvaluationService Service(BloodBankDbContext c) =>
        new(new EfRepository<RhigEvaluation>(c), new EfRepository<Patient>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), new FixedPermissionEvaluator(1, PermissionCodes.ImmunoRecord, PermissionCodes.ResultVerify),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    private static void Setting(BloodBankDbContext c, string key, string value)
    {
        var row = c.SystemSettings.SingleOrDefault(s => s.Key == key);
        if (row is null)
        {
            c.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        }
        else
        {
            row.Value = value;
        }
    }

    // TEST-BB-869: with the dose settings unset an open evaluation suggests a decision but calculates nothing and says
    // why; once set it shows the calculated vials and the working. Completing keeps what was suggested and calculated then,
    // so a later settings change does not alter the completed record; a recorded dose differing from it is flagged.
    [Fact]
    public async Task Evaluation_SuggestsAndCalculates_AndCompletionKeepsThem()
    {
        await using var c = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = "MRN-RHIG-DOSE", LastName = "Mother", FirstName = "Bea", DateOfBirth = new DateOnly(1990, 5, 6) };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory { PatientId = patient.Id, Abo = AboGroup.A, RhD = RhType.Negative, IsCurrent = true });
        await c.SaveChangesAsync();

        var saved = await Service(c).SaveAsync(patient.Id, new SaveRhigEvaluationRequest(
            null, RhigContext.Postpartum, _factory.Clock.UtcNow.AddHours(-4), RhType.Positive, FetalBleedScreen.Positive, 0.9m,
            RhigDecision.Candidate, 2, null));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(RhigDecision.Candidate, saved.Value!.SuggestedDecision);
        Assert.Contains("infant is Rh positive", saved.Value.SuggestionReason);
        Assert.Null(saved.Value.VialsCalculated);
        Assert.Contains("not set on Facility Policies", saved.Value.DoseExplanation);

        Setting(c, FacilityPolicyKeys.RhigMlPerVial, "30");
        Setting(c, FacilityPolicyKeys.RhigMaternalBloodVolumeMl, "5000");
        Setting(c, FacilityPolicyKeys.RhigDoseRounding, "2");
        await c.SaveChangesAsync();
        var open = Assert.Single(await Service(c).ListAsync(patient.Id));
        Assert.Equal(3, open.VialsCalculated);
        Assert.StartsWith("0.9% × 5000 mL = 45 mL fetal blood", open.DoseExplanation);
        Assert.True(open.VialsDifferFromCalculated);

        var completed = await Service(c).CompleteAsync(patient.Id, saved.Value.Id);
        Assert.True(completed.Succeeded, completed.Error);
        var row = await c.RhigEvaluations.AsNoTracking().SingleAsync(e => e.Id == saved.Value.Id);
        Assert.Equal((RhigDecision.Candidate, (int?)3), (row.SuggestedDecision, row.VialsCalculated));
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(RhigEvaluation) && a.EntityId == row.Id
            && a.NewValueJson!.Contains("\"VialsCalculated\":3")));

        Setting(c, FacilityPolicyKeys.RhigDoseRounding, "1");
        await c.SaveChangesAsync();
        var after = Assert.Single(await Service(c).ListAsync(patient.Id));
        Assert.Equal((3, true), (after.VialsCalculated, after.VialsDifferFromCalculated));
        Assert.False(after.DecisionDiffersFromSuggestion);
    }
}
