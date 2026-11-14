using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Audit;
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

        group.MapGet("/", async (
            string? entityType,
            long? entityId,
            string? eventType,
            string? userName,
            DateTime? fromUtc,
            DateTime? toUtc,
            int skip,
            int take,
            BloodBankDbContext context,
            CancellationToken ct) =>
        {
            if (!AuditTrailQuery.TryParseEventType(eventType, out var parsedEventType))
            {
                return Results.BadRequest(new { error = $"Unknown audit event type '{eventType}'." });
            }

            var query = AuditTrailQuery.Apply(
                context.AuditEvents.AsNoTracking(),
                entityType,
                entityId,
                parsedEventType,
                userName,
                fromUtc,
                toUtc);

            take = take <= 0 ? 200 : Math.Min(take, 1000);
            skip = Math.Max(0, skip);

            var total = await query.CountAsync(ct);
            var events = await query
                .OrderByDescending(a => a.OccurredUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);

            // Each row names its record as staff know it (end-user testing pass 48: "Patient #20", "Specimen #18").
            var names = await RecordNamesAsync(context, events, ct);
            var items = events.Select(e => new
            {
                e.Id, e.EventType, e.EntityType, e.EntityId, e.UserName, e.Workstation, e.OccurredUtc,
                e.OldValueJson, e.NewValueJson, e.Reason, e.SignatureId,
                EntityName = e.EntityId is long id ? names.GetValueOrDefault((e.EntityType, id)) : null
            });
            return Results.Ok(new { total, skip, take, items });
        });

        // CSV of the filtered trail, newest first, capped at AuditCsvExport.MaxRows. The export itself is audited.
        group.MapGet("/export", async (
            string? entityType,
            long? entityId,
            string? eventType,
            string? userName,
            DateTime? fromUtc,
            DateTime? toUtc,
            BloodBankDbContext context,
            Application.Abstractions.IAuditWriter audit,
            CancellationToken ct) =>
        {
            if (!AuditTrailQuery.TryParseEventType(eventType, out var parsedEventType))
            {
                return Results.BadRequest(new { error = $"Unknown audit event type '{eventType}'." });
            }

            var events = await AuditTrailQuery.Apply(
                    context.AuditEvents.AsNoTracking(), entityType, entityId, parsedEventType, userName, fromUtc, toUtc)
                .OrderByDescending(a => a.OccurredUtc)
                .ThenByDescending(a => a.Id)
                .Take(AuditCsvExport.MaxRows)
                .ToListAsync(ct);
            var bytes = AuditCsvExport.Write(events);

            audit.Record(
                Domain.Enums.AuditEventType.Export,
                "AuditTrail",
                null,
                null,
                new { Format = "csv", Rows = events.Count, entityType, entityId, eventType, userName, fromUtc, toUtc },
                "Audit trail exported.");
            await context.SaveChangesAsync(ct);

            return Results.File(bytes, Domain.Reporting.CsvFormat.ContentType, $"audit-trail-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        });

        // The entity-type filter matches exactly, so offer the types actually recorded instead of free text.
        group.MapGet("/entity-types", async (BloodBankDbContext context, CancellationToken ct) =>
            Results.Ok(await context.AuditEvents.AsNoTracking()
                .Select(a => a.EntityType)
                .Where(t => t != null && t != "")
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync(ct)));

        // The user filter also matches exactly, so offer the user names actually recorded.
        group.MapGet("/users", async (BloodBankDbContext context, CancellationToken ct) =>
            Results.Ok(await context.AuditEvents.AsNoTracking()
                .Select(a => a.UserName)
                .Where(u => u != null && u != "")
                .Distinct()
                .OrderBy(u => u)
                .ToListAsync(ct)));
    }

    /// <summary>Names for the page's patients, units, specimens and orders: one lookup per type.</summary>
    private static async Task<Dictionary<(string, long), string>> RecordNamesAsync(
        BloodBankDbContext context, IReadOnlyList<BloodBankLIS.Domain.Audit.AuditEvent> events, CancellationToken ct)
    {
        var names = new Dictionary<(string, long), string>();
        List<long> Ids(params string[] types) => events
            .Where(e => e.EntityId is not null && types.Contains(e.EntityType))
            .Select(e => e.EntityId!.Value).Distinct().ToList();

        var patients = Ids("Patient");
        if (patients.Count > 0)
        {
            foreach (var p in await context.Patients.AsNoTracking().Where(p => patients.Contains(p.Id))
                         .Select(p => new { p.Id, p.LastName, p.FirstName, p.MedicalRecordNumber }).ToListAsync(ct))
            {
                names[("Patient", p.Id)] = $"{p.LastName}, {p.FirstName} ({p.MedicalRecordNumber})";
            }
        }

        var units = Ids("BloodUnit", "Unit");
        if (units.Count > 0)
        {
            foreach (var u in await context.BloodUnits.AsNoTracking().Where(u => units.Contains(u.Id)).Select(u => new { u.Id, u.UnitNumber }).ToListAsync(ct))
            {
                names[("BloodUnit", u.Id)] = u.UnitNumber;
                names[("Unit", u.Id)] = u.UnitNumber;
            }
        }

        var specimens = Ids("Specimen");
        if (specimens.Count > 0)
        {
            foreach (var s in await context.Specimens.AsNoTracking().Where(s => specimens.Contains(s.Id)).Select(s => new { s.Id, s.AccessionNumber }).ToListAsync(ct))
            {
                names[("Specimen", s.Id)] = s.AccessionNumber;
            }
        }

        var orders = Ids("Order");
        if (orders.Count > 0)
        {
            foreach (var o in await context.Orders.AsNoTracking().Where(o => orders.Contains(o.Id)).Select(o => new { o.Id, o.OrderNumber }).ToListAsync(ct))
            {
                names[("Order", o.Id)] = o.OrderNumber;
            }
        }

        return names;
    }
}
