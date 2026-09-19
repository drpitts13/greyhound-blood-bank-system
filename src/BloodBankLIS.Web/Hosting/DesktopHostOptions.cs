namespace BloodBankLIS.Web.Hosting;

/// <summary>
/// Desktop-shortcut host. When enabled, the Web process stops after the last
/// browser session leaves so the launcher can stop the background API.
/// </summary>
public sealed class DesktopHostOptions
{
    public const string SectionName = "DesktopHost";

    public bool ShutdownOnExit { get; set; }

    /// <summary>
    /// Seconds to wait after the last UI circuit disconnects so a refresh can
    /// reconnect before the desktop host stops.
    /// </summary>
    public int IdleExitSeconds { get; set; } = 15;
}
