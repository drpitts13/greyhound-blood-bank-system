namespace BloodBankLIS.Application.Identity;

/// <summary>
/// Interactive authentication. Legacy <c>X-User</c> is off unless an explicit
/// test host sets <see cref="AllowLegacyIdentityHeader"/>.
/// </summary>
public sealed class AuthSessionOptions
{
    public const string SectionName = "Auth";

    public bool AllowLegacyIdentityHeader { get; set; }

    public int IdleTimeoutMinutes { get; set; } = 30;

    public int AbsoluteTimeoutHours { get; set; } = 12;
}
