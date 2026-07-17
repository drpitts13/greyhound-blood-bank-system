using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities.Identity;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>
/// Identity introspection for the calling user: the resolved display name and the
/// effective permission set. Lets a client render role-aware UI; the API remains the
/// authority and re-checks permissions on every protected route.
/// </summary>
public static class MeEndpoints
{
    public static void MapMeEndpoints(this WebApplication app)
    {
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
}
