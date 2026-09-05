using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class FacilityPolicyAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public FacilityPolicyAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private FacilityPolicyAdminService Policies(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new FacilityPolicyAdminService(
            new EfRepository<SystemSetting>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    [Fact]
    public async Task Update_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var listed = await Policies(c).ListAsync();
        var retention = listed.Single(p => p.Key == FacilityPolicyKeys.RetentionYears);
        var original = retention.Value;

        var denied = await Policies(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .UpdateAsync(retention.Id, new SaveFacilityPolicyRequest("12", "Extend retention to twelve years."));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == FacilityPolicyAuthorizationRule.UpdateCode);
        Assert.Equal(original, (await c.SystemSettings.SingleAsync(s => s.Id == retention.Id)).Value);

        var allowed = await Policies(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .UpdateAsync(retention.Id, new SaveFacilityPolicyRequest("12", "Extend retention to twelve years."));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("12", allowed.Value!.Value);
    }
}
