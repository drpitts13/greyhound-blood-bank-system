using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>The RhIG lot register (end-user testing pass 5, 2026-11-12).</summary>
public class RhigLotServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private RhigLotService Lots(BloodBankDbContext c) =>
        new(new EfRepository<RhigLot>(c), new EfRepository<RhigEvaluation>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private RhigEvaluationService Evaluations(BloodBankDbContext c) =>
        new(new EfRepository<RhigEvaluation>(c), new EfRepository<Patient>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate),
            lots: new EfRepository<RhigLot>(c));

    private async Task<(long PatientId, long EvaluationId)> CandidateAsync(BloodBankDbContext c, string mrn)
    {
        var patient = new Patient { MedicalRecordNumber = mrn, LastName = "Mother", FirstName = "Ann", DateOfBirth = new DateOnly(1990, 1, 1) };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var evaluation = new RhigEvaluation
        {
            PatientId = patient.Id, Context = RhigContext.Postpartum, EventUtc = _factory.Clock.UtcNow.AddHours(-6),
            InfantRh = RhType.Positive, FetalBleedScreen = FetalBleedScreen.Negative, Decision = RhigDecision.Candidate,
            VialsRecommended = 1, RecordedBy = "tech", RecordedUtc = _factory.Clock.UtcNow.AddHours(-5),
            CompletedBy = "lead", CompletedUtc = _factory.Clock.UtcNow.AddHours(-4)
        };
        c.RhigEvaluations.Add(evaluation);
        await c.SaveChangesAsync();
        return (patient.Id, evaluation.Id);
    }

    // End-user testing pass 69: "today" is the facility's day. At 20:00 in Chicago (02:00 UTC the next day) a lot expiring
    // that day is still in date; on the UTC day it was refused as expired.
    [Fact]
    public async Task Receive_UsesTheFacilityDay()
    {
        await using var c = _factory.Create();
        var evening = new FixedClock(new DateTime(2026, 11, 13, 2, 0, 0, DateTimeKind.Utc));
        var chicago = BloodBankLIS.Infrastructure.Common.FacilityTimeZone.FromId("America/Chicago");
        RhigLotService Lots(BloodBankLIS.Application.Abstractions.IFacilityTimeZone? zone) =>
            new(new EfRepository<RhigLot>(c), new EfRepository<RhigEvaluation>(c), c, evening, _factory.CurrentUser,
                new AuditWriter(c, evening, _factory.CurrentUser), zone);

        var localDay = await Lots(chicago).ReceiveAsync(new ReceiveRhigLotRequest("RH-EVE-1", "RhIG 300 ug", new DateOnly(2026, 11, 12), 5));
        Assert.True(localDay.Succeeded, localDay.Error);
        Assert.False((await Lots(null).ReceiveAsync(new ReceiveRhigLotRequest("RH-EVE-2", "RhIG 300 ug", new DateOnly(2026, 11, 12), 5))).Succeeded);
    }

    // TEST-BB-1004: with no lot registered the lot given is taken as typed; once lots are registered, an unknown lot, a
    // retired lot and a lot expired when given are refused, a registered lot is recorded under its registered number
    // (any case), and the register counts vials given; receiving and retiring are audited.
    [Fact]
    public async Task Register_ChecksTheLotGiven()
    {
        await using var c = _factory.Create();
        var today = DateOnly.FromDateTime(_factory.Clock.UtcNow);
        var (typedPatient, typedEval) = await CandidateAsync(c, "MRN-RHIG-TYPED");
        var typed = await Evaluations(c).RecordGivenAsync(typedPatient, typedEval, new RecordRhigGivenRequest("ANY-LOT", 1, _factory.Clock.UtcNow, null));
        Assert.True(typed.Succeeded, typed.Error);

        var good = await Lots(c).ReceiveAsync(new ReceiveRhigLotRequest("RH-2611", "RhIG 300 ug", today.AddMonths(10), 10));
        Assert.True(good.Succeeded, good.Error);
        Assert.False((await Lots(c).ReceiveAsync(new ReceiveRhigLotRequest("rh-2611", "RhIG 300 ug", today.AddMonths(10), 10))).Succeeded);
        var old = await Lots(c).ReceiveAsync(new ReceiveRhigLotRequest("RH-OLD", "RhIG 300 ug", today, 5));
        Assert.True(old.Succeeded, old.Error);
        var gone = await Lots(c).ReceiveAsync(new ReceiveRhigLotRequest("RH-GONE", "RhIG 300 ug", today.AddMonths(10), 5));
        Assert.False((await Lots(c).RetireAsync(gone.Value!.Id, new RetireRhigLotRequest(""))).Succeeded);
        Assert.True((await Lots(c).RetireAsync(gone.Value.Id, new RetireRhigLotRequest("Recalled by the manufacturer"))).Succeeded);

        var (patient, eval) = await CandidateAsync(c, "MRN-RHIG-REG");
        Assert.Contains("not in the RhIG lot register",
            (await Evaluations(c).RecordGivenAsync(patient, eval, new RecordRhigGivenRequest("RH-26II", 1, _factory.Clock.UtcNow, null))).Error);
        Assert.Contains("retired",
            (await Evaluations(c).RecordGivenAsync(patient, eval, new RecordRhigGivenRequest("RH-GONE", 1, _factory.Clock.UtcNow, null))).Error);
        _factory.Clock.UtcNow = _factory.Clock.UtcNow.Date.AddDays(1).AddHours(1);
        Assert.Contains("expired",
            (await Evaluations(c).RecordGivenAsync(patient, eval, new RecordRhigGivenRequest("RH-OLD", 1, _factory.Clock.UtcNow, null))).Error);

        var given = await Evaluations(c).RecordGivenAsync(patient, eval, new RecordRhigGivenRequest(" rh-2611 ", 1, _factory.Clock.UtcNow, null));
        Assert.True(given.Succeeded, given.Error);
        Assert.Equal("RH-2611", given.Value!.GivenLot);
        var listed = Assert.Single(await Lots(c).ListAsync(), l => l.LotNumber == "RH-2611");
        Assert.Equal((1, 9), (listed.VialsGiven, listed.VialsLeft));
        Assert.DoesNotContain(await Lots(c).ListAsync(), l => l.LotNumber == "RH-GONE");
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(RhigLot) && e.Reason == "Recalled by the manufacturer"));
    }
}
