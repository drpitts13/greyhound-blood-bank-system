using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ModificationRuleAdminServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ModificationRuleAdminServiceTests(SqliteContextFactory factory) => _factory = factory;

    private ModificationRuleAdminService CreateService(BloodBankDbContext context, IEnvironmentInfo? env = null)
    {
        env ??= new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new ModificationRuleAdminService(
            new EfRepository<ModificationRule>(context),
            new EfRepository<ProductType>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private async Task<(long SourceId, long TargetId)> EnsureProductTypesAsync(string suffix, bool sourceActive = true, bool targetActive = true)
    {
        await using var context = _factory.Create();
        var source = new ProductType { ProductCode = $"MRSRC-{suffix}", Name = "Source", IsActive = sourceActive };
        var target = new ProductType { ProductCode = $"MRTGT-{suffix}", Name = "Target", IsActive = targetActive };
        context.ProductTypes.AddRange(source, target);
        await context.SaveChangesAsync();
        return (source.Id, target.Id);
    }

    private static SaveModificationRuleRequest NewRequest(
        long sourceId,
        long targetId,
        ModificationType type = ModificationType.Irradiate,
        string offset = "24H",
        string? reason = null) =>
        new(sourceId, type, targetId, offset, "Test rule", reason);

    [Fact]
    public async Task Create_ValidRule_SucceedsAsInactiveDraft()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("CREATE");

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.CreateAsync(NewRequest(sourceId, targetId));

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.IsActive);
        Assert.Equal(1, result.Value.Version);
    }

    [Fact]
    public async Task Create_InvalidOffsetCode_IsBlocked()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("BADOFFSET");

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.CreateAsync(NewRequest(sourceId, targetId, offset: "not-a-code"));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Evaluation);
        Assert.True(result.Evaluation!.IsHardStopped);
        Assert.Contains(result.Evaluation.HardStops, r => r.Code == "MODRULE.OFFSET.INVALID");
    }

    [Fact]
    public async Task Create_InactiveSourceProduct_IsBlocked()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("INACTIVESRC", sourceActive: false);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.CreateAsync(NewRequest(sourceId, targetId));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MODRULE.SOURCE.INACTIVE");
    }

    [Fact]
    public async Task Activate_DuplicateActiveTriple_IsBlocked()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("DUPTRIPLE");

        await using var context = _factory.Create();
        var service = CreateService(context);

        var first = await service.CreateAsync(NewRequest(sourceId, targetId));
        var second = await service.CreateAsync(NewRequest(sourceId, targetId));
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);

        var activateFirst = await service.ActivateAsync(first.Value!.Id, "go live");
        Assert.True(activateFirst.Succeeded);

        var activateSecond = await service.ActivateAsync(second.Value!.Id, "should collide");
        Assert.False(activateSecond.Succeeded);
        Assert.Contains(activateSecond.Evaluation!.HardStops, r => r.Code == "MODRULE.TRIPLE.DUPLICATE");
    }

    [Fact]
    public async Task Update_ActiveRule_WithoutReason_Fails()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("EDITNOREASON");

        await using var context = _factory.Create();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRequest(sourceId, targetId));
        await service.ActivateAsync(created.Value!.Id, "activate");

        var update = await service.UpdateAsync(created.Value.Id, NewRequest(sourceId, targetId, offset: "48H", reason: null));

        Assert.False(update.Succeeded);
        Assert.Contains("reason", update.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_ActiveRule_WithReason_BumpsVersionAndRecordsHistory()
    {
        long id;
        await using (var context = _factory.Create())
        {
            var (sourceId, targetId) = await EnsureProductTypesAsync("EDITVER");
            var service = CreateService(context);

            var created = await service.CreateAsync(NewRequest(sourceId, targetId));
            await service.ActivateAsync(created.Value!.Id, "activate");
            id = created.Value.Id;

            var update = await service.UpdateAsync(id, NewRequest(sourceId, targetId, offset: "5D", reason: "Clinical update"));
            Assert.True(update.Succeeded);
            Assert.Equal(2, update.Value!.Version);
            Assert.Equal("5D", update.Value.ExpirationOffsetCode);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.ConfigurationChangeHistory
                .Where(h => h.EntityType == nameof(ModificationRule) && h.EntityId == id && h.Action == ConfigChangeAction.Update)
                .SingleAsync();
            Assert.Equal("Clinical update", history.ChangeReason);
        }
    }

    [Fact]
    public async Task Deactivate_SetsInactive_AndRecordsHistory()
    {
        long id;
        await using (var context = _factory.Create())
        {
            var (sourceId, targetId) = await EnsureProductTypesAsync("DEACT");
            var service = CreateService(context);

            var created = await service.CreateAsync(NewRequest(sourceId, targetId));
            await service.ActivateAsync(created.Value!.Id, "activate");
            id = created.Value.Id;

            var deactivate = await service.DeactivateAsync(id, "Retire");
            Assert.True(deactivate.Succeeded);
            Assert.False(deactivate.Value!.IsActive);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.ConfigurationChangeHistory
                .Where(h => h.EntityType == nameof(ModificationRule) && h.EntityId == id && h.Action == ConfigChangeAction.Deactivate)
                .SingleAsync();
            Assert.Equal("Retire", history.ChangeReason);
        }
    }

    [Fact]
    public async Task Create_WritesCreateAudit()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("AUDIT");
        long id;
        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var created = await service.CreateAsync(NewRequest(sourceId, targetId));
            id = created.Value!.Id;
        }

        await using var verify = _factory.Create();
        var audit = await verify.AuditEvents
            .Where(a => a.EntityType == nameof(ModificationRule) && a.EntityId == id && a.EventType == AuditEventType.Create)
            .ToListAsync();
        Assert.NotEmpty(audit);
    }
}
