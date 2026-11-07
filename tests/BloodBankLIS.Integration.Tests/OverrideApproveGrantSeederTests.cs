using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-339: Supervisors hold override.approve (product owner decision, 2026-11-02), granted once.
public class OverrideApproveGrantSeederTests : IDisposable
{
    private const string Marker = "Seed.OverrideApproveSupervisorGrant";
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<bool> SupervisorHasItAsync()
    {
        await using var db = _factory.Create();
        return await db.RolePermissions.AnyAsync(rp =>
            db.Roles.Any(r => r.Id == rp.RoleId && r.Name == "Supervisor")
            && db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == PermissionCodes.OverrideApprove));
    }

    private async Task RemoveSupervisorGrantAsync()
    {
        await using var db = _factory.Create();
        var roleId = await db.Roles.Where(r => r.Name == "Supervisor").Select(r => r.Id).SingleAsync();
        var permId = await db.Permissions.Where(p => p.Code == PermissionCodes.OverrideApprove).Select(p => p.Id).SingleAsync();
        await db.RolePermissions.Where(rp => rp.RoleId == roleId && rp.PermissionId == permId).ExecuteDeleteAsync();
    }

    private async Task SeedAsync()
    {
        await using var db = _factory.Create();
        await DatabaseSeeder.SeedAsync(db);
    }

    [Fact]
    public async Task FreshInstall_GrantsSupervisor_AndRecordsTheMarker()
    {
        await SeedAsync();
        Assert.True(await SupervisorHasItAsync());
        await using var db = _factory.Create();
        Assert.True(await db.SystemSettings.AnyAsync(s => s.Key == Marker));
    }

    [Fact]
    public async Task OlderDatabaseWithoutTheMarker_IsGrantedOnce()
    {
        await SeedAsync();
        await RemoveSupervisorGrantAsync();
        await using (var db = _factory.Create())
        {
            await db.SystemSettings.Where(s => s.Key == Marker).ExecuteDeleteAsync();
        }

        await SeedAsync();
        Assert.True(await SupervisorHasItAsync());
    }

    [Fact]
    public async Task AdministratorRemoval_AfterTheGrant_IsNotUndone()
    {
        await SeedAsync();
        await RemoveSupervisorGrantAsync();

        await SeedAsync();
        Assert.False(await SupervisorHasItAsync());
    }
}
