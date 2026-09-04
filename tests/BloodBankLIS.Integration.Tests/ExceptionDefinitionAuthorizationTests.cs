using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ExceptionDefinitionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ExceptionDefinitionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ExceptionDefinitionAdminService Exceptions(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ExceptionDefinitionAdminService(
            new EfRepository<ExceptionDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveExceptionDefinitionRequest Request(string code) =>
        new(code, "Temp exception", "Override gate for tests", 2, true);

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"EXC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var denied = await Exceptions(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ExceptionCatalogAuthorizationRule.CreateCode);
        Assert.False(await c.ExceptionDefinitions.AnyAsync(e => e.RuleCode == code));

        var allowed = await Exceptions(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.RuleCode);
    }

    [Fact]
    public async Task Deactivate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Exceptions(c).CreateAsync(Request($"EXC-{Guid.NewGuid():N}"[..12].ToUpperInvariant()));
        Assert.True(created.Succeeded, created.Error);
        Assert.True(created.Value!.IsActive);

        var denied = await Exceptions(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .SetActiveAsync(created.Value.Id, false);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ExceptionCatalogAuthorizationRule.DeactivateCode);
        Assert.True((await c.ExceptionDefinitions.SingleAsync(e => e.Id == created.Value.Id)).IsActive);

        var allowed = await Exceptions(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .SetActiveAsync(created.Value.Id, false);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.False(allowed.Value!.IsActive);
    }
}
