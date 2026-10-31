using System.Globalization;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Application.Audit;

/// <summary>Audit trail rows as CSV, including the hash chain so an exported copy can be checked.</summary>
public static class AuditCsvExport
{
    public const int MaxRows = 50_000;

    public static readonly IReadOnlyList<string> Columns =
    [
        "id", "occurred_utc", "event_type", "entity_type", "entity_id", "user_name", "workstation",
        "reason", "signature_id", "old_value_json", "new_value_json", "environment", "is_dev_mode",
        "previous_hash", "record_hash"
    ];

    public static byte[] Write(IEnumerable<AuditEvent> events) =>
        CsvFormat.Write(Columns, events.Select(e => (IReadOnlyList<string?>)
        [
            e.Id.ToString(CultureInfo.InvariantCulture),
            e.OccurredUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            e.EventType.ToString(),
            e.EntityType,
            e.EntityId?.ToString(CultureInfo.InvariantCulture),
            e.UserName,
            e.Workstation,
            e.Reason,
            e.SignatureId?.ToString(CultureInfo.InvariantCulture),
            e.OldValueJson,
            e.NewValueJson,
            e.Environment,
            e.IsDevMode ? "true" : "false",
            e.PreviousHash,
            e.RecordHash
        ]));
}
