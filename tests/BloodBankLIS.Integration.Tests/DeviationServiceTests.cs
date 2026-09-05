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
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .CreateAsync(new CreateDeviationRequest("QC miss", "Daily QC not documented", DeviationSeverity.Major));
        Assert.False(denied.Succeeded);
        Assert.Equal(DeviationAuthorizationRule.EvaluateManage(false).Message, denied.Error);
        Assert.Empty(context.Deviations);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.DeviationManage))
            .CreateAsync(new CreateDeviationRequest("QC miss", "Daily QC not documented", DeviationSeverity.Major));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("QC miss", allowed.Value!.Title);
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
}
