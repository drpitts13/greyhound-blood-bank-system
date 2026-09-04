using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Test double that returns a fixed max security level for override gates.
/// An empty permission list means unrestricted <see cref="HasPermissionAsync"/> so
/// existing issue tests keep passing; pass an explicit list to deny a code.
/// </summary>
public sealed class FixedPermissionEvaluator : IPermissionEvaluator
{
    private readonly int _securityLevel;
    private readonly IReadOnlySet<string> _permissions;

    public FixedPermissionEvaluator(int securityLevel, params string[] permissions)
    {
        _securityLevel = securityLevel;
        _permissions = new HashSet<string>(permissions, StringComparer.Ordinal);
    }

    public Task<IReadOnlySet<string>> GetPermissionsAsync(string userName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_permissions);

    public Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(_permissions.Count == 0 || _permissions.Contains(permissionCode));

    public Task<int> GetMaxSecurityLevelAsync(string userName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_securityLevel);
}
