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

    // TEST-BB-545: the worklist line for a hand-entered result carries the reagent lots recorded with it, for the verifier.
    [Fact]
    public async Task WorklistLine_ShowsTheLotsOfItsResult()
    {
        var now = _factory.Clock.UtcNow;
        await using (var c = _factory.Create())
        {
            var reagent = new Reagent { Code = "WL-AHG", Name = "Worklist AHG", TestCodes = "WLSC" };
            c.Reagents.Add(reagent);
            await c.SaveChangesAsync();
            c.ReagentLots.Add(new ReagentLot { ReagentId = reagent.Id, LotNumber = "WL-1", ReceivedUtc = now.AddDays(-2), ExpiresUtc = now.AddDays(20), State = ReagentLotState.Opened });
            await c.SaveChangesAsync();
        }

        var specimenId = await SpecimenAsync("MRN-TRACE-WL", "ACC-TRACE-WL");
        long orderId;
        await using (var c = _factory.Create())
        {
            var specimen = await c.Specimens.SingleAsync(s => s.Id == specimenId);
            var location = new OrderingLocation { Code = "WL-LOC", Name = "Worklist", IsActive = true };
            c.OrderingLocations.Add(location);
            var encounter = new Encounter { PatientId = specimen.PatientId, VisitNumber = "VIS-TRACE-WL", Status = EncounterStatus.Active };
            c.Encounters.Add(encounter);
            await c.SaveChangesAsync();
            var order = new Order { OrderNumber = "ORD-TRACE-WL", PatientId = specimen.PatientId, EncounterId = encounter.Id, OrderingLocationId = location.Id, TestCode = "WLSC", Status = OrderStatus.InProcess, OrderedUtc = now };
            c.Orders.Add(order);
            await c.SaveChangesAsync();
            c.OrderLines.Add(new OrderLine { OrderId = order.Id, LineNumber = 1, LineName = "WLSC", TestCode = "WLSC", ResultStatus = ResultStatus.Pending, IsActive = true });
            c.OrderSpecimens.Add(new OrderSpecimen { OrderId = order.Id, SpecimenId = specimenId, IsPrimary = true });
            await c.SaveChangesAsync();
            orderId = order.Id;
            var entered = await Results(c).EnterResultAsync(new EnterResultRequest(specimenId, "WLSC", "Negative", OrderId: orderId));
            Assert.True(entered.Succeeded, entered.Error);
        }

        await using var verify = _factory.Create();
        var worklist = new TestWorklistService(
            new EfRepository<Order>(verify), new EfRepository<OrderLine>(verify), new EfRepository<OrderSpecimen>(verify),
            new EfRepository<Specimen>(verify), new EfRepository<Patient>(verify),
            new EfRepository<PatientBloodTypeHistory>(verify), new EfRepository<AntibodyHistory>(verify),
            new EfRepository<TestResult>(verify), new EfRepository<TestDefinition>(verify), new EfRepository<SpecimenTypeDefinition>(verify),
            _factory.Clock, reagentQc: Qc(verify));
        var line = Assert.Single(await worklist.ListForSpecimenAsync(specimenId), i => i.TestCode == "WLSC");
        Assert.Equal(["WL-AHG WL-1"], line.CurrentResultReagentLots);
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

    // TEST-BB-984: a reagent code already in the register (ignoring case) is refused; a reagent is renamed with a reason;
    // it is not retired while a lot is received or open, is retired with a reason once its lots are closed (then it
    // takes no new lot), and is restored; each change is audited.
    [Fact]
    public async Task Register_DuplicateRenameRetireRestore()
    {
        await using var c = _factory.Create();
        var qc = Qc(c);
        var created = await qc.SaveReagentAsync(new SaveReagentRequest("REG-713", "Anti-Fya"));
        Assert.True(created.Succeeded, created.Error);
        var id = created.Value!.Id;
        var duplicate = await qc.SaveReagentAsync(new SaveReagentRequest(" reg-713 ", "Another"));
        Assert.False(duplicate.Succeeded);
        Assert.Contains("already in the register", duplicate.Error);

        Assert.False((await qc.RenameReagentAsync(id, new RenameReagentRequest("Anti-Fy(a)", ""))).Succeeded);
        Assert.Equal("Anti-Fy(a)", (await qc.RenameReagentAsync(id, new RenameReagentRequest("Anti-Fy(a)", "Manufacturer's name"))).Value!.Name);

        var lot = await qc.ReceiveLotAsync(new ReceiveReagentLotRequest(id, "L-713", _factory.Clock.UtcNow.AddDays(30)));
        Assert.True(lot.Succeeded, lot.Error);
        var refused = await qc.SetReagentActiveAsync(id, new ReagentActiveRequest(false, "Discontinued"));
        Assert.False(refused.Succeeded);
        Assert.Contains("1 lot is still received or open", refused.Error);

        Assert.True((await qc.CloseLotAsync(lot.Value!.Id)).Succeeded);
        Assert.False((await qc.SetReagentActiveAsync(id, new ReagentActiveRequest(false, " "))).Succeeded);
        var retired = await qc.SetReagentActiveAsync(id, new ReagentActiveRequest(false, "Discontinued by the manufacturer"));
        Assert.True(retired.Succeeded, retired.Error);
        Assert.False(retired.Value!.IsActive);
        Assert.False((await qc.ReceiveLotAsync(new ReceiveReagentLotRequest(id, "L-714", _factory.Clock.UtcNow.AddDays(30)))).Succeeded);

        Assert.True((await qc.SetReagentActiveAsync(id, new ReagentActiveRequest(true, null))).Value!.IsActive);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Reagent) && e.EntityId == id && e.Reason == "Discontinued by the manufacturer"));
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Reagent) && e.EntityId == id && e.Reason == "Manufacturer's name"));
    }

    // TEST-BB-987: a received lot's expiry and number are corrected with a reason (audited before and after); no reason,
    // no change, or another lot's number is refused; a closed lot is not corrected.
    [Fact]
    public async Task CorrectLot_WithAReason()
    {
        await using var c = _factory.Create();
        var qc = Qc(c);
        var reagent = (await qc.SaveReagentAsync(new SaveReagentRequest("COR-714", "Correctable"))).Value!;
        var expires = _factory.Clock.UtcNow.AddDays(30);
        var lot = (await qc.ReceiveLotAsync(new ReceiveReagentLotRequest(reagent.Id, "L-1", expires))).Value!;
        var other = (await qc.ReceiveLotAsync(new ReceiveReagentLotRequest(reagent.Id, "L-2", expires))).Value!;

        Assert.False((await qc.CorrectLotAsync(lot.Id, new CorrectReagentLotRequest("L-1", expires, "nothing changed"))).Succeeded);
        Assert.False((await qc.CorrectLotAsync(lot.Id, new CorrectReagentLotRequest("L-1", expires.AddDays(60), ""))).Succeeded);
        Assert.False((await qc.CorrectLotAsync(lot.Id, new CorrectReagentLotRequest("l-2", expires, "typo"))).Succeeded);

        var corrected = await qc.CorrectLotAsync(lot.Id, new CorrectReagentLotRequest(" L-1A ", expires.AddDays(60), "Expiry typed wrongly at receipt"));
        Assert.True(corrected.Succeeded, corrected.Error);
        Assert.Equal(("L-1A", expires.AddDays(60)), (corrected.Value!.LotNumber, corrected.Value.ExpiresUtc));
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(ReagentLot) && e.EntityId == lot.Id && e.Reason == "Expiry typed wrongly at receipt"));

        Assert.True((await qc.CloseLotAsync(other.Id)).Succeeded);
        Assert.False((await qc.CorrectLotAsync(other.Id, new CorrectReagentLotRequest("L-2B", expires, "typo"))).Succeeded);
    }
}
