using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class DeviationServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public DeviationServiceTests(SqliteContextFactory factory) => _factory = factory;

    private DeviationService CreateService(
        BloodBankDbContext context,
        IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<Deviation>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: permissions);

    [Fact]
    public async Task Create_WithoutDeviationManage_IsHardStopped()
    {
        await using var context = _factory.Create();
        var deniedTitle = $"QC miss deny {Guid.NewGuid():N}";
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .CreateAsync(new CreateDeviationRequest(deniedTitle, "Daily QC not documented", DeviationSeverity.Major));
        Assert.False(denied.Succeeded);
        Assert.Equal(DeviationAuthorizationRule.EvaluateManage(false).Message, denied.Error);
        Assert.DoesNotContain(context.Deviations.ToList(), d => d.Title == deniedTitle);

        var allowedTitle = $"QC miss allow {Guid.NewGuid():N}";
        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.DeviationManage))
            .CreateAsync(new CreateDeviationRequest(allowedTitle, "Daily QC not documented", DeviationSeverity.Major));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(allowedTitle, allowed.Value!.Title);
    }

    [Fact]
    public async Task UpdateStatus_WithoutDeviationManage_IsHardStopped()
    {
        await using var context = _factory.Create();
        var created = await CreateService(context).CreateAsync(
            new CreateDeviationRequest("QC miss", "Daily QC not documented", DeviationSeverity.Major));
        Assert.True(created.Succeeded, created.Error);

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .UpdateStatusAsync(created.Value!.Id, DeviationStatus.Closed, "Retrained staff");
        Assert.False(denied.Succeeded);
        Assert.Equal(DeviationAuthorizationRule.EvaluateManage(false).Message, denied.Error);
        Assert.Equal(DeviationStatus.Open, (await context.Deviations.FindAsync(created.Value.Id))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.DeviationManage))
            .UpdateStatusAsync(created.Value.Id, DeviationStatus.Closed, "Retrained staff");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(DeviationStatus.Closed, allowed.Value!.Status);
    }

    [Fact]
    public async Task CreateAndUpdateStatus_WriteDeviation()
    {
        await using var context = _factory.Create();
        var title = $"QC miss {Guid.NewGuid():N}";
        var created = await CreateService(context).CreateAsync(
            new CreateDeviationRequest(title, "Daily QC not documented", DeviationSeverity.Major, "TestResult", 1));
        Assert.True(created.Succeeded, created.Error);

        var updated = await CreateService(context).UpdateStatusAsync(
            created.Value!.Id, DeviationStatus.Closed, "Retrained staff");
        Assert.True(updated.Succeeded, updated.Error);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EntityType == nameof(Deviation)
            && e.EventType == AuditEventType.Deviation
            && e.EntityId == created.Value.Id
            && e.Reason == "Deviation created.");
        Assert.Contains(events, e =>
            e.EntityType == nameof(Deviation)
            && e.EventType == AuditEventType.Deviation
            && e.EntityId == created.Value.Id
            && e.Reason == "Deviation status updated.");
    }

    // TEST-BB-489: closing without a corrective action and blanking a recorded one are refused; reopening clears the
    // close stamp, which the audit keeps; the list reads open, most severe, oldest first.
    [Fact]
    public async Task Close_NeedsACorrectiveAction_AndReopenKeepsTheEarlierCloseInTheAudit()
    {
        await using var context = _factory.Create();
        var created = await CreateService(context).CreateAsync(
            new CreateDeviationRequest($"Label jam {Guid.NewGuid():N}", "Printer jammed during relabel", DeviationSeverity.Minor));
        Assert.True(created.Succeeded, created.Error);
        var id = created.Value!.Id;

        var bare = await CreateService(context).UpdateStatusAsync(id, DeviationStatus.Closed, null);
        Assert.False(bare.Succeeded);
        Assert.Contains("corrective action", bare.Error);
        Assert.Equal(DeviationStatus.Open, (await context.Deviations.FindAsync(id))!.Status);

        Assert.True((await CreateService(context).UpdateStatusAsync(id, DeviationStatus.Closed, "  Printer serviced  ")).Succeeded);
        var closed = (await context.Deviations.FindAsync(id))!;
        Assert.Equal("Printer serviced", closed.CorrectiveAction);
        Assert.Equal(_factory.CurrentUser.UserName, closed.ClosedBy);

        var blanked = await CreateService(context).UpdateStatusAsync(id, DeviationStatus.Closed, "");
        Assert.False(blanked.Succeeded);
        Assert.Equal("Printer serviced", (await context.Deviations.FindAsync(id))!.CorrectiveAction);

        Assert.True((await CreateService(context).UpdateStatusAsync(id, DeviationStatus.UnderReview, null)).Succeeded);
        var reopened = (await context.Deviations.FindAsync(id))!;
        Assert.Null(reopened.ClosedBy);
        Assert.Null(reopened.ClosedUtc);
        Assert.Equal("Printer serviced", reopened.CorrectiveAction);
        Assert.Contains(context.AuditEvents.Where(e => e.EntityType == nameof(Deviation) && e.EntityId == id).ToList(),
            e => e.OldValueJson != null && e.OldValueJson.Contains(_factory.CurrentUser.UserName) && e.OldValueJson.Contains("ClosedBy", StringComparison.OrdinalIgnoreCase));

        // Closing again keeps the recorded action.
        Assert.True((await CreateService(context).UpdateStatusAsync(id, DeviationStatus.Closed, null)).Succeeded);

        var at = new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);
        DeviationDto Row(long rowId, DeviationSeverity severity, DeviationStatus status, int hoursAgo) =>
            new(rowId, "t", "d", severity, status, null, null, null, "x", at.AddHours(-hoursAgo));
        Assert.Equal([3L, 2L, 4L, 1L], DeviationListView.Order([
            Row(1, DeviationSeverity.Critical, DeviationStatus.Closed, 50),
            Row(2, DeviationSeverity.Major, DeviationStatus.Open, 1),
            Row(3, DeviationSeverity.Critical, DeviationStatus.UnderReview, 2),
            Row(4, DeviationSeverity.Minor, DeviationStatus.Open, 30)
        ]).Select(r => r.Id));
    }

    // TEST-BB-491: a record's page lists only deviations linked to it (older "Unit" spelling included), open first.
    [Fact]
    public async Task ListForRecord_MatchesTheRecordAndOlderSpellings_OpenFirst()
    {
        var unitId = 900_000 + Random.Shared.Next(1, 99_999);
        await using var context = _factory.Create();
        context.Deviations.AddRange(
            new Deviation { Title = "Older closed", Description = "d", Status = DeviationStatus.Closed, CorrectiveAction = "done", ContextType = "Unit", ContextId = unitId, ReportedUtc = _factory.Clock.UtcNow.AddDays(-5) },
            new Deviation { Title = "Open on the unit", Description = "d", ContextType = "BloodUnit", ContextId = unitId, ReportedUtc = _factory.Clock.UtcNow.AddDays(-1) },
            new Deviation { Title = "Same id, a patient", Description = "d", ContextType = "Patient", ContextId = unitId, ReportedUtc = _factory.Clock.UtcNow },
            new Deviation { Title = "Another unit", Description = "d", ContextType = "BloodUnit", ContextId = unitId + 1, ReportedUtc = _factory.Clock.UtcNow });
        await context.SaveChangesAsync();

        var rows = await CreateService(context).ListForRecordAsync(DeviationRecordTypes.BloodUnit, unitId);
        Assert.Equal(["Open on the unit", "Older closed"], rows.Select(r => r.Title));

        var patient = await CreateService(context).ListForRecordAsync(DeviationRecordTypes.Patient, unitId);
        Assert.Equal("Same id, a patient", Assert.Single(patient).Title);
        Assert.Empty(await CreateService(context).ListForRecordAsync("Specimen", unitId));
    }
}
