using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Api.Auth;

/// <summary>
/// Request-scoped current-user resolver. Reads the authenticated identity from the
/// <c>X-User</c> / <c>X-Workstation</c> headers supplied by the API gateway. This is a
/// deliberately thin shim: a production deployment terminates real authentication
/// (OIDC / Windows / smartcard) at the gateway and forwards the verified identity, so
/// the LIS never trusts a self-asserted header from an untrusted client.
/// When there is no HTTP context (startup migration/seed, background jobs) it falls
/// back to the system account so audit metadata is still populated.
/// </summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    public const string UserHeader = "X-User";
    public const string WorkstationHeader = "X-Workstation";

    private readonly IHttpContextAccessor _accessor;
    private readonly DevModeOptions _devMode;

    public HttpCurrentUser(IHttpContextAccessor accessor, DevModeOptions devMode)
    {
        _accessor = accessor;
        _devMode = devMode;
    }

    public string UserName
    {
        get
        {
            var header = _accessor.HttpContext?.Request.Headers[UserHeader].ToString();
            if (!string.IsNullOrWhiteSpace(header))
            {
                return header.Trim();
            }

            // No-login dev mode: resolve unauthenticated callers as the dev admin so audit
            // and authorization both see a real, fully-permissioned account.
            return _devMode.Enabled ? _devMode.UserName : "system";
        }
    }

    public string? Workstation
    {
        get
        {
            var header = _accessor.HttpContext?.Request.Headers[WorkstationHeader].ToString();
            return string.IsNullOrWhiteSpace(header) ? Environment.MachineName : header.Trim();
        }
    }

    /// <summary>
    /// True when an identity header was supplied, or when dev mode is active (which resolves
    /// unauthenticated callers to the dev admin account).
    /// </summary>
    public bool IsAuthenticated
    {
        get
        {
            var header = _accessor.HttpContext?.Request.Headers[UserHeader].ToString();
            return !string.IsNullOrWhiteSpace(header) || _devMode.Enabled;
        }
    }
}
