using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Infrastructure.Common;

/// <summary>
/// Fixed environment descriptor. The composition root (API/Web) supplies the real
/// environment name and effective dev-mode flag; a default instance keeps migrations,
/// seeding, and tests working without host wiring.
/// </summary>
public sealed class StaticEnvironmentInfo : IEnvironmentInfo
{
    public StaticEnvironmentInfo(string environmentName = "Unknown", bool isDevMode = false)
    {
        EnvironmentName = environmentName;
        IsDevMode = isDevMode;
    }

    public string EnvironmentName { get; }

    public bool IsDevMode { get; }
}
