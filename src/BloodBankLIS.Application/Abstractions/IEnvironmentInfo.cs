namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Describes the hosting environment for audit stamping and dev-mode behavior. Resolved
/// in the composition root from the host environment and DevMode configuration.
/// </summary>
public interface IEnvironmentInfo
{
    /// <summary>Host environment name, e.g. "Development" or "Production".</summary>
    string EnvironmentName { get; }

    /// <summary>True when no-login dev mode is effectively active (enabled AND Development).</summary>
    bool IsDevMode { get; }
}
