using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Security.Authorization;
using BloodBankLIS.Security.Signatures;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class Phase8SecurityTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static PermissionEvaluator Evaluator(BloodBankDbContext c) =>
        new(new EfRepository<User>(c), new EfRepository<UserRole>(c), new EfRepository<Role>(c),
            new EfRepository<RolePermission>(c), new EfRepository<Permission>(c));

    private SignatureService Signatures(BloodBankDbContext c) =>
        new(new EfRepository<User>(c), new EfRepository<ElectronicSignature>(c), c,
            _factory.Clock, _factory.CurrentUser);

    [Fact]
    public async Task Seed_CreatesPermissionCatalogRolesAndUsers()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        Assert.Equal(PermissionCodes.All.Count, await c.Permissions.CountAsync());
        Assert.True(await c.Roles.AnyAsync(r => r.Name == "Administrator"));
        Assert.True(await c.Users.AnyAsync(u => u.UserName == "tech1"));
    }

    [Fact]
    public async Task Administrator_HasEveryPermission()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        var perms = await Evaluator(c).GetPermissionsAsync("admin");
        Assert.Equal(PermissionCodes.All.Count, perms.Count);
    }

    [Fact]
    public async Task Technologist_HasRoutineButNotDangerousPermissions()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        var evaluator = Evaluator(c);
        Assert.True(await evaluator.HasPermissionAsync("tech1", PermissionCodes.ResultEnter));
        Assert.True(await evaluator.HasPermissionAsync("tech1", PermissionCodes.IssueCreate));
        Assert.True(await evaluator.HasPermissionAsync("tech1", PermissionCodes.PatientWrite));
        Assert.False(await evaluator.HasPermissionAsync("tech1", PermissionCodes.PatientMerge));
        Assert.False(await evaluator.HasPermissionAsync("tech1", PermissionCodes.IssueOverride));
        Assert.False(await evaluator.HasPermissionAsync("tech1", PermissionCodes.InventoryDiscard));
        Assert.False(await evaluator.HasPermissionAsync("tech1", PermissionCodes.ResultCorrect));
    }

    [Fact]
    public async Task Supervisor_HasOverrideAndDangerousPermissions()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        var evaluator = Evaluator(c);
        Assert.True(await evaluator.HasPermissionAsync("supervisor", PermissionCodes.IssueOverride));
        Assert.True(await evaluator.HasPermissionAsync("supervisor", PermissionCodes.InventoryDiscard));
        Assert.True(await evaluator.HasPermissionAsync("supervisor", PermissionCodes.BillingCancel));
        Assert.True(await evaluator.HasPermissionAsync("supervisor", PermissionCodes.PatientMerge));
    }

    [Fact]
    public async Task ReadOnly_HasOnlyAuditRead()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        var perms = await Evaluator(c).GetPermissionsAsync("viewer");
        Assert.Single(perms);
        Assert.Contains(PermissionCodes.AuditRead, perms);
    }

    [Fact]
    public async Task UnknownUser_HasNoPermissions()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        var perms = await Evaluator(c).GetPermissionsAsync("ghost");
        Assert.Empty(perms);
    }

    [Fact]
    public async Task InactiveUser_HasNoPermissions()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        var role = await c.Roles.FirstAsync(r => r.Name == "Administrator");
        var inactive = new User { UserName = "retired", DisplayName = "Retired Admin", IsActive = false };
        inactive.UserRoles.Add(new UserRole { RoleId = role.Id });
        c.Users.Add(inactive);
        await c.SaveChangesAsync();

        var perms = await Evaluator(c).GetPermissionsAsync("retired");
        Assert.Empty(perms);
    }

    [Fact]
    public async Task InactiveRole_GrantsNoPermissions()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        // Build a user whose only role is inactive.
        var role = new Role { Name = "Suspended", IsActive = false };
        role.RolePermissions.Add(new RolePermission { Permission = await c.Permissions.FirstAsync(p => p.Code == PermissionCodes.IssueCreate) });
        c.Roles.Add(role);
        await c.SaveChangesAsync();

        var user = new User { UserName = "suspended-user", DisplayName = "Suspended" };
        user.UserRoles.Add(new UserRole { RoleId = role.Id });
        c.Users.Add(user);
        await c.SaveChangesAsync();

        var perms = await Evaluator(c).GetPermissionsAsync("suspended-user");
        Assert.Empty(perms);
    }

    [Fact]
    public async Task Signature_RecordsAttestationForCurrentUser()
    {
        long signatureId;
        await using (var setup = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(setup);
            // The factory's current user is "tech-test"; seed a matching account.
            setup.Users.Add(new User { UserName = _factory.CurrentUser.UserName, DisplayName = "Bench Tech" });
            await setup.SaveChangesAsync();

            var result = await Signatures(setup).RecordAsync("IssueOverride", "I authorize this emergency release", "Issue", 42);
            Assert.True(result.Succeeded);
            signatureId = result.Value;
        }

        await using var verify = _factory.Create();
        var signature = await verify.ElectronicSignatures.SingleAsync(s => s.Id == signatureId);
        Assert.Equal("IssueOverride", signature.Action);
        Assert.Equal("Issue", signature.ContextType);
        Assert.Equal(42, signature.ContextId);
        Assert.Equal("I authorize this emergency release", signature.MeaningOfSignature);
        Assert.Equal(_factory.Clock.UtcNow, signature.SignedUtc);
    }

    [Fact]
    public async Task Signature_RequiresMeaning()
    {
        await using var c = _factory.Create();
        c.Users.Add(new User { UserName = _factory.CurrentUser.UserName, DisplayName = "Bench Tech" });
        await c.SaveChangesAsync();

        var result = await Signatures(c).RecordAsync("IssueOverride", meaningOfSignature: "   ");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Signature_FailsForUnknownCurrentUser()
    {
        await using var c = _factory.Create();
        // No user row for "tech-test".
        var result = await Signatures(c).RecordAsync("IssueOverride", "Attest");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Signature_ValidationChecksOwnerAndAction()
    {
        long signatureId;
        await using (var setup = _factory.Create())
        {
            setup.Users.Add(new User { UserName = _factory.CurrentUser.UserName, DisplayName = "Bench Tech" });
            await setup.SaveChangesAsync();
            signatureId = (await Signatures(setup).RecordAsync("IssueOverride", "Attest")).Value;
        }

        await using var c = _factory.Create();
        var signatures = Signatures(c);
        Assert.True(await signatures.IsValidForCurrentUserAsync(signatureId, "IssueOverride"));
        Assert.True(await signatures.IsValidForCurrentUserAsync(signatureId));
        Assert.False(await signatures.IsValidForCurrentUserAsync(signatureId, "SomethingElse"));
        Assert.False(await signatures.IsValidForCurrentUserAsync(999999, "IssueOverride"));
    }
}
