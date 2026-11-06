using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
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
            permissionEvaluator: permissions,
            auditQuery: new AuditQuery(c));
    }

    // TEST-BB-479: an access review needs a note and the users permission; it records every active account with its
    // roles and last sign-in in one audit event, changes no account, and is shown as the last review.
    [Fact]
    public async Task AccessReview_RecordsActiveAccounts_AndIsReadBack()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8];
        c.Users.AddRange(
            new User { UserName = $"rev-active-{key}", DisplayName = "Active", IsActive = true, LastLoginUtc = _factory.Clock.UtcNow.AddDays(-200) },
            new User { UserName = $"rev-gone-{key}", DisplayName = "Gone", IsActive = false });
        await c.SaveChangesAsync();
        var users = Users(c, new FixedPermissionEvaluator(3, PermissionCodes.AdminUsersManage));

        Assert.False((await users.RecordAccessReviewAsync(new RecordAccessReviewRequest(" "))).Succeeded);
        Assert.False((await Users(c, new FixedPermissionEvaluator(3, PermissionCodes.AuditRead))
            .RecordAccessReviewAsync(new RecordAccessReviewRequest("Quarterly"))).Succeeded);

        var activeCount = await c.Users.CountAsync(u => u.IsActive);
        var recorded = await users.RecordAccessReviewAsync(new RecordAccessReviewRequest($"Quarterly review {key}"));
        Assert.True(recorded.Succeeded, recorded.Error);
        Assert.Equal(activeCount, recorded.Value!.ActiveAccounts);

        var audit = await c.AuditEvents.SingleAsync(a => a.EntityType == "AccessReview" && a.Reason == $"Access review: Quarterly review {key}");
        Assert.Equal(AuditEventType.UserRole, audit.EventType);
        Assert.Contains($"rev-active-{key}", audit.NewValueJson);
        Assert.DoesNotContain($"rev-gone-{key}", audit.NewValueJson);
        Assert.True(await c.Users.AnyAsync(u => u.UserName == $"rev-active-{key}" && u.IsActive));

        var last = await users.GetLastAccessReviewAsync();
        Assert.Equal(($"Quarterly review {key}", _factory.CurrentUser.UserName, activeCount), (last!.Note, last.ReviewedBy, last.ActiveAccounts));
    }

    // TEST-BB-385: access changes (roles, permissions, security level, activation, unlock) record why.
    [Fact]
    public async Task AccessChanges_RequireAReason_AndKeepTheBeforeState()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var svc = Users(c);
        var created = await svc.CreateUserAsync(new SaveUserRequest($"rsn-{Guid.NewGuid():N}"[..14], "Reason Tech", null, false, null, null));
        Assert.True(created.Succeeded, created.Error);
        var userId = created.Value!.Id;

        // Roles: a change needs a reason; resending the same roles does not.
        Assert.False((await svc.AssignRolesAsync(userId, new AssignRolesRequest(["Technologist"], null))).Succeeded);
        Assert.True((await svc.AssignRolesAsync(userId, new AssignRolesRequest(["Technologist"], "New bench technologist"))).Succeeded);
        Assert.True((await svc.AssignRolesAsync(userId, new AssignRolesRequest(["Technologist"], null))).Succeeded);

        // Deactivate needs a reason and records the state before.
        var noReason = await svc.SetActiveAsync(userId, false, "  ");
        Assert.False(noReason.Succeeded);
        Assert.Contains("at least 8 characters", noReason.Error);
        Assert.True((await c.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).IsActive);
        Assert.True((await svc.SetActiveAsync(userId, false, "Left the department")).Succeeded);
        var history = await c.ConfigurationChangeHistory.AsNoTracking()
            .Where(h => h.EntityId == userId && h.ChangeReason == "Left the department").SingleAsync();
        Assert.False(string.IsNullOrWhiteSpace(history.OldValueJson));

        // Locking protects and needs no reason; unlocking does.
        Assert.True((await svc.SetLockedAsync(userId, true, null)).Succeeded);
        Assert.False((await svc.SetLockedAsync(userId, false, "ok")).Succeeded);
        Assert.True((await c.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).IsLocked);
        Assert.True((await svc.SetLockedAsync(userId, false, "Identity confirmed by phone")).Succeeded);
    }

    [Fact]
    public async Task RolePermissionOrLevelChange_NeedsAReason_DescriptionDoesNot()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var svc = Users(c);
        var role = await c.Roles.AsNoTracking().SingleAsync(r => r.Name == "ReadOnly");
        var current = (await svc.GetRoleAsync(role.Id))!;

        Assert.True((await svc.UpdateRoleAsync(role.Id, new SaveRoleRequest(
            current.Name, "Read-only access for auditors", current.SecurityLevel, current.Permissions, null))).Succeeded);

        var raised = await svc.UpdateRoleAsync(role.Id, new SaveRoleRequest(
            current.Name, current.Description, current.SecurityLevel + 1, current.Permissions, null));
        Assert.False(raised.Succeeded);
        Assert.Equal(current.SecurityLevel, (await c.Roles.AsNoTracking().SingleAsync(r => r.Id == role.Id)).SecurityLevel);

        var withReason = await svc.UpdateRoleAsync(role.Id, new SaveRoleRequest(
            current.Name, current.Description, current.SecurityLevel + 1, current.Permissions, "Auditors may sign off reviews"));
        Assert.True(withReason.Succeeded, withReason.Error);
        Assert.Equal(current.SecurityLevel + 1, withReason.Value!.SecurityLevel);
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
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(User)
            && a.EntityId == allowed.Value.Id
            && a.EventType == AuditEventType.UserRole));
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
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(Role)
            && a.EntityId == allowed.Value.Id
            && a.EventType == AuditEventType.UserRole));
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
        tech.IsLocked = false;
        await c.SaveChangesAsync();

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

    [Fact]
    public async Task CreateAndUpdate_WriteUserRole()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var svc = Users(c);
        var name = $"tech-{Guid.NewGuid():N}"[..16];

        var created = await svc.CreateUserAsync(new SaveUserRequest(
            name, "Temp Tech", null, false, ["Technologist"], "New hire."));
        Assert.True(created.Succeeded, created.Error);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(User)
            && a.EntityId == created.Value!.Id
            && a.EventType == AuditEventType.UserRole));

        var updated = await svc.UpdateUserAsync(
            created.Value!.Id,
            new SaveUserRequest(name, "Evening Tech", null, false, null, "Display name for the evening bench."));
        Assert.True(updated.Succeeded, updated.Error);

        var events = await c.AuditEvents
            .Where(a => a.EntityType == nameof(User) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, events.Count(a => a.EventType == AuditEventType.UserRole));
    }

    [Fact]
    public async Task LockAndPasswordReset_WriteUserRole()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var tech = await c.Users.SingleAsync(u => u.UserName == "tech1");
        var svc = Users(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminUsersManage));

        var locked = await svc.SetLockedAsync(tech.Id, true, "Lockout.");
        Assert.True(locked.Succeeded, locked.Error);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(User)
            && a.EntityId == tech.Id
            && a.EventType == AuditEventType.UserRole
            && a.Reason == "Lockout."));

        var reset = await svc.RequestPasswordResetAsync(tech.Id, "Forgot.");
        Assert.True(reset.Succeeded, reset.Error);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(User)
            && a.EntityId == tech.Id
            && a.EventType == AuditEventType.UserRole
            && a.Reason == "Forgot."));
    }
}
