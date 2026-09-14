using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Api.Auth;

/// <summary>
/// Request-scoped current-user resolver. Identity comes from a validated session
/// (or Development DevMode) staged by <see cref="AuthSessionMiddleware"/>.
/// A self-asserted <c>X-User</c> header is not trusted.
/// When there is no HTTP context (startup migration/seed, background jobs) it falls
/// back to the system account so audit metadata is still populated.
/// </summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    public const string UserHeader = AuthSessionMiddleware.UserHeader;
    public const string WorkstationHeader = AuthSessionMiddleware.WorkstationHeader;

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
            var http = _accessor.HttpContext;
            if (http is not null)
            {
                return AuthHttpContext.GetIdentity(http).UserName;
            }

            return _devMode.Enabled ? _devMode.UserName : "system";
        }
    }

    public string? Workstation
    {
        get
        {
            var http = _accessor.HttpContext;
            if (http is not null)
            {
                var workstation = AuthHttpContext.GetIdentity(http).Workstation;
                return string.IsNullOrWhiteSpace(workstation) ? Environment.MachineName : workstation;
            }

            return Environment.MachineName;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            var http = _accessor.HttpContext;
            if (http is not null)
            {
                return AuthHttpContext.GetIdentity(http).IsAuthenticated;
            }

            return _devMode.Enabled;
        }
    }
}
