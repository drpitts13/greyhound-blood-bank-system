namespace BloodBankLIS.Web.Hosting;

/// <summary>
/// How the Web process is hosted.
/// </summary>
public enum DesktopHostMode
{
    /// <summary>Single-PC launcher: the host may stop when its last browser session leaves.</summary>
    Desktop,

    /// <summary>Shared server for many users: the host never stops itself or touches the API process.</summary>
    Server
}

/// <summary>
/// Desktop-shortcut host. When enabled, the Web process stops after the last
/// browser session leaves so the launcher can stop the background API.
/// Ignored in <see cref="DesktopHostMode.Server"/> mode.
/// </summary>
public sealed class DesktopHostOptions
{
    public const string SectionName = "DesktopHost";

    public DesktopHostMode Mode { get; set; } = DesktopHostMode.Desktop;

    public bool ShutdownOnExit { get; set; }

    /// <summary>
    /// Seconds to wait after the last UI circuit disconnects so a refresh can
    /// reconnect before the desktop host stops.
    /// </summary>
    public int IdleExitSeconds { get; set; } = 15;

    public bool IsServer => Mode == DesktopHostMode.Server;

    /// <summary>
    /// True only for a desktop launch that asked to stop on exit; always false in Server mode.
    /// </summary>
    public bool StopsWithLastClient => !IsServer && ShutdownOnExit;
}
