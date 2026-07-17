using BloodBankLIS.Api.Auth;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>
/// Read-only access to the audit trail. There is intentionally no create/update/
/// delete endpoint for audit events (see docs/architecture.md 4.1).
/// </summary>
public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/audit-events").WithTags("Audit")
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.AuditRead);

        group.MapGet("/", async (string? entityType, long? entityId, BloodBankDbContext context, CancellationToken ct) =>
        {
            var query = context.AuditEvents.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                query = query.Where(a => a.EntityType == entityType);
            }

            if (entityId is not null)
            {
                query = query.Where(a => a.EntityId == entityId);
            }

            var events = await query
                .OrderByDescending(a => a.OccurredUtc)
                .Take(200)
                .ToListAsync(ct);

            return Results.Ok(events);
        });
    }
}
