using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-542: a manual result records the open, unexpired lots of the reagents used for its test; an instrument result
// records none; the lot lookback lists the result with its patient and specimen; a reagent's tests must be known tests.
public class ReagentLotTraceabilityTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ReagentLotTraceabilityTests(SqliteContextFactory factory) => _factory = factory;

    private ReagentQcService Qc(BloodBankDbContext c, bool catalog = false) =>
        new(
            new EfRepository<Reagent>(c),
            new EfRepository<ReagentLot>(c),
            new EfRepository<ReagentDailyQc>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestResultReagentLot>(c),
            new EfRepository<TestResult>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<Patient>(c),
            catalog ? new EfRepository<TestDefinition>(c) : null);

    private ResultService Results(BloodBankDbContext c) =>
        new(
            new EfRepository<TestResult>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            patients: new EfRepository<Patient>(c),
            permissions: new FixedPermissionEvaluator(2),
            reagentQc: Qc(c));

    private async Task<long> SpecimenAsync(string mrn, string accession)
    {
        await using var c = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = mrn, LastName = "Reagent", FirstName = "Trace", DateOfBirth = new DateOnly(1980, 1, 1), Sex = Sex.Female };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var specimen = await new SpecimenService(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
                c, _factory.Clock, currentUser: _factory.CurrentUser)
            .AccessionAsync(new AccessionSpecimenRequest(accession, patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(specimen.Succeeded, specimen.Error);
        return specimen.Value!.Id;
    }

    [Fact]
    public async Task ManualResult_RecordsOpenLots_AndTheLotLookbackFindsIt()
    {
        var now = _factory.Clock.UtcNow;
        long openLotId;
        await using (var c = _factory.Create())
        {
            var used = new Reagent { Code = "TR-ANTI", Name = "Trace reagent", TestCodes = "TRHGB, TRX" };
            var other = new Reagent { Code = "TR-OTHER", Name = "Other reagent", TestCodes = "TRY" };
            var retired = new Reagent { Code = "TR-OLD", Name = "Retired reagent", TestCodes = "TRHGB", IsActive = false };
            c.Reagents.AddRange(used, other, retired);
            await c.SaveChangesAsync();
            var open = new ReagentLot { ReagentId = used.Id, LotNumber = "L-OPEN", ReceivedUtc = now.AddDays(-5), OpenedUtc = now.AddDays(-4), ExpiresUtc = now.AddDays(30), State = ReagentLotState.Opened };
            c.ReagentLots.AddRange(
                open,
                new ReagentLot { ReagentId = used.Id, LotNumber = "L-EXPIRED", ReceivedUtc = now.AddDays(-50), ExpiresUtc = now.AddDays(-1), State = ReagentLotState.Opened },
                new ReagentLot { ReagentId = used.Id, LotNumber = "L-UNOPENED", ReceivedUtc = now.AddDays(-1), ExpiresUtc = now.AddDays(60), State = ReagentLotState.Received },
                new ReagentLot { ReagentId = other.Id, LotNumber = "L-OTHER", ReceivedUtc = now.AddDays(-5), ExpiresUtc = now.AddDays(30), State = ReagentLotState.Opened },
                new ReagentLot { ReagentId = retired.Id, LotNumber = "L-RETIRED", ReceivedUtc = now.AddDays(-5), ExpiresUtc = now.AddDays(30), State = ReagentLotState.Opened });
            await c.SaveChangesAsync();
            openLotId = open.Id;
        }

        var specimenId = await SpecimenAsync("MRN-TRACE-1", "ACC-TRACE-1");
        long manualId, instrumentId;
        await using (var c = _factory.Create())
        {
            var manual = await Results(c).EnterResultAsync(new EnterResultRequest(specimenId, "TRHGB", "13.1"));
            Assert.True(manual.Succeeded, manual.Error);
            // TEST-BB-544: the open lot has no QC today, so the entry says so, and the result is still recorded.
            Assert.Contains(manual.Warnings, w => w.Code == BloodBankLIS.Domain.Rules.ReagentLotUseRule.QcNotPassedCode && w.Message.Contains("L-OPEN"));
            manualId = manual.Value!.Id;
            var instrument = await Results(c).EnterResultAsync(new EnterResultRequest(specimenId, "TRX", "4+", Source: ResultSource.Instrument, SourceReference: "ECHO"));
            Assert.True(instrument.Succeeded, instrument.Error);
            instrumentId = instrument.Value!.Id;
        }

        await using var verify = _factory.Create();
        var labels = await Qc(verify).LotLabelsForResultsAsync([manualId, instrumentId]);
        Assert.Equal(["TR-ANTI L-OPEN"], labels[manualId]);
        Assert.False(labels.ContainsKey(instrumentId));
        var lots = await Qc(verify).LotsForResultAsync(manualId);
        Assert.Equal(["L-OPEN"], lots.Select(l => l.LotNumber));
        Assert.Equal("TR-ANTI", lots[0].ReagentCode);
        Assert.Empty(await Qc(verify).LotsForResultAsync(instrumentId));
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EntityType == nameof(TestResult) && a.EntityId == manualId && a.NewValueJson!.Contains("L-OPEN")));

        var lookback = await Qc(verify).ResultsForLotAsync(openLotId);
        Assert.True(lookback.Succeeded);
        var row = Assert.Single(lookback.Value!);
        Assert.Equal((manualId, "MRN-TRACE-1", "ACC-TRACE-1", "TRHGB"), (row.ResultId, row.PatientMrn, row.SpecimenAccession, row.TestCode));
        Assert.False((await Qc(verify).ResultsForLotAsync(999999)).Succeeded);
    }

    [Fact]
    public async Task ReagentTests_MustBeKnownTests_AndAreStoredTidy()
    {
        await using var c = _factory.Create();
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = "TRABSC", Name = "Trace screen", ResultValueType = ResultValueType.Coded, AllowedResultValues = "Negative\nPositive",
            IsActive = true, IsDraft = false, EffectiveUtc = DateTime.UtcNow, Version = 1
        });
        await c.SaveChangesAsync();
        var created = await Qc(c, catalog: true).SaveReagentAsync(new SaveReagentRequest("TR-SC", "Trace cells", "trabsc;  "));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("TRABSC", created.Value!.TestCodes);

        var refused = await Qc(c, catalog: true).SetReagentTestsAsync(created.Value.Id, new SetReagentTestsRequest("TRABSC, NOPE"));
        Assert.False(refused.Succeeded);
        Assert.Contains("NOPE", refused.Error);

        var cleared = await Qc(c, catalog: true).SetReagentTestsAsync(created.Value.Id, new SetReagentTestsRequest(" "));
        Assert.True(cleared.Succeeded, cleared.Error);
        Assert.Null(cleared.Value!.TestCodes);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Reagent) && a.EntityId == created.Value.Id && a.Reason == "Tests the reagent is used for changed."));
    }
}
