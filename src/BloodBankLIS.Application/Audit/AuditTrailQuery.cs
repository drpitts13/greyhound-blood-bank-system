using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Audit;

/// <summary>
/// Filters the append-only audit trail so investigators can find named clinical
/// events (who / what / when / where / old / new / why) without scanning every
/// generic Create/Update row.
/// </summary>
public static class AuditTrailQuery
{
    public static IQueryable<AuditEvent> Apply(
        IQueryable<AuditEvent> query,
        string? entityType,
        long? entityId,
        AuditEventType? eventType,
        string? userName,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var type = entityType.Trim();
            query = query.Where(a => a.EntityType == type);
        }

        if (entityId is not null)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        if (eventType is not null)
        {
            query = query.Where(a => a.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            var user = userName.Trim();
            query = query.Where(a => a.UserName == user);
        }

        if (fromUtc is not null)
        {
            var from = ToUtc(fromUtc.Value);
            query = query.Where(a => a.OccurredUtc >= from);
        }

        if (toUtc is not null)
        {
            var to = ToUtc(toUtc.Value);
            query = query.Where(a => a.OccurredUtc < to);
        }

        return query;
    }

    public static bool TryParseEventType(string? raw, out AuditEventType? eventType)
    {
        eventType = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (Enum.TryParse<AuditEventType>(raw.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            eventType = parsed;
            return true;
        }

        return false;
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
