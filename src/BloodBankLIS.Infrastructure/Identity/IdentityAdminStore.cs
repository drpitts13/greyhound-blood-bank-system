using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Identity;

/// <summary>
/// EF Core implementation that stages join-row additions/removals for user↔role and
/// role↔permission membership on the shared <see cref="BloodBankDbContext"/>.
/// </summary>
public sealed class IdentityAdminStore : IIdentityAdminStore
{
    private readonly BloodBankDbContext _context;

    public IdentityAdminStore(BloodBankDbContext context)
    {
        _context = context;
    }

    public async Task StageUserRolesAsync(long userId, IReadOnlyCollection<long> roleIds, CancellationToken ct = default)
    {
        var existing = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(ct);
        var desired = new HashSet<long>(roleIds);

        foreach (var ur in existing.Where(ur => !desired.Contains(ur.RoleId)))
        {
            _context.UserRoles.Remove(ur);
        }

        var current = existing.Select(ur => ur.RoleId).ToHashSet();
        foreach (var roleId in desired.Where(id => !current.Contains(id)))
        {
            _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        }
    }

    public async Task StageRolePermissionsAsync(long roleId, IReadOnlyCollection<long> permissionIds, CancellationToken ct = default)
    {
        var existing = await _context.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        var desired = new HashSet<long>(permissionIds);

        foreach (var rp in existing.Where(rp => !desired.Contains(rp.PermissionId)))
        {
            _context.RolePermissions.Remove(rp);
        }

        var current = existing.Select(rp => rp.PermissionId).ToHashSet();
        foreach (var permissionId in desired.Where(id => !current.Contains(id)))
        {
            _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        }
    }
}
