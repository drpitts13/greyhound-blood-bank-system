using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin management of users and roles: create/edit/activate/deactivate/lock, role
/// assignment (privilege escalation requires a reason), and role↔permission editing.
/// Guards prevent an admin from removing their own administrative access. Every change is
/// audited and snapshotted. Stays EF-free by stitching join rows via repositories.
/// </summary>
public sealed class UserAdminService : ConfigAdminServiceBase
{
    private const string UserEntity = nameof(User);
    private const string RoleEntity = nameof(Role);

    private readonly IRepository<User> _users;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<Permission> _permissions;
    private readonly IRepository<UserRole> _userRoles;
    private readonly IRepository<RolePermission> _rolePermissions;
    private readonly IIdentityAdminStore _identity;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public UserAdminService(
        IRepository<User> users,
        IRepository<Role> roles,
        IRepository<Permission> permissions,
        IRepository<UserRole> userRoles,
        IRepository<RolePermission> rolePermissions,
        IIdentityAdminStore identity,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _users = users;
        _roles = roles;
        _permissions = permissions;
        _userRoles = userRoles;
        _rolePermissions = rolePermissions;
        _identity = identity;
        _permissionEvaluator = permissionEvaluator;
    }

    // ---- Reads ----

    public async Task<IReadOnlyList<AdminUserDto>> ListUsersAsync(bool includeInactive, CancellationToken ct = default)
    {
        var users = includeInactive ? await _users.ListAsync(ct) : await _users.ListAsync(u => u.IsActive, ct);
        var roleNames = (await _roles.ListAsync(ct)).ToDictionary(r => r.Id, r => r.Name);
        var links = await _userRoles.ListAsync(ct);
        var byUser = links.GroupBy(l => l.UserId).ToDictionary(g => g.Key, g => g.Select(l => l.RoleId).ToList());

        return users.OrderBy(u => u.UserName).Select(u => MapUser(u, byUser, roleNames)).ToList();
    }

    public async Task<AdminUserDto?> GetUserAsync(long id, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
        {
            return null;
        }

        var roleNames = (await _roles.ListAsync(ct)).ToDictionary(r => r.Id, r => r.Name);
        var links = await _userRoles.ListAsync(l => l.UserId == id, ct);
        var byUser = new Dictionary<long, List<long>> { [id] = links.Select(l => l.RoleId).ToList() };
        return MapUser(user, byUser, roleNames);
    }

    public async Task<IReadOnlyList<AdminRoleDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var roles = await _roles.ListAsync(ct);
        var permCodes = (await _permissions.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Code);
        var links = await _rolePermissions.ListAsync(ct);
        var byRole = links.GroupBy(l => l.RoleId).ToDictionary(g => g.Key, g => g.Select(l => l.PermissionId).ToList());

        return roles.OrderBy(r => r.Name).Select(r => MapRole(r, byRole, permCodes)).ToList();
    }

    public async Task<AdminRoleDto?> GetRoleAsync(long id, CancellationToken ct = default)
    {
        var role = await _roles.GetByIdAsync(id, ct);
        if (role is null)
        {
            return null;
        }

        var permCodes = (await _permissions.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Code);
        var links = await _rolePermissions.ListAsync(l => l.RoleId == id, ct);
        var byRole = new Dictionary<long, List<long>> { [id] = links.Select(l => l.PermissionId).ToList() };
        return MapRole(role, byRole, permCodes);
    }

    public Task<IReadOnlyList<string>> ListPermissionCodesAsync(CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<string>)PermissionCodes.All.OrderBy(c => c).ToList());

    // ---- User mutations ----

    public async Task<OperationResult<AdminUserDto>> CreateUserAsync(SaveUserRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedAsync<AdminUserDto>(
            PermissionCodes.AdminUsersManage, AdminAuthorizationRule.EvaluateCreateUser, ct);
        if (denied is not null)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(req.UserName))
        {
            return OperationResult<AdminUserDto>.Fail("Username is required.");
        }

        var userName = req.UserName.Trim();
        if (await _users.AnyAsync(u => u.UserName == userName, ct))
        {
            return OperationResult<AdminUserDto>.Fail($"A user named '{userName}' already exists.");
        }

        var (roleIds, unknown) = await ResolveRolesAsync(req.Roles, ct);
        if (unknown is not null)
        {
            return OperationResult<AdminUserDto>.Fail(unknown);
        }

        if (await GrantsAdminAsync(roleIds, ct) && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return OperationResult<AdminUserDto>.Fail("A change reason is required when granting administrative roles.");
        }

        var user = new User
        {
            UserName = userName,
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? userName : req.DisplayName.Trim(),
            Email = req.Email?.Trim(),
            IsServiceAccount = req.IsServiceAccount,
            IsActive = true
        };
        await _users.AddAsync(user, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        await _identity.StageUserRolesAsync(user.Id, roleIds, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = await GetUserAsync(user.Id, ct);
        RecordChange(UserEntity, user.Id, 1, ConfigChangeAction.Create, AuditEventType.Create, null, dto, req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<AdminUserDto>.Ok(dto!);
    }

    public async Task<OperationResult<AdminUserDto>> UpdateUserAsync(long id, SaveUserRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedAsync<AdminUserDto>(
            PermissionCodes.AdminUsersManage, AdminAuthorizationRule.EvaluateUpdateUser, ct);
        if (denied is not null)
        {
            return denied;
        }

        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
        {
            return OperationResult<AdminUserDto>.Fail("User not found.");
        }

        var before = await GetUserAsync(id, ct);
        user.DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? user.UserName : req.DisplayName.Trim();
        user.Email = req.Email?.Trim();
        user.IsServiceAccount = req.IsServiceAccount;
        _users.Update(user);

        if (req.Roles is not null)
        {
            var assignResult = await AssignRolesInternalAsync(user, req.Roles, req.ChangeReason, ct);
            if (!assignResult.Succeeded)
            {
                return OperationResult<AdminUserDto>.Fail(assignResult.Error!);
            }
        }

        var after = await GetUserAsync(id, ct);
        RecordChange(UserEntity, user.Id, 1, ConfigChangeAction.Update, AuditEventType.Update, before, after, req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<AdminUserDto>.Ok(after!);
    }

    public async Task<OperationResult<AdminUserDto>> AssignRolesAsync(long id, AssignRolesRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedAsync<AdminUserDto>(
            PermissionCodes.AdminUsersManage, AdminAuthorizationRule.EvaluateAssignRoles, ct);
        if (denied is not null)
        {
            return denied;
        }

        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
        {
            return OperationResult<AdminUserDto>.Fail("User not found.");
        }

        var before = await GetUserAsync(id, ct);
        var assignResult = await AssignRolesInternalAsync(user, req.Roles, req.ChangeReason, ct);
        if (!assignResult.Succeeded)
        {
            return OperationResult<AdminUserDto>.Fail(assignResult.Error!);
        }

        var after = await GetUserAsync(id, ct);
        RecordChange(UserEntity, user.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, before, after, req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<AdminUserDto>.Ok(after!);
    }

    public async Task<OperationResult<AdminUserDto>> SetActiveAsync(long id, bool active, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync<AdminUserDto>(
            PermissionCodes.AdminUsersManage, AdminAuthorizationRule.EvaluateSetActive, ct);
        if (denied is not null)
        {
            return denied;
        }

        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
        {
            return OperationResult<AdminUserDto>.Fail("User not found.");
        }

        if (!active && string.Equals(user.UserName, CurrentUser.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<AdminUserDto>.Fail("You cannot deactivate your own account.");
        }

        user.IsActive = active;
        _users.Update(user);

        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        var dto = await GetUserAsync(id, ct);
        RecordChange(UserEntity, user.Id, 1, action, ToAuditType(action), null, dto, reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<AdminUserDto>.Ok(dto!);
    }

    public async Task<OperationResult<AdminUserDto>> SetLockedAsync(long id, bool locked, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync<AdminUserDto>(
            PermissionCodes.AdminUsersManage, AdminAuthorizationRule.EvaluateSetLocked, ct);
        if (denied is not null)
        {
            return denied;
        }

        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
        {
            return OperationResult<AdminUserDto>.Fail("User not found.");
        }

        if (locked && string.Equals(user.UserName, CurrentUser.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<AdminUserDto>.Fail("You cannot lock your own account.");
        }

        user.IsLocked = locked;
        _users.Update(user);

        var dto = await GetUserAsync(id, ct);
        RecordChange(UserEntity, user.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, null, dto, reason ?? (locked ? "Locked" : "Unlocked"));
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<AdminUserDto>.Ok(dto!);
    }

    /// <summary>Password reset placeholder: records the request for audit; no credential store yet.</summary>
    public async Task<OperationResult<AdminUserDto>> RequestPasswordResetAsync(long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync<AdminUserDto>(
            PermissionCodes.AdminUsersManage, AdminAuthorizationRule.EvaluatePasswordReset, ct);
        if (denied is not null)
        {
            return denied;
        }

        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
        {
            return OperationResult<AdminUserDto>.Fail("User not found.");
        }

        Audit.Record(AuditEventType.Configure, UserEntity, user.Id, reason: reason ?? "Password reset requested (placeholder).");
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = await GetUserAsync(id, ct);
        return OperationResult<AdminUserDto>.Ok(dto!);
    }

    // ---- Role mutations ----

    public async Task<OperationResult<AdminRoleDto>> CreateRoleAsync(SaveRoleRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedAsync<AdminRoleDto>(
            PermissionCodes.AdminRolesManage, AdminAuthorizationRule.EvaluateCreateRole, ct);
        if (denied is not null)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return OperationResult<AdminRoleDto>.Fail("Role name is required.");
        }

        var name = req.Name.Trim();
        if (await _roles.AnyAsync(r => r.Name == name, ct))
        {
            return OperationResult<AdminRoleDto>.Fail($"A role named '{name}' already exists.");
        }

        var (permissionIds, unknown) = await ResolvePermissionsAsync(req.Permissions, ct);
        if (unknown is not null)
        {
            return OperationResult<AdminRoleDto>.Fail(unknown);
        }

        var role = new Role { Name = name, Description = req.Description?.Trim(), SecurityLevel = Math.Max(0, req.SecurityLevel) };
        await _roles.AddAsync(role, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        await _identity.StageRolePermissionsAsync(role.Id, permissionIds, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = await GetRoleAsync(role.Id, ct);
        RecordChange(RoleEntity, role.Id, 1, ConfigChangeAction.Create, AuditEventType.Create, null, dto, req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<AdminRoleDto>.Ok(dto!);
    }

    public async Task<OperationResult<AdminRoleDto>> UpdateRoleAsync(long id, SaveRoleRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedAsync<AdminRoleDto>(
            PermissionCodes.AdminRolesManage, AdminAuthorizationRule.EvaluateUpdateRole, ct);
        if (denied is not null)
        {
            return denied;
        }

        var role = await _roles.GetByIdAsync(id, ct);
        if (role is null)
        {
            return OperationResult<AdminRoleDto>.Fail("Role not found.");
        }

        var (permissionIds, unknown) = await ResolvePermissionsAsync(req.Permissions, ct);
        if (unknown is not null)
        {
            return OperationResult<AdminRoleDto>.Fail(unknown);
        }

        var before = await GetRoleAsync(id, ct);
        role.Description = req.Description?.Trim();
        role.SecurityLevel = Math.Max(0, req.SecurityLevel);
        _roles.Update(role);
        await _identity.StageRolePermissionsAsync(role.Id, permissionIds, ct);

        var after = await GetRoleAsync(id, ct);
        RecordChange(RoleEntity, role.Id, 1, ConfigChangeAction.Update, AuditEventType.Update, before, after, req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<AdminRoleDto>.Ok(after!);
    }

    // ---- Helpers ----

    private async Task<OperationResult<bool>> AssignRolesInternalAsync(User user, IReadOnlyList<string> roleNames, string? reason, CancellationToken ct)
    {
        var (roleIds, unknown) = await ResolveRolesAsync(roleNames, ct);
        if (unknown is not null)
        {
            return OperationResult<bool>.Fail(unknown);
        }

        var editingSelf = string.Equals(user.UserName, CurrentUser.UserName, StringComparison.OrdinalIgnoreCase);
        var currentlyAdmin = await UserGrantsAdminAsync(user.Id, ct);
        var willBeAdmin = await GrantsAdminAsync(roleIds, ct);

        if (willBeAdmin && !currentlyAdmin && string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<bool>.Fail("A change reason is required when granting administrative roles.");
        }

        if (editingSelf && currentlyAdmin && !willBeAdmin)
        {
            return OperationResult<bool>.Fail("You cannot remove your own administrative access.");
        }

        await _identity.StageUserRolesAsync(user.Id, roleIds, ct);
        return OperationResult<bool>.Ok(true);
    }

    private async Task<(IReadOnlyCollection<long> Ids, string? Error)> ResolveRolesAsync(IReadOnlyList<string>? roleNames, CancellationToken ct)
    {
        if (roleNames is null || roleNames.Count == 0)
        {
            return (Array.Empty<long>(), null);
        }

        var wanted = roleNames.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var all = await _roles.ListAsync(ct);
        var matched = all.Where(r => wanted.Contains(r.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        var missing = wanted.Where(w => !matched.Any(r => string.Equals(r.Name, w, StringComparison.OrdinalIgnoreCase))).ToList();
        if (missing.Count > 0)
        {
            return (Array.Empty<long>(), $"Unknown role(s): {string.Join(", ", missing)}.");
        }

        return (matched.Select(r => r.Id).ToList(), null);
    }

    private async Task<(IReadOnlyCollection<long> Ids, string? Error)> ResolvePermissionsAsync(IReadOnlyList<string>? codes, CancellationToken ct)
    {
        if (codes is null || codes.Count == 0)
        {
            return (Array.Empty<long>(), null);
        }

        var wanted = codes.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var all = await _permissions.ListAsync(ct);
        var matched = all.Where(p => wanted.Contains(p.Code)).ToList();
        var missing = wanted.Where(w => !matched.Any(p => p.Code == w)).ToList();
        if (missing.Count > 0)
        {
            return (Array.Empty<long>(), $"Unknown permission(s): {string.Join(", ", missing)}.");
        }

        return (matched.Select(p => p.Id).ToList(), null);
    }

    private async Task<bool> GrantsAdminAsync(IReadOnlyCollection<long> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0)
        {
            return false;
        }

        var adminPermIds = await AdminPermissionIdsAsync(ct);
        if (adminPermIds.Count == 0)
        {
            return false;
        }

        var links = await _rolePermissions.ListAsync(rp => roleIds.Contains(rp.RoleId), ct);
        return links.Any(rp => adminPermIds.Contains(rp.PermissionId));
    }

    private async Task<bool> UserGrantsAdminAsync(long userId, CancellationToken ct)
    {
        var links = await _userRoles.ListAsync(ur => ur.UserId == userId, ct);
        var roleIds = links.Select(l => l.RoleId).ToList();
        return await GrantsAdminAsync(roleIds, ct);
    }

    private async Task<HashSet<long>> AdminPermissionIdsAsync(CancellationToken ct)
    {
        var perms = await _permissions.ListAsync(p => PermissionCodes.AdminAll.Contains(p.Code), ct);
        return perms.Select(p => p.Id).ToHashSet();
    }

    private static AdminUserDto MapUser(User u, IReadOnlyDictionary<long, List<long>> rolesByUser, IReadOnlyDictionary<long, string> roleNames)
    {
        var names = rolesByUser.TryGetValue(u.Id, out var ids)
            ? ids.Where(roleNames.ContainsKey).Select(id => roleNames[id]).OrderBy(n => n).ToList()
            : new List<string>();
        return new AdminUserDto(u.Id, u.UserName, u.DisplayName, u.Email, u.IsActive, u.IsLocked, u.IsServiceAccount, u.LastLoginUtc, names);
    }

    private async Task<OperationResult<T>?> RejectUnauthorizedAsync<T>(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(
            CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<T>.Fail(auth.Message)
            : null;
    }

    private static AdminRoleDto MapRole(Role r, IReadOnlyDictionary<long, List<long>> permsByRole, IReadOnlyDictionary<long, string> permCodes)
    {
        var codes = permsByRole.TryGetValue(r.Id, out var ids)
            ? ids.Where(permCodes.ContainsKey).Select(id => permCodes[id]).OrderBy(c => c).ToList()
            : new List<string>();
        return new AdminRoleDto(r.Id, r.Name, r.Description, r.SecurityLevel, codes);
    }
}
