namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Reconciles identity many-to-many membership (user↔role, role↔permission). These join
/// rows are configuration, not clinical data, so they may be added/removed. Changes are
/// staged on the shared unit of work and committed by the caller's SaveChanges, keeping
/// the audit/history snapshot in the same transaction.
/// </summary>
public interface IIdentityAdminStore
{
    Task StageUserRolesAsync(long userId, IReadOnlyCollection<long> roleIds, CancellationToken ct = default);

    Task StageRolePermissionsAsync(long roleId, IReadOnlyCollection<long> permissionIds, CancellationToken ct = default);
}
