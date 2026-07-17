using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Infrastructure.Common;

/// <summary>
/// Placeholder current-user resolver for Phase 1 (no authentication yet). Real
/// per-request identity and electronic-signature support arrive with the Security
/// phase; this keeps audit metadata populated in the meantime.
/// </summary>
public sealed class StaticCurrentUser : ICurrentUser
{
    public StaticCurrentUser(string userName = "system", string? workstation = null)
    {
        UserName = userName;
        Workstation = workstation;
    }

    public string UserName { get; }

    public string? Workstation { get; }
}
