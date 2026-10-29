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
    public async Task StockPar_ShowsOnHandAgainstTheMinimum()
    {
        await using var context = _factory.Create();
        var product = new ProductType { ProductCode = "PLT-PAR", Name = "Platelets" };
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();
        context.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-PAR-1",
            ProductTypeId = product.Id,
            Status = UnitStatus.Available,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(5)
        });
        await context.SaveChangesAsync();
        var service = new StockParService(
            new EfRepository<StockParLevel>(context),
            new EfRepository<ProductType>(context),
            new InventoryRepository(context),
            context);
        var saved = await service.SaveAsync(new SaveStockParRequest(product.Id, AboGroup.A, 3));
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
