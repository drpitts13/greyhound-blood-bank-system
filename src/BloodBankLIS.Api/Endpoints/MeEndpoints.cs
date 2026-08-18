using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

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
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            IRepository<User> users,
            IPermissionEvaluator permissions,
            IAuditWriter audit,
            BloodBankDbContext context,
            CancellationToken ct) =>
        {
            var user = await users.FirstOrDefaultAsync(u => u.UserName == request.UserName, ct);
            if (user is null || !user.IsActive)
            {
                return Results.Unauthorized();
            }

            if (user.IsLocked)
            {
                audit.Record(AuditEventType.Lockout, nameof(User), user.Id, reason: "Locked account sign-in attempt");
                await context.SaveChangesAsync(ct);
                return Results.Unauthorized();
            }

            if (!string.IsNullOrEmpty(user.PasswordHash)
                && !SecretHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
            {
                user.FailedSignInCount++;
                if (user.FailedSignInCount >= 5)
                {
                    user.IsLocked = true;
                    audit.Record(AuditEventType.Lockout, nameof(User), user.Id, reason: "Failed sign-in lockout");
                }
                else
                {
                    audit.Record(AuditEventType.SignatureFailed, nameof(User), user.Id, reason: "Failed sign-in");
                }

                await context.SaveChangesAsync(ct);
                return Results.Unauthorized();
            }

            user.LastLoginUtc = DateTime.UtcNow;
            user.FailedSignInCount = 0;
            audit.Record(AuditEventType.Login, nameof(User), user.Id);
            await context.SaveChangesAsync(ct);

            var codes = await permissions.GetPermissionsAsync(user.UserName, ct);
            var securityLevel = await permissions.GetMaxSecurityLevelAsync(user.UserName, ct);
            return Results.Ok(new
            {
                userName = user.UserName,
                displayName = user.DisplayName,
                securityLevel,
                permissions = codes.OrderBy(c => c).ToArray()
            });
        }).WithTags("Identity");

        app.MapPost("/api/auth/logout", async (
            ICurrentUser currentUser,
            IRepository<User> users,
            IAuditWriter audit,
            BloodBankDbContext context,
            CancellationToken ct) =>
        {
            var user = await users.FirstOrDefaultAsync(u => u.UserName == currentUser.UserName, ct);
            audit.Record(AuditEventType.Logout, nameof(User), user?.Id);
            await context.SaveChangesAsync(ct);
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
}

public sealed record LoginRequest(string UserName, string? Password = null, string? Workstation = null);
