using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Identity;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class UserAdminAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public UserAdminAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private UserAdminService Users(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new UserAdminService(
            new EfRepository<User>(c),
            new EfRepository<Role>(c),
            new EfRepository<Permission>(c),
            new EfRepository<UserRole>(c),
            new EfRepository<RolePermission>(c),
            new IdentityAdminStore(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    [Fact]
    public async Task AssignRoles_WithoutAdminUsersManage_IsRejected()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var tech = await c.Users.SingleAsync(u => u.UserName == "tech1");
        var request = new AssignRolesRequest(["Supervisor"], "Coverage.");

        var denied = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .AssignRolesAsync(tech.Id, request);
        Assert.False(denied.Succeeded);
        Assert.Contains("admin.users.manage", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await c.UserRoles.AnyAsync(ur =>
            ur.UserId == tech.Id && c.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Supervisor")));

        var created = await Users(c).CreateUserAsync(new SaveUserRequest(
            $"tech-{Guid.NewGuid():N}"[..16],
            "Temp Tech",
            null,
            false,
            null,
            "New hire."));
        Assert.True(created.Succeeded, created.Error);
        var allowed = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminUsersManage))
            .AssignRolesAsync(created.Value!.Id, new AssignRolesRequest(["Technologist"], "Bench assignment."));
        Assert.True(allowed.Succeeded, allowed.Error);
    }

    [Fact]
    public async Task CreateUser_WithoutAdminUsersManage_IsRejected()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var request = new SaveUserRequest(
            $"tech-{Guid.NewGuid():N}"[..16],
            "Temp Tech",
            null,
            false,
            ["Technologist"],
            "New hire.");

        var denied = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateUserAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains("admin.users.manage", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await c.Users.AnyAsync(u => u.UserName == request.UserName));

        var allowed = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminUsersManage))
            .CreateUserAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(request.UserName, allowed.Value!.UserName);
    }

    [Fact]
    public async Task CreateRole_WithoutAdminRolesManage_IsRejected()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var name = $"Role-{Guid.NewGuid():N}"[..16];
        var request = new SaveRoleRequest(name, "Temp", 1, [PermissionCodes.IssueCreate], "Need a bench role.");

        var denied = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminUsersManage))
            .CreateRoleAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains("admin.roles.manage", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await c.Roles.AnyAsync(r => r.Name == name));

        var allowed = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminRolesManage))
            .CreateRoleAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Contains(PermissionCodes.IssueCreate, allowed.Value!.Permissions);
    }

    [Fact]
    public async Task SetActive_WithoutAdminUsersManage_IsRejected()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var tech = await c.Users.SingleAsync(u => u.UserName == "tech1");

        var denied = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .SetActiveAsync(tech.Id, false, "Coverage.");
        Assert.False(denied.Succeeded);
        Assert.Contains("admin.users.manage", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True((await c.Users.SingleAsync(u => u.Id == tech.Id)).IsActive);

        var allowed = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminUsersManage))
            .SetActiveAsync(tech.Id, false, "Coverage.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.False(allowed.Value!.IsActive);
    }

    [Fact]
    public async Task SetLocked_WithoutAdminUsersManage_IsRejected()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var tech = await c.Users.SingleAsync(u => u.UserName == "tech1");

        var denied = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .SetLockedAsync(tech.Id, true, "Lockout.");
        Assert.False(denied.Succeeded);
        Assert.Contains("admin.users.manage", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False((await c.Users.SingleAsync(u => u.Id == tech.Id)).IsLocked);

        var allowed = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminUsersManage))
            .SetLockedAsync(tech.Id, true, "Lockout.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsLocked);
    }

    [Fact]
    public async Task RequestPasswordReset_WithoutAdminUsersManage_IsRejected()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var tech = await c.Users.SingleAsync(u => u.UserName == "tech1");

        var denied = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .RequestPasswordResetAsync(tech.Id, "Forgot.");
        Assert.False(denied.Succeeded);
        Assert.Contains("admin.users.manage", denied.Error, StringComparison.OrdinalIgnoreCase);

        var allowed = await Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminUsersManage))
            .RequestPasswordResetAsync(tech.Id, "Forgot.");
        Assert.True(allowed.Succeeded, allowed.Error);
    }
}
