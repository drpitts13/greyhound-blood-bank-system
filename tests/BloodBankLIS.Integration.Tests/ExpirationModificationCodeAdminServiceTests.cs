using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ExpirationModificationCodeAdminServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ExpirationModificationCodeAdminServiceTests(SqliteContextFactory factory) => _factory = factory;

    private ExpirationModificationCodeAdminService CreateService(BloodBankDbContext context, IEnvironmentInfo? env = null)
    {
        env ??= new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new ExpirationModificationCodeAdminService(
            new EfRepository<ExpirationModificationCode>(context),
            new EfRepository<ModificationRule>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private static SaveExpirationModificationCodeRequest NewRequest(
        string code = "24H",
        int amount = 24,
        ExpirationOffsetUnit unit = ExpirationOffsetUnit.Hours,
        ExpirationRelativeTo relativeTo = ExpirationRelativeTo.ModificationDateTime,
        string? reason = null) =>
        new(code, amount, unit, relativeTo, "Test expiration code", reason);

    [Fact]
    public async Task Create_ValidCode_SucceedsAsInactiveDraft()
    {
        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.CreateAsync(NewRequest("12H", 12));

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.IsActive);
        Assert.Equal("12H", result.Value.Code);
        Assert.Equal(ExpirationRelativeTo.ModificationDateTime, result.Value.RelativeTo);
    }

    [Fact]
    public async Task Create_ZeroAmount_IsBlocked()
    {
        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.CreateAsync(NewRequest("0H", 0));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "EXPCODE.AMOUNT.INVALID");
    }

    [Fact]
    public async Task Create_DuplicateCode_IsBlocked()
    {
        await using var context = _factory.Create();
        var service = CreateService(context);

        var first = await service.CreateAsync(NewRequest("36H", 36));
        Assert.True(first.Succeeded);

        var second = await service.CreateAsync(NewRequest("36H", 36));
        Assert.False(second.Succeeded);
        Assert.Contains(second.Evaluation!.HardStops, r => r.Code == "EXPCODE.CODE.DUPLICATE");
    }

    [Fact]
    public async Task Update_ActiveCode_WithoutReason_Fails()
    {
        await using var context = _factory.Create();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRequest("6H", 6));
        await service.ActivateAsync(created.Value!.Id, "activate");

        var update = await service.UpdateAsync(created.Value.Id, NewRequest("6H", 8, reason: null));

        Assert.False(update.Succeeded);
        Assert.Contains("reason", update.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deactivate_WhenUsedByActiveRule_Fails()
    {
        long codeId;
        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var created = await service.CreateAsync(NewRequest("18H", 18));
            await service.ActivateAsync(created.Value!.Id, "activate");
            codeId = created.Value.Id;

            context.ProductTypes.AddRange(
                new BloodBankLIS.Domain.Entities.ProductType { ProductCode = "EXP-SRC", Name = "Source", IsActive = true },
                new BloodBankLIS.Domain.Entities.ProductType { ProductCode = "EXP-TGT", Name = "Target", IsActive = true });
            await context.SaveChangesAsync();
            var products = context.ProductTypes.ToList();
            context.ModificationRules.Add(new ModificationRule
            {
                ModificationCode = "IRR-EXP-SRC",
                SourceProductTypeId = products[0].Id,
                TargetProductTypeId = products[1].Id,
                ModificationType = ModificationType.Irradiate,
                ExpirationModificationCodeId = codeId,
                IsActive = true,
                Version = 1
            });
            await context.SaveChangesAsync();
        }

        await using var verify = _factory.Create();
        var deactivate = await CreateService(verify).DeactivateAsync(codeId, "retire");
        Assert.False(deactivate.Succeeded);
        Assert.Contains("active modification rule", deactivate.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WritesCreateAudit()
    {
        long id;
        await using (var context = _factory.Create())
        {
            var created = await CreateService(context).CreateAsync(NewRequest("7H", 7));
            id = created.Value!.Id;
        }

        await using var verify = _factory.Create();
        var audit = await verify.AuditEvents
            .Where(a => a.EntityType == nameof(ExpirationModificationCode) && a.EntityId == id && a.EventType == AuditEventType.Create)
            .ToListAsync();
        Assert.NotEmpty(audit);
    }
}
