using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Identity;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Identity;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AuthSessionServiceTests
{
    private static AuthSessionService Sessions(
        BloodBankDbContext c,
        SqliteContextFactory factory,
        AuthSessionOptions? options = null)
    {
        factory.Clock.UtcNow = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new AuthSessionService(
            new EfRepository<User>(c),
            new EfRepository<AuthSession>(c),
            c,
            factory.Clock,
            new AuditWriter(c, factory.Clock, factory.CurrentUser, env),
            new FixedPermissionEvaluator(2, PermissionCodes.IssueCreate, PermissionCodes.IssueEmergencyRelease),
            options ?? new AuthSessionOptions());
    }

    [Fact]
    public async Task Login_WithPassword_IssuesResolvableSession()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var service = Sessions(c, factory);

        var login = await service.LoginAsync("supervisor", "demo", "BB-1");
        Assert.True(login.Succeeded, login.Error);
        Assert.False(string.IsNullOrWhiteSpace(login.Value!.SessionToken));
        Assert.Equal("supervisor", login.Value.UserName);
        Assert.Contains(PermissionCodes.IssueEmergencyRelease, login.Value.Permissions);

        var principal = await service.ResolveAsync(login.Value.SessionToken);
        Assert.NotNull(principal);
        Assert.Equal("supervisor", principal!.UserName);
        Assert.Equal("BB-1", principal.Workstation);

        Assert.True(await c.AuditEvents.AnyAsync(e =>
            e.EventType == AuditEventType.Login && e.UserName == "supervisor"));
    }

    [Fact]
    public async Task Login_WrongPassword_FailsAndDoesNotIssueSession()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        var login = await Sessions(c, factory).LoginAsync("supervisor", "wrong", "BB-1");
        Assert.False(login.Succeeded);
        Assert.Empty(await c.AuthSessions.ToListAsync());
    }

    [Fact]
    public async Task Login_MissingPasswordHash_FailClosed()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        c.Users.Add(new User
        {
            UserName = "no-secret",
            DisplayName = "No Secret",
            IsActive = true,
            PasswordHash = null
        });
        await c.SaveChangesAsync();

        var login = await Sessions(c, factory).LoginAsync("no-secret", "demo", "BB-1");
        Assert.False(login.Succeeded);
        Assert.Empty(await c.AuthSessions.ToListAsync());
    }

    [Fact]
    public async Task Login_LockedUser_Fails()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var user = await c.Users.SingleAsync(u => u.UserName == "tech1");
        user.IsLocked = true;
        await c.SaveChangesAsync();

        var login = await Sessions(c, factory).LoginAsync("tech1", "demo", "BB-1");
        Assert.False(login.Succeeded);
        Assert.Empty(await c.AuthSessions.ToListAsync());
    }

    [Fact]
    public async Task FiveFailedLogins_LocksAndRevokesExistingSession()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var service = Sessions(c, factory);
        var first = await service.LoginAsync("tech1", "demo", "BB-1");
        Assert.True(first.Succeeded, first.Error);

        for (var i = 0; i < AuthSessionService.FailedSignInLockoutThreshold; i++)
        {
            var failed = await service.LoginAsync("tech1", "wrong", "BB-1");
            Assert.False(failed.Succeeded);
        }

        var user = await c.Users.SingleAsync(u => u.UserName == "tech1");
        Assert.True(user.IsLocked);
        Assert.Null(await service.ResolveAsync(first.Value!.SessionToken));
        Assert.All(await c.AuthSessions.ToListAsync(), s => Assert.NotNull(s.RevokedUtc));
    }

    [Fact]
    public async Task Logout_RevokesToken()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var service = Sessions(c, factory);
        var login = await service.LoginAsync("tech1", "demo", "BB-1");
        Assert.True(login.Succeeded, login.Error);

        var logout = await service.LogoutAsync(login.Value!.SessionToken);
        Assert.True(logout.Succeeded);
        Assert.Null(await service.ResolveAsync(login.Value.SessionToken));
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EventType == AuditEventType.Logout));
    }

    [Fact]
    public async Task ExpiredIdleSession_DoesNotResolve()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var options = new AuthSessionOptions { IdleTimeoutMinutes = 30, AbsoluteTimeoutHours = 12 };
        var service = Sessions(c, factory, options);
        var login = await service.LoginAsync("tech1", "demo", "BB-1");
        Assert.True(login.Succeeded, login.Error);

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddMinutes(31);
        Assert.Null(await service.ResolveAsync(login.Value!.SessionToken));
    }

    [Fact]
    public async Task AbsoluteExpiry_DoesNotResolve()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var options = new AuthSessionOptions { IdleTimeoutMinutes = 30, AbsoluteTimeoutHours = 1 };
        var service = Sessions(c, factory, options);
        var login = await service.LoginAsync("tech1", "demo", "BB-1");
        Assert.True(login.Succeeded, login.Error);

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddHours(2);
        Assert.Null(await service.ResolveAsync(login.Value!.SessionToken));
    }

    [Fact]
    public async Task Deactivate_RevokesOutstandingSessions()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var service = Sessions(c, factory);
        var login = await service.LoginAsync("tech1", "demo", "BB-1");
        Assert.True(login.Succeeded, login.Error);

        var user = await c.Users.SingleAsync(u => u.UserName == "tech1");
        await service.RevokeUserSessionsAsync(user.Id, "Account deactivated");

        Assert.Null(await service.ResolveAsync(login.Value!.SessionToken));
    }

    [Fact]
    public async Task UnknownToken_DoesNotResolve()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        Assert.Null(await Sessions(c, factory).ResolveAsync("not-a-real-token"));
    }

    [Fact]
    public async Task UserAdminLock_RevokesOutstandingSessions()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var sessions = Sessions(c, factory);
        var login = await sessions.LoginAsync("tech1", "demo", "BB-1");
        Assert.True(login.Succeeded, login.Error);

        var admin = Admin(c, factory, sessions, PermissionCodes.AdminUsersManage);

        var user = await c.Users.SingleAsync(u => u.UserName == "tech1");
        var locked = await admin.SetLockedAsync(user.Id, true, "Lockout.");
        Assert.True(locked.Succeeded, locked.Error);
        Assert.Null(await sessions.ResolveAsync(login.Value!.SessionToken));
    }

    [Fact]
    public async Task AssignRoles_RevokesOutstandingSessions()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var sessions = Sessions(c, factory);
        var login = await sessions.LoginAsync("tech1", "demo", "BB-1");
        Assert.True(login.Succeeded, login.Error);

        var admin = Admin(c, factory, sessions, PermissionCodes.AdminUsersManage);
        var user = await c.Users.SingleAsync(u => u.UserName == "tech1");
        var assigned = await admin.AssignRolesAsync(user.Id, new AssignRolesRequest(["ReadOnly"], "Privilege reduction."));
        Assert.True(assigned.Succeeded, assigned.Error);
        Assert.Null(await sessions.ResolveAsync(login.Value!.SessionToken));
    }

    [Fact]
    public async Task UpdateRolePermissions_RevokesSessionsForRoleMembers()
    {
        using var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var sessions = Sessions(c, factory);
        var login = await sessions.LoginAsync("tech1", "demo", "BB-1");
        Assert.True(login.Succeeded, login.Error);

        var admin = Admin(c, factory, sessions, PermissionCodes.AdminRolesManage);
        var role = await c.Roles.SingleAsync(r => r.Name == "Technologist");
        var current = await admin.GetRoleAsync(role.Id);
        Assert.NotNull(current);

        var updated = await admin.UpdateRoleAsync(role.Id, new SaveRoleRequest(
            current!.Name,
            current.Description,
            current.SecurityLevel,
            current.Permissions.Where(p => p != PermissionCodes.IssueCreate).ToArray(),
            "Remove issue.create."));
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Null(await sessions.ResolveAsync(login.Value!.SessionToken));
    }

    private static UserAdminService Admin(
        BloodBankDbContext c,
        SqliteContextFactory factory,
        IAuthSessionService sessions,
        params string[] permissions)
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
            factory.Clock,
            factory.CurrentUser,
            new AuditWriter(c, factory.Clock, factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, factory.Clock, factory.CurrentUser, env),
            new FixedPermissionEvaluator(1, permissions),
            sessions);
    }
}
