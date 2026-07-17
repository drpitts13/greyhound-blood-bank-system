namespace BloodBankLIS.Api.Auth;

/// <summary>
/// Development-only "no login" mode. When enabled in a Development host, unauthenticated
/// requests are treated as the <see cref="UserName"/> account (seeded with full permissions),
/// and the change history/audit is stamped as dev-mode. It is a hard error to enable this in
/// any non-Development environment (enforced by a startup guard).
/// </summary>
public sealed class DevModeOptions
{
    public const string SectionName = "DevMode";

    public bool Enabled { get; set; }

    public string UserName { get; set; } = "DEV_ADMIN";
}
