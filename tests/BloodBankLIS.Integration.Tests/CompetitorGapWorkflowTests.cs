using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class CompetitorGapWorkflowTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public CompetitorGapWorkflowTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task ReagentQc_ReviewerMustDiffer_AndWorklistClearsAfterReview()
    {
        await using var context = _factory.Create();
        var service = Qc(context, _factory.CurrentUser);
        var reagent = await service.SaveReagentAsync(new SaveReagentRequest("ANTI-A", "Anti-A"));
        var lot = await service.ReceiveLotAsync(new ReceiveReagentLotRequest(reagent.Value!.Id, "L-100", _factory.Clock.UtcNow.AddDays(30)));
        await service.OpenLotAsync(lot.Value!.Id);
        var overdue = await service.ListWorkAsync();
        Assert.Contains(overdue, row => row.Kind == ReagentQcWorklistRule.Overdue);

        var qc = await service.RecordQcAsync(new RecordReagentQcRequest(lot.Value.Id, "Positive", "Positive", true));
        Assert.True(qc.Succeeded, qc.Error);
        var self = await service.ReviewAsync(qc.Value!.Id);
        Assert.False(self.Succeeded);

        var reviewed = await Qc(context, new TestCurrentUser("lead-test", "WORKSTATION-1")).ReviewAsync(qc.Value.Id);
        Assert.True(reviewed.Succeeded, reviewed.Error);
        var work = await service.ListWorkAsync();
        Assert.DoesNotContain(work, row => row.QcId == qc.Value.Id);
        Assert.DoesNotContain(work, row => row.LotId == lot.Value.Id && row.Kind == ReagentQcWorklistRule.Overdue);
    }

    [Fact]
    public async Task ReagentLot_Lifecycle_RefusesExpiredAndDuplicateLots_AndAuditsOpenAndClose()
    {
        // TEST-BB-559
        await using var context = _factory.Create();
        var service = Qc(context, _factory.CurrentUser);
        var reagent = await service.SaveReagentAsync(new SaveReagentRequest("ANTI-D-LC", "Anti-D lifecycle"));
        var now = _factory.Clock.UtcNow;

        var expired = await service.ReceiveLotAsync(new ReceiveReagentLotRequest(reagent.Value!.Id, "LC-OLD", now.AddDays(-1)));
        Assert.False(expired.Succeeded);
        Assert.Contains("expired", expired.Error);

        var lot = await service.ReceiveLotAsync(new ReceiveReagentLotRequest(reagent.Value.Id, "LC-1", now.AddDays(20)));
        Assert.True(lot.Succeeded, lot.Error);
        var duplicate = await service.ReceiveLotAsync(new ReceiveReagentLotRequest(reagent.Value.Id, " lc-1 ", now.AddDays(25)));
        Assert.False(duplicate.Succeeded);
        Assert.Contains("already has", duplicate.Error);

        Assert.True((await service.OpenLotAsync(lot.Value!.Id)).Succeeded);
        Assert.True((await service.CloseLotAsync(lot.Value.Id)).Succeeded);
        Assert.False((await service.CloseLotAsync(lot.Value.Id)).Succeeded);
        Assert.False((await service.OpenLotAsync(lot.Value.Id)).Succeeded);
        var messages = await context.AuditEvents
            .Where(a => a.EntityType == nameof(ReagentLot) && a.EntityId == lot.Value.Id)
            .Select(a => a.Reason)
            .ToListAsync();
        Assert.Contains("Reagent lot opened.", messages);
        Assert.Contains("Reagent lot closed.", messages);

        // Once closed, the number may be received again.
        var again = await service.ReceiveLotAsync(new ReceiveReagentLotRequest(reagent.Value.Id, "LC-1", now.AddDays(25)));
        Assert.True(again.Succeeded, again.Error);

        // A lot that has since expired can be neither opened nor tested.
        var stale = await context.ReagentLots.SingleAsync(l => l.Id == again.Value!.Id);
        stale.ExpiresUtc = now.AddHours(-1);
        await context.SaveChangesAsync();
        var open = await service.OpenLotAsync(stale.Id);
        Assert.False(open.Succeeded);
        Assert.Contains("close it and open a new lot", open.Error);
        var qc = await service.RecordQcAsync(new RecordReagentQcRequest(stale.Id, "Positive", "Positive", true));
        Assert.False(qc.Succeeded);
        Assert.Contains("close it rather than testing it", qc.Error);
    }

    [Fact]
    public async Task PatientResult_StaysOpenWhenReagentQcPolicyIsOff()
    {
        await using var context = _factory.Create();
        var qc = Qc(context, _factory.CurrentUser);
        var reagent = await qc.SaveReagentAsync(new SaveReagentRequest("ANTI-B", "Anti-B"));
        await qc.ReceiveLotAsync(new ReceiveReagentLotRequest(reagent.Value!.Id, "L-200", _factory.Clock.UtcNow.AddDays(10)));
        var results = Results(context, qc);
        var open = await results.SaveTestResultAsync(DummyResult());
        Assert.False(open.Succeeded);
        Assert.Null(open.Evaluation);
        Assert.Equal("Specimen not found.", open.Error);

        context.SystemSettings.Add(new SystemSetting
        {
            Key = FacilityPolicyKeys.BlockResultsWhenReagentQcOpen,
            Value = "true"
        });
        await context.SaveChangesAsync();
        var blocked = await Results(context, qc).SaveTestResultAsync(DummyResult());
        Assert.Contains(blocked.Evaluation!.HardStops, stop => stop.Code == ReagentQcResultGate.Code);
    }

    [Fact]
    public async Task OverdueDisposition_ListsOnlyUndocumentedIssuedUnits()
    {
        await using var context = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-FATE",
            LastName = "Lane",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1970, 4, 1)
        };
        var product = new ProductType { ProductCode = "RBC-FATE", Name = "Red cells" };
        context.Patients.Add(patient);
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();
        var overdueUnit = new BloodUnit
        {
            UnitNumber = "U-FATE-OLD",
            ProductTypeId = product.Id,
            Status = UnitStatus.Issued,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Abo = AboGroup.O,
            RhD = RhType.Positive
        };
        var recentUnit = new BloodUnit
        {
            UnitNumber = "U-FATE-NEW",
            ProductTypeId = product.Id,
            Status = UnitStatus.Issued,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Abo = AboGroup.O,
            RhD = RhType.Positive
        };
        context.BloodUnits.AddRange(overdueUnit, recentUnit);
        await context.SaveChangesAsync();
        context.Issues.AddRange(
            new Issue
            {
                BloodProductId = overdueUnit.Id,
                PatientId = patient.Id,
                IssuedUtc = _factory.Clock.UtcNow.AddHours(-30),
                IssuedBy = "tech-test",
                Status = IssueStatus.Issued
            },
            new Issue
            {
                BloodProductId = recentUnit.Id,
                PatientId = patient.Id,
                IssuedUtc = _factory.Clock.UtcNow.AddHours(-2),
                IssuedBy = "tech-test",
                Status = IssueStatus.Issued
            });
        await context.SaveChangesAsync();

        var board = Board(context);
        var rows = await board.ListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(overdueUnit.Id, row.UnitId);

        context.TransfusionEvents.Add(new TransfusionEvent
        {
            IssueId = row.IssueId,
            BloodProductId = overdueUnit.Id,
            PatientId = patient.Id,
            DocumentedBy = "tech-test"
        });
        await context.SaveChangesAsync();
        Assert.Empty(await board.ListAsync());
    }

    [Fact]
    public async Task MinimumStock_ShowsOnHandAgainstTheMinimum()
    {
        await using var context = _factory.Create();
        var product = new ProductType { ProductCode = "PLT-MIN", Name = "Platelets" };
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();
        context.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-MIN-1",
            ProductTypeId = product.Id,
            Status = UnitStatus.Available,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(5)
        });
        await context.SaveChangesAsync();
        var service = new MinimumStockService(
            new EfRepository<MinimumStockLevel>(context),
            new EfRepository<ProductType>(context),
            new InventoryRepository(context),
            context);
        var saved = await service.SaveAsync(new SaveMinimumStockRequest(product.Id, AboGroup.A, 3));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(1, saved.Value!.OnHand);
        Assert.Equal(2, saved.Value.Shortfall);
        Assert.Empty(context.Set<BillingEvent>());
    }

    private ReagentQcService Qc(BloodBankDbContext context, ICurrentUser user) =>
        new(
            new EfRepository<Reagent>(context),
            new EfRepository<ReagentLot>(context),
            new EfRepository<ReagentDailyQc>(context),
            context,
            _factory.Clock,
            user,
            new AuditWriter(context, _factory.Clock, user));

    private FinalDispositionBoardService Board(BloodBankDbContext context) =>
        new(
            new EfRepository<Issue>(context),
            new EfRepository<TransfusionEvent>(context),
            new EfRepository<Return>(context),
            new InventoryRepository(context),
            new EfRepository<Patient>(context),
            new FacilityPolicyService(new EfRepository<SystemSetting>(context)),
            _factory.Clock);

    private ResultService Results(BloodBankDbContext context, ReagentQcService qc) =>
        new(
            new EfRepository<TestResult>(context),
            new EfRepository<Specimen>(context),
            new EfRepository<PatientBloodTypeHistory>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            policy: new FacilityPolicyService(new EfRepository<SystemSetting>(context)),
            reagentQc: qc);

    private static SaveTestResultRequest DummyResult() =>
        new(0, 0, 0, "ABO", null, null, null, null, null, null, false, null, null, null, null, null);
}
