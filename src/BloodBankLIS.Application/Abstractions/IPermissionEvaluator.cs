namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Resolves the effective permission set for a user from their active roles. Keeps
/// authorization logic out of the API/UI so the same decision applies to every entry
/// point (see docs/architecture.md 4.2).
/// </summary>
public interface IPermissionEvaluator
{
    /// <summary>
    /// Returns the distinct permission codes granted to <paramref name="userName"/>
    /// through active roles. An unknown or inactive user yields an empty set.
    /// </summary>
    Task<IReadOnlySet<string>> GetPermissionsAsync(string userName, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maximum <see cref="Domain.Entities.Identity.Role.SecurityLevel"/> across the user's
    /// active roles. Unknown/inactive users yield 0.
    /// </summary>
    Task<int> GetMaxSecurityLevelAsync(string userName, CancellationToken cancellationToken = default);
}
