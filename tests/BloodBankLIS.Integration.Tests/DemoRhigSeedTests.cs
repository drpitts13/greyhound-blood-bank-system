using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-578: the RhIG demo shows only states the application produces (completed records pass the completion check;
// RhIG given only on a completed candidate, with a lot), covers open, to-give, and given, and is added once.
public class DemoRhigSeedTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RhigDemo_IsConsistent_AndAddedOnce()
    {
        await using (var db = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using (var again = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(again);
        }

        await using var v = _factory.Create();
        var mothers = await v.Patients.AsNoTracking().Where(p => p.MedicalRecordNumber == "MRN0025" || p.MedicalRecordNumber == "MRN0026").ToListAsync();
        Assert.Equal(2, mothers.Count);
        var ids = mothers.Select(m => m.Id).ToList();
        var evaluations = await v.RhigEvaluations.AsNoTracking().Where(e => ids.Contains(e.PatientId)).ToListAsync();
        Assert.Equal(3, evaluations.Count);

        foreach (var e in evaluations.Where(e => e.CompletedUtc is not null))
        {
            Assert.Equal(RuleSeverity.Pass,
                RhigEvaluationRule.EvaluateComplete(e.Decision, e.VialsRecommended, e.DecisionNote, e.FetalBleedScreen, e.FetalBleedPercent).Severity);
        }

        Assert.All(evaluations.Where(e => e.GivenUtc is not null), e =>
        {
            Assert.NotNull(e.CompletedUtc);
            Assert.Equal(RhigDecision.Candidate, e.Decision);
            Assert.False(string.IsNullOrWhiteSpace(e.GivenLot));
        });
        Assert.Single(evaluations, e => e.CompletedUtc is null);
        Assert.Single(evaluations, e => e.CompletedUtc is not null && e.GivenUtc is null && e.Decision == RhigDecision.Candidate);
        Assert.Single(evaluations, e => e.GivenLot == "RHIG-DEMO-01");
        Assert.All(ids, id => Assert.True(v.PatientBloodTypeHistory.Any(b => b.PatientId == id && b.IsCurrent && b.RhD == RhType.Negative)));
    }
}
