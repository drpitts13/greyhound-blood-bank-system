using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
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
            new EfRepository<ExpirationModificationCode>(context),
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

    private async Task<long> EnsureExpirationCodeAsync(string code = "24H", bool isActive = true)
    {
        if (!ExpirationOffsetCode.TryParse(code, out var offset))
        {
            throw new ArgumentException($"Invalid offset code '{code}'.", nameof(code));
        }

        await using var context = _factory.Create();
        var existing = context.ExpirationModificationCodes.FirstOrDefault(c => c.Code == code);
        if (existing is not null)
        {
            existing.IsActive = isActive;
            await context.SaveChangesAsync();
            return existing.Id;
        }

        var entity = new ExpirationModificationCode
        {
            Code = code,
            OffsetAmount = offset.Amount,
            OffsetUnit = offset.Unit,
            RelativeTo = ExpirationRelativeTo.ModificationDateTime,
            IsActive = isActive,
            Version = 1
        };
        context.ExpirationModificationCodes.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    private static SaveModificationRuleRequest NewRequest(
        long sourceId,
        long targetId,
        long expirationCodeId,
        ModificationType type = ModificationType.Irradiate,
        string? reason = null,
        string? modificationCode = null) =>
        new(modificationCode ?? $"MOD-{sourceId}-{targetId}-{(int)type}", sourceId, type, targetId, expirationCodeId, "Test rule", reason);

    [Fact]
    public async Task Create_ValidRule_SucceedsAsInactiveDraft()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("CREATE");
        var codeId = await EnsureExpirationCodeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.CreateAsync(NewRequest(sourceId, targetId, codeId));

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.IsActive);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal($"MOD-{sourceId}-{targetId}-{(int)ModificationType.Irradiate}", result.Value.ModificationCode);
    }

    [Fact]
    public async Task Create_DuplicateModificationCode_IsBlocked()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("DUPCODE");
        var codeId = await EnsureExpirationCodeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);

        var first = await service.CreateAsync(NewRequest(sourceId, targetId, codeId, modificationCode: "SAME-CODE"));
        Assert.True(first.Succeeded);

        var otherSource = new ProductType { ProductCode = "MRSRC-DUPCODE2", Name = "Other", IsActive = true };
        context.ProductTypes.Add(otherSource);
        await context.SaveChangesAsync();

        var second = await service.CreateAsync(NewRequest(otherSource.Id, targetId, codeId, modificationCode: "SAME-CODE"));

        Assert.False(second.Succeeded);
        Assert.Contains(second.Evaluation!.HardStops, r => r.Code == "MODRULE.CODE.DUPLICATE");
    }

    [Fact]
    public async Task Create_MissingExpirationCode_IsBlocked()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("NOEXP");

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.CreateAsync(NewRequest(sourceId, targetId, 0));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Evaluation);
        Assert.True(result.Evaluation!.IsHardStopped);
        Assert.Contains(result.Evaluation.HardStops, r => r.Code == "MODRULE.EXPCODE.REQUIRED");
    }

    [Fact]
    public async Task Create_InactiveExpirationCode_IsBlocked()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("INACTIVEXP");
        var codeId = await EnsureExpirationCodeAsync("48H", isActive: false);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.CreateAsync(NewRequest(sourceId, targetId, codeId));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MODRULE.EXPCODE.INACTIVE");
    }

    [Fact]
    public async Task Create_InactiveSourceProduct_IsBlocked()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("INACTIVESRC", sourceActive: false);
        var codeId = await EnsureExpirationCodeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.CreateAsync(NewRequest(sourceId, targetId, codeId));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MODRULE.SOURCE.INACTIVE");
    }

    [Fact]
    public async Task Activate_DuplicateActiveTriple_IsBlocked()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("DUPTRIPLE");
        var codeId = await EnsureExpirationCodeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);

        var first = await service.CreateAsync(NewRequest(sourceId, targetId, codeId, modificationCode: "DUP-A"));
        var second = await service.CreateAsync(NewRequest(sourceId, targetId, codeId, modificationCode: "DUP-B"));
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
        var codeId = await EnsureExpirationCodeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRequest(sourceId, targetId, codeId));
        await service.ActivateAsync(created.Value!.Id, "activate");

        var update = await service.UpdateAsync(created.Value.Id, NewRequest(sourceId, targetId, codeId, reason: null));

        Assert.False(update.Succeeded);
        Assert.Contains("reason", update.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_ActiveRule_WithReason_BumpsVersionAndRecordsHistory()
    {
        var (sourceId, targetId) = await EnsureProductTypesAsync("EDITVER");
        var firstCodeId = await EnsureExpirationCodeAsync("24H");
        var secondCodeId = await EnsureExpirationCodeAsync("5D");
        long id;
        await using (var context = _factory.Create())
        {
            var service = CreateService(context);

            var created = await service.CreateAsync(NewRequest(sourceId, targetId, firstCodeId));
            var activated = await service.ActivateAsync(created.Value!.Id, "activate");
            Assert.True(activated.Succeeded);
            id = created.Value.Id;

            Assert.NotEqual(firstCodeId, secondCodeId);
            var update = await service.UpdateAsync(id, new SaveModificationRuleRequest(
                "MOD-EDITVER", sourceId, ModificationType.Irradiate, targetId, secondCodeId, "Test rule", "Clinical update"));
            Assert.True(update.Succeeded);
            Assert.Equal(secondCodeId, update.Value!.ExpirationModificationCodeId);
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
            var codeId = await EnsureExpirationCodeAsync();
            var service = CreateService(context);

            var created = await service.CreateAsync(NewRequest(sourceId, targetId, codeId));
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
        var codeId = await EnsureExpirationCodeAsync();
        long id;
        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var created = await service.CreateAsync(NewRequest(sourceId, targetId, codeId));
            id = created.Value!.Id;
        }

        await using var verify = _factory.Create();
        var audit = await verify.AuditEvents
            .Where(a => a.EntityType == nameof(ModificationRule) && a.EntityId == id && a.EventType == AuditEventType.Create)
            .ToListAsync();
        Assert.NotEmpty(audit);
    }
}
