namespace BloodBankLIS.Web.Services;

/// <summary>
/// Web-side view of the no-login dev mode. When enabled (Development only), the shell
/// auto-signs-in as the dev admin and shows a persistent banner. Enforced by a startup
/// guard that fails fast if enabled outside Development.
/// </summary>
public sealed class DevModeState
{
    public bool Enabled { get; init; }

    public string UserName { get; init; } = "DEV_ADMIN";
}
