namespace BloodBankLIS.Web.Services;

/// <summary>
/// Holds the signed-in operator's identity for the current Blazor circuit. The API is
/// the security boundary; this app forwards the server-issued session token on every
/// request via Authorization Bearer. The token stays in circuit memory, not localStorage.
/// </summary>
public sealed class UserSession
{
    private readonly DevModeState _devMode;

    public UserSession(DevModeState devMode) => _devMode = devMode;

    public string? SessionToken { get; private set; }

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

    public void SignIn(
        string userName,
        string? displayName,
        string? workstation,
        IReadOnlyList<string> permissions,
        int securityLevel = 0,
        string? sessionToken = null)
    {
        SessionToken = string.IsNullOrWhiteSpace(sessionToken) ? SessionToken : sessionToken;
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

    /// <summary>
    /// Updates UI gating from a live <c>/api/me</c> profile. Does not raise
    /// <see cref="Changed"/> when nothing changed, so layout refresh can poll
    /// without a render loop.
    /// </summary>
    public bool TryApplyLiveProfile(string userName, string? displayName, IReadOnlyList<string> permissions, int securityLevel)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? userName : displayName;
        var same =
            string.Equals(UserName, userName, StringComparison.Ordinal)
            && string.Equals(DisplayName, name, StringComparison.Ordinal)
            && SecurityLevel == securityLevel
            && Permissions.Count == permissions.Count
            && Permissions.SequenceEqual(permissions);
        if (same)
        {
            Touch();
            return false;
        }

        UserName = userName;
        DisplayName = name;
        Permissions = permissions;
        SecurityLevel = securityLevel;
        Touch();
        Changed?.Invoke();
        return true;
    }

    public void SignOut()
    {
        SessionToken = null;
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
