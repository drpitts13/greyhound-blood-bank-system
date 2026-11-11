using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class InterfaceTransfusionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InterfaceTransfusionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private InterfaceTransfusionService Service(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        return new InterfaceTransfusionService(
            new EfRepository<Patient>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<Issue>(c),
            new EfRepository<TransfusionEvent>(c),
            new InventoryRepository(c),
            new ReactionInvestigationService(
                new EfRepository<ReactionInvestigation>(c),
                new EfRepository<TransfusionEvent>(c),
                new InventoryRepository(c),
                c,
                _factory.Clock,
                _factory.CurrentUser,
                audit),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            permissions: permissions);
    }

    private static InterfaceTransfusionRequest DummyRequest() =>
        new("MRN-IFACE-PERM", "UNIT-IFACE", null, null, null, null, null, null, false);

    [Fact]
    public async Task DocumentAsync_WithoutTransfusionDocument_IsRejected()
    {
        await using var c = _factory.Create();
        var request = DummyRequest();
        // The class shares one database; another test records a transfusion, so count rather than expect none.
        var before = await c.TransfusionEvents.CountAsync();

        var denied = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
            .DocumentAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains("transfusion.document", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await c.TransfusionEvents.CountAsync());

        var allowed = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.TransfusionDocument))
            .DocumentAsync(request);
        Assert.False(allowed.Succeeded);
        Assert.DoesNotContain("transfusion.document", allowed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No patient found", allowed.Error, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(long UnitId, long IssueId, string Key)> SeedIssuedAsync(UnitStatus status)
    {
        var key = $"{status}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await using var c = _factory.Create();
        var productType = new ProductType { ProductCode = $"RBC-IF-{key}"[..20], Name = "Interface RBC", ComponentClass = ComponentClass.RedBloodCells };
        var patient = new Patient { MedicalRecordNumber = $"MRN-{key}", LastName = "Interface", FirstName = "Held", DateOfBirth = new DateOnly(1970, 1, 1) };
        c.ProductTypes.Add(productType);
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var unit = new BloodUnit
        {
            UnitNumber = $"U-{key}", ProductTypeId = productType.Id, Abo = AboGroup.O, RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10), Status = status,
            RecallReason = status == UnitStatus.Recalled ? "Donor subsequently reactive" : null
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        var issue = new Issue
        {
            BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddHours(-2),
            IssuedBy = "tech-test", Status = IssueStatus.Issued
        };
        c.Issues.Add(issue);
        await c.SaveChangesAsync();
        return (unit.Id, issue.Id, key);
    }

    private Task<OperationResult<string>> ReportAsync(BloodBankDbContext c, string key) =>
        Service(c, new FixedPermissionEvaluator(1, PermissionCodes.TransfusionDocument)).DocumentAsync(
            new InterfaceTransfusionRequest($"MRN-{key}", $"U-{key}", null, _factory.Clock.UtcNow.AddHours(-1), _factory.Clock.UtcNow,
                300m, "4W", "RN Smith", false));

    // TEST-BB-929 (Cycle 695, changed by OCD-083 option B in Cycle 700): a transfusion message for a unit the blood bank
    // recalled, quarantined, or recorded as damaged or missing after it was issued is recorded: the issue is transfused,
    // the event keeps the unit's status, the unit keeps it too and is marked reported transfused, and the message says it
    // is flagged for review. A unit still issued is recorded as before.
    [Theory]
    [InlineData(UnitStatus.Recalled)]
    [InlineData(UnitStatus.Quarantine)]
    [InlineData(UnitStatus.Damaged)]
    [InlineData(UnitStatus.Missing)]
    [InlineData(UnitStatus.Issued)]
    public async Task Document_UnitNoLongerIssued_IsRecordedAndFlagged(UnitStatus status)
    {
        var (unitId, issueId, key) = await SeedIssuedAsync(status);
        await using (var c = _factory.Create())
        {
            var result = await ReportAsync(c, key);
            Assert.True(result.Succeeded, result.Error);
            if (status != UnitStatus.Issued)
            {
                Assert.Contains($"the unit is {status} in the blood bank and is flagged for review (OCD-083)", result.Value);
            }
        }

        await using var verify = _factory.Create();
        var after = (await verify.BloodUnits.FindAsync(unitId))!;
        var issueAfter = (await verify.Issues.FindAsync(issueId))!;
        var transfusion = await verify.TransfusionEvents.SingleAsync(t => t.IssueId == issueId);
        Assert.Equal(IssueStatus.Transfused, issueAfter.Status);
        if (status == UnitStatus.Issued)
        {
            Assert.Equal(UnitStatus.Transfused, after.Status);
            Assert.Null(transfusion.UnitStatusAtReport);
            Assert.Null(after.TransfusionReportedUtc);
        }
        else
        {
            Assert.Equal(status, after.Status);
            Assert.Equal(status, transfusion.UnitStatusAtReport);
            Assert.Equal(_factory.Clock.UtcNow, after.TransfusionReportedUtc);
            Assert.True(await verify.AuditEvents.AnyAsync(a => a.EntityType == nameof(BloodUnit) && a.EntityId == unitId
                && a.NewValueJson != null && a.NewValueJson.Contains("TransfusedOverStatus")));
        }
    }

    // TEST-BB-937 (Cycle 700): a unit in any other status that is not issued (discarded here) is still refused and records nothing.
    [Fact]
    public async Task Document_DiscardedUnit_IsStillRefused()
    {
        var (unitId, issueId, key) = await SeedIssuedAsync(UnitStatus.Discarded);
        await using (var c = _factory.Create())
        {
            var result = await ReportAsync(c, key);
            Assert.False(result.Succeeded);
            Assert.Contains("is Discarded in the blood bank; the transfusion message is held for review and not recorded", result.Error);
        }

        await using var verify = _factory.Create();
        Assert.False(await verify.TransfusionEvents.AnyAsync(t => t.IssueId == issueId));
        Assert.Equal(UnitStatus.Discarded, (await verify.BloodUnits.FindAsync(unitId))!.Status);
    }

    // TEST-BB-938 (Cycle 700): a reported transfusion waits on the supervisor queue until reviewed; a review needs a note and
    // a permission, is recorded and audited, cannot be repeated, and takes the item off the queue. The recalled unit is no
    // longer on the Recalled board.
    [Fact]
    public async Task ReportedTransfusion_IsReviewedOnTheSupervisorQueue()
    {
        var (unitId, issueId, key) = await SeedIssuedAsync(UnitStatus.Recalled);
        await using (var c = _factory.Create())
        {
            Assert.True((await ReportAsync(c, key)).Succeeded);
        }

        await using (var c = _factory.Create())
        {
            var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
            TransfusionReportReviewService Review(IPermissionEvaluator? permissions = null) => new(
                new EfRepository<TransfusionEvent>(c), new EfRepository<BloodUnit>(c), new EfRepository<Patient>(c),
                c, _factory.Clock, _factory.CurrentUser, audit, permissions);
            var transfusionId = (await c.TransfusionEvents.SingleAsync(t => t.IssueId == issueId)).Id;

            // TEST-BB-944 (Cycle 701): the patient's product history says so on the transfusion row.
            var patientId = (await c.Issues.FindAsync(issueId))!.PatientId;
            var history = await new BloodBankLIS.Application.PatientWorkspace.PatientProductHistoryService(
                new EfRepository<Allocation>(c), new EfRepository<Crossmatch>(c), new EfRepository<Issue>(c), new EfRepository<Return>(c),
                new EfRepository<TransfusionEvent>(c), new EfRepository<BloodUnit>(c), new EfRepository<ProductType>(c),
                new EfRepository<Encounter>(c), new EfRepository<Order>(c), new EfRepository<Specimen>(c),
                new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<Override>(c)).ListByPatientAsync(patientId);
            var txRow = Assert.Single(history, r => r.EventType == PatientProductHistoryEventType.Transfused);
            Assert.Contains("Reported by the EHR while the unit was Recalled in the blood bank; review pending on the supervisor queue", txRow.EntryNotes!);

            var queue = await new SupervisorQueueService(_factory.CurrentUser, reported: Review()).ListAsync();
            var item = Assert.Single(queue.Value!, i => i.Kind == SupervisorQueueRule.KindTransfusionReported && i.ContextId == transfusionId);
            Assert.Contains($"Transfused while Recalled — U-{key}", item.Title);
            Assert.Contains("(Donor subsequently reactive)", item.Detail);

            Assert.Contains("requires inventory.release or override.approve",
                (await Review(new FixedPermissionEvaluator(1, PermissionCodes.TransfusionDocument)).ReviewAsync(transfusionId, new ReviewTransfusionReportRequest("Seen"))).Error);
            Assert.Contains("Say what the review found", (await Review().ReviewAsync(transfusionId, new ReviewTransfusionReportRequest(" "))).Error);
            var reviewed = await Review().ReviewAsync(transfusionId, new ReviewTransfusionReportRequest("Physician informed; recipient notification open"));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            Assert.Contains("already reviewed", (await Review().ReviewAsync(transfusionId, new ReviewTransfusionReportRequest("Again"))).Error);

            var after = await new SupervisorQueueService(_factory.CurrentUser, reported: Review()).ListAsync();
            Assert.DoesNotContain(after.Value!, i => i.Kind == SupervisorQueueRule.KindTransfusionReported && i.ContextId == transfusionId);
        }

        await using var verify = _factory.Create();
        var tx = await verify.TransfusionEvents.SingleAsync(t => t.IssueId == issueId);
        Assert.Equal("Physician informed; recipient notification open", tx.ReportReviewNote);
        Assert.NotNull(tx.ReportReviewedUtc);
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EntityType == nameof(TransfusionEvent) && a.EntityId == tx.Id
            && a.NewValueJson != null && a.NewValueJson.Contains(TransfusionReportReviewService.ReviewCode)));
        var unit = (await verify.BloodUnits.FindAsync(unitId))!;
        Assert.Equal(UnitStatus.Recalled, unit.Status);
    }

    [Fact]
    public async Task DocumentFromHl7Async_DoesNotRequireTransfusionDocument()
    {
        await using var c = _factory.Create();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
                .DocumentFromHl7Async(DummyRequest()));
        Assert.Contains("No patient found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
