using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-344: seeded demo transfusions are consistent with their issue and unit.
public class DemoTransfusionConsistencyTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task EveryTransfusionOnAnIssuedIssue_IsARunningTransfusion()
    {
        await using (var db = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var verify = _factory.Create();
        var open = await (
            from t in verify.TransfusionEvents
            join i in verify.Issues on t.IssueId equals i.Id
            join u in verify.BloodUnits on t.BloodProductId equals u.Id
            where i.Status == IssueStatus.Issued
            select new { t.StopUtc, t.VolumeTransfused, UnitStatus = u.Status, u.UnitNumber }).ToListAsync();

        Assert.NotEmpty(open);
        Assert.All(open, row =>
        {
            Assert.Null(row.StopUtc);
            Assert.Null(row.VolumeTransfused);
            Assert.Equal(UnitStatus.TransfusionStarted, row.UnitStatus);
        });
        Assert.Contains(open, row => row.UnitNumber == "W0001230000099");
    }

    // TEST-BB-347: the chart and the timeline show a running transfusion as running, never as transfused.
    [Fact]
    public async Task RunningTransfusion_IsShownAsStarted_OnProductHistoryAndTimeline()
    {
        await using (var db = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(db);
        }

        await using var v = _factory.Create();
        var patient = await v.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0001");
        var history = await new PatientProductHistoryService(
            new EfRepository<Allocation>(v), new EfRepository<Crossmatch>(v), new EfRepository<Issue>(v),
            new EfRepository<Return>(v), new EfRepository<TransfusionEvent>(v), new EfRepository<BloodUnit>(v),
            new EfRepository<ProductType>(v), new EfRepository<Encounter>(v), new EfRepository<Order>(v),
            new EfRepository<Specimen>(v), new EfRepository<PatientBloodTypeHistory>(v), new EfRepository<Override>(v))
            .ListByPatientAsync(patient.Id);

        var running = Assert.Single(history, h => h.UnitNumber == "W0001230000099" && h.EventSourceType == nameof(TransfusionEvent));
        Assert.Equal(PatientProductHistoryEventType.TransfusionStarted, running.EventType);
        Assert.Equal("In progress", running.FinalDisposition);
        Assert.True(running.IsOpenAssignment);
        Assert.DoesNotContain(history, h => h.UnitNumber == "W0001230000099" && h.EventType == PatientProductHistoryEventType.Transfused);

        var timeline = await new PatientTimelineService(
            new EfRepository<Encounter>(v), new EfRepository<Order>(v), new EfRepository<Specimen>(v),
            new EfRepository<TestResult>(v), new EfRepository<PatientBloodTypeHistory>(v), new EfRepository<AntibodyHistory>(v),
            new EfRepository<SpecialTransfusionRequirement>(v), new EfRepository<SpecialRequirementDefinition>(v),
            new EfRepository<Allocation>(v), new EfRepository<Crossmatch>(v), new EfRepository<Issue>(v),
            new EfRepository<Return>(v), new EfRepository<TransfusionEvent>(v), new EfRepository<ReactionInvestigation>(v),
            new EfRepository<BloodUnit>(v), new EfRepository<Override>(v)).GetAsync(patient.Id);
        var started = Assert.Single(timeline, e => e.Title.Contains("W0001230000099") && e.Category == TimelineCategory.Transfusion);
        Assert.Contains("transfusion started", started.Title);
        Assert.Equal("In progress, not yet completed", started.Detail);
        Assert.Equal(TimelineSeverity.Warning, started.Severity);
    }
}
