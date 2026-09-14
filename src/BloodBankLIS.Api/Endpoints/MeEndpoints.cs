using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Identity;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>
/// Interactive sign-in issues a server-side session. Subsequent requests must
/// present the token as Authorization Bearer. Identity introspection is for UI
/// gating only; the API re-checks permissions on every protected route.
/// </summary>
public static class MeEndpoints
{
    public static void MapMeEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            IAuthSessionService sessions,
            CancellationToken ct) =>
        {
            var result = await sessions.LoginAsync(request.UserName, request.Password, request.Workstation, ct);
            if (!result.Succeeded || result.Value is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(ToLoginResponse(result.Value));
        }).WithTags("Identity");

        app.MapPost("/api/auth/logout", async (
            HttpContext http,
            IAuthSessionService sessions,
            CancellationToken ct) =>
        {
            var token = http.Items.TryGetValue(AuthHttpContext.BearerTokenKey, out var stored) && stored is string s
                ? s
                : RequestIdentityResolver.ReadBearerToken(http.Request.Headers.Authorization);
            await sessions.LogoutAsync(token, ct);
            return Results.Ok();
        })
        .RequireAuthenticatedUser()
        .WithTags("Identity");

        app.MapGet("/api/me", async (
            ICurrentUser currentUser,
            IPermissionEvaluator permissions,
            IRepository<User> users,
            CancellationToken ct) =>
        {
            var user = await users.FirstOrDefaultAsync(u => u.UserName == currentUser.UserName && u.IsActive && !u.IsLocked, ct);
            if (user is null)
            {
                return Results.NotFound(new { error = "The current identity does not map to an active account." });
            }

            var codes = await permissions.GetPermissionsAsync(currentUser.UserName, ct);
            var securityLevel = await permissions.GetMaxSecurityLevelAsync(currentUser.UserName, ct);
            return Results.Ok(new
            {
                userName = user.UserName,
                displayName = user.DisplayName,
                securityLevel,
                permissions = codes.OrderBy(c => c).ToArray()
            });
        })
        .RequireAuthenticatedUser()
        .WithTags("Identity");
    }

    private static object ToLoginResponse(AuthLoginResult login) => new
    {
        sessionToken = login.SessionToken,
        userName = login.UserName,
        displayName = login.DisplayName,
        securityLevel = login.SecurityLevel,
        permissions = login.Permissions
    };
}

public sealed record LoginRequest(string UserName, string? Password = null, string? Workstation = null);
