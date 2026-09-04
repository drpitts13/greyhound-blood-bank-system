using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class TestGrouperAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public TestGrouperAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private TestGrouperAdminService Groupers(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new TestGrouperAdminService(
            new EfRepository<TestGrouper>(c),
            new EfRepository<TestDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static async Task<SaveTestGrouperRequest> SeedMemberAsync(BloodBankDbContext c)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var testCode = $"T{suffix}";
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = testCode,
            Name = "Member",
            IsActive = true,
            IsDraft = false
        });
        await c.SaveChangesAsync();
        return new SaveTestGrouperRequest(
            $"GRP-{suffix}",
            "Temp grouper",
            [new TestGrouperMemberDto(testCode, 1)],
            "Catalog.");
    }

    [Fact]
    public async Task Create_WithoutAdminTestsManage_IsRejected()
    {
        await using var c = _factory.Create();
        var request = await SeedMemberAsync(c);

        var denied = await Groupers(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == TestGrouperAuthorizationRule.CreateCode);
        Assert.False(await c.TestGroupers.AnyAsync(g => g.Code == request.Code));

        var allowed = await Groupers(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .CreateAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(request.Code, allowed.Value!.Code);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Groupers(c).CreateAsync(await SeedMemberAsync(c));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Groupers(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == TestGrouperAuthorizationRule.ActivateCode);
        Assert.False((await c.TestGroupers.SingleAsync(g => g.Id == created.Value.Id)).IsActive);

        var allowed = await Groupers(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }
}
