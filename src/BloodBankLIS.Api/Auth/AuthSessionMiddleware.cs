using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Identity;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Auth;

/// <summary>
/// Resolves the interactive operator from a server-issued Bearer session.
/// A self-asserted <c>X-User</c> header is ignored unless
/// <see cref="AuthSessionOptions.AllowLegacyIdentityHeader"/> is on.
/// </summary>
public sealed class AuthSessionMiddleware
{
    public const string UserHeader = "X-User";
    public const string WorkstationHeader = "X-Workstation";

    private readonly RequestDelegate _next;

    public AuthSessionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IAuthSessionService sessions,
        AuthSessionOptions options,
        DevModeOptions devMode)
    {
        var token = RequestIdentityResolver.ReadBearerToken(context.Request.Headers.Authorization);
        var session = await sessions.ResolveAsync(token, context.RequestAborted);
        var identity = RequestIdentityResolver.Resolve(
            session,
            context.Request.Headers[UserHeader],
            context.Request.Headers[WorkstationHeader],
            options.AllowLegacyIdentityHeader,
            devMode.Enabled,
            devMode.UserName);

        context.Items[AuthHttpContext.IdentityKey] = identity;
        if (session is not null)
        {
            context.Items[AuthHttpContext.SessionIdKey] = session.SessionId;
            context.Items[AuthHttpContext.BearerTokenKey] = token;
            await sessions.TouchAsync(session.SessionId, context.RequestAborted);
        }

        await _next(context);
    }
}
