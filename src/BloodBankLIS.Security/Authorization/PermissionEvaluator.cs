using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities.Identity;

namespace BloodBankLIS.Security.Authorization;

/// <summary>
/// Resolves a user's effective permissions from active roles. Uses only the repository
/// abstraction (no EF dependency here) so the Security layer stays persistence-agnostic.
/// Inactive/locked users and inactive roles contribute nothing — default-deny.
/// </summary>
public sealed class PermissionEvaluator : IPermissionEvaluator
{
    private readonly IRepository<User> _users;
    private readonly IRepository<UserRole> _userRoles;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<RolePermission> _rolePermissions;
    private readonly IRepository<Permission> _permissions;

    public PermissionEvaluator(
        IRepository<User> users,
        IRepository<UserRole> userRoles,
        IRepository<Role> roles,
        IRepository<RolePermission> rolePermissions,
        IRepository<Permission> permissions)
    {
        _users = users;
        _userRoles = userRoles;
        _roles = roles;
        _rolePermissions = rolePermissions;
        _permissions = permissions;
    }

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(string userName, CancellationToken cancellationToken = default)
    {
        var empty = (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(userName))
        {
            return empty;
        }

        var user = await _users.FirstOrDefaultAsync(
            u => u.UserName == userName && u.IsActive && !u.IsLocked, cancellationToken);
        if (user is null)
        {
            return empty;
        }

        var roleIds = (await _userRoles.ListAsync(ur => ur.UserId == user.Id, cancellationToken))
            .Select(ur => ur.RoleId)
            .Distinct()
            .ToList();
        if (roleIds.Count == 0)
        {
            return empty;
        }

        var activeRoleIds = (await _roles.ListAsync(r => roleIds.Contains(r.Id) && r.IsActive, cancellationToken))
            .Select(r => r.Id)
            .ToList();
        if (activeRoleIds.Count == 0)
        {
            return empty;
        }

        var permissionIds = (await _rolePermissions.ListAsync(rp => activeRoleIds.Contains(rp.RoleId), cancellationToken))
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToList();
        if (permissionIds.Count == 0)
        {
            return empty;
        }

        var codes = (await _permissions.ListAsync(p => permissionIds.Contains(p.Id), cancellationToken))
            .Select(p => p.Code);

        return new HashSet<string>(codes, StringComparer.Ordinal);
    }

    public async Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(permissionCode))
        {
            return false;
        }

        var permissions = await GetPermissionsAsync(userName, cancellationToken);
        return permissions.Contains(permissionCode);
    }

    public async Task<int> GetMaxSecurityLevelAsync(string userName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return 0;
        }

        var user = await _users.FirstOrDefaultAsync(
            u => u.UserName == userName && u.IsActive && !u.IsLocked, cancellationToken);
        if (user is null)
        {
            return 0;
        }

        var roleIds = (await _userRoles.ListAsync(ur => ur.UserId == user.Id, cancellationToken))
            .Select(ur => ur.RoleId)
            .Distinct()
            .ToList();
        if (roleIds.Count == 0)
        {
            return 0;
        }

        var activeRoles = await _roles.ListAsync(r => roleIds.Contains(r.Id) && r.IsActive, cancellationToken);
        return activeRoles.Count == 0 ? 0 : activeRoles.Max(r => r.SecurityLevel);
    }
}
