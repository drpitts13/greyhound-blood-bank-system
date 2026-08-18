namespace BloodBankLIS.Web.Services;

/// <summary>
/// Holds the signed-in operator's identity for the current Blazor circuit. The API is
/// the security boundary; this app forwards the chosen identity on every request via
/// the <c>X-User</c>/<c>X-Workstation</c> headers. In a real deployment the gateway
/// would assert this identity from a verified credential (OIDC/Windows/smartcard).
/// </summary>
public sealed class UserSession
{
    private readonly DevModeState _devMode;

    public UserSession(DevModeState devMode) => _devMode = devMode;

    public string? UserName { get; private set; }

    public string? DisplayName { get; private set; }

    public string Workstation { get; private set; } = Environment.MachineName;

    public IReadOnlyList<string> Permissions { get; private set; } = Array.Empty<string>();

    /// <summary>Max role security level for UI gating of exception overrides. API re-checks.</summary>
    public int SecurityLevel { get; private set; }

    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

    public static TimeSpan IdleTimeout { get; } = TimeSpan.FromMinutes(30);

    public bool IsSignedIn => !string.IsNullOrWhiteSpace(UserName);

    public bool IsIdle => IsSignedIn && DateTime.UtcNow - LastActivityUtc > IdleTimeout;

    public event Action? Changed;

    public void Touch() => LastActivityUtc = DateTime.UtcNow;

    public void SignIn(string userName, string? displayName, string? workstation, IReadOnlyList<string> permissions, int securityLevel = 0)
    {
        UserName = userName;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName;
        if (!string.IsNullOrWhiteSpace(workstation))
        {
            Workstation = workstation;
        }

        Permissions = permissions;
        SecurityLevel = securityLevel;
        Touch();
        Changed?.Invoke();
    }

    public void SignOut()
    {
        UserName = null;
        DisplayName = null;
        Permissions = Array.Empty<string>();
        SecurityLevel = 0;
        Changed?.Invoke();
    }

    /// <summary>UI gating only; the API re-checks every permission. Dev mode grants all UI actions.</summary>
    public bool Has(string permissionCode) =>
        _devMode.Enabled || Permissions.Contains(permissionCode);

    public bool HasAny(params string[] permissionCodes) =>
        _devMode.Enabled || permissionCodes.Any(p => Permissions.Contains(p));
}
