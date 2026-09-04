using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class SpecimenTypeAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SpecimenTypeAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private SpecimenTypeAdminService Types(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new SpecimenTypeAdminService(
            new EfRepository<SpecimenTypeDefinition>(c),
            new EfRepository<TestDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveSpecimenTypeDefinitionRequest Request(string code) =>
        new(code, "Temp specimen type", [], 90, "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"ST{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var denied = await Types(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == SpecimenTypeAuthorizationRule.CreateCode);
        Assert.False(await c.SpecimenTypeDefinitions.AnyAsync(d => d.Code == code));

        var allowed = await Types(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Types(c).CreateAsync(Request($"ST{Guid.NewGuid():N}"[..8].ToUpperInvariant()));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Types(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == SpecimenTypeAuthorizationRule.ActivateCode);
        Assert.False((await c.SpecimenTypeDefinitions.SingleAsync(d => d.Id == created.Value.Id)).IsActive);

        var allowed = await Types(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }
}
