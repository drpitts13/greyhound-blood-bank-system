using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Identity;

/// <summary>
/// Sign-in outcome. When <see cref="Challenge"/> is set the password was right but no
/// session was issued: the caller must send a code (and, for enrollment, set up the key
/// shown). Permissions are withheld until the session exists.
/// </summary>
public sealed record AuthLoginResult(
    string SessionToken,
    string UserName,
    string DisplayName,
    int SecurityLevel,
    IReadOnlyList<string> Permissions,
    MfaChallenge? Challenge = null)
{
    public bool SignedIn => Challenge is null && !string.IsNullOrEmpty(SessionToken);
}

public sealed record MfaChallenge(MfaRequirement Kind, string? SecretBase32 = null, string? ProvisioningUri = null);
