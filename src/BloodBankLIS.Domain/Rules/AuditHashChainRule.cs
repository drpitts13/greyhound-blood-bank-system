using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Tamper-evident hash for append-only <see cref="AuditEvent"/> rows.
/// Does not define retention or purge (OCD-007). Pre-chain rows (null
/// <see cref="AuditEvent.RecordHash"/>) are not covered.
/// </summary>
public static class AuditHashChainRule
{
    public const string GenesisPreviousHash = "GENESIS";
    public const string OkCode = "AUDIT-CHAIN-OK";
    public const string LinkCode = "AUDIT-CHAIN-LINK";
    public const string PayloadCode = "AUDIT-CHAIN-PAYLOAD";

    public static string ComputeRecordHash(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return ComputeRecordHash(
            auditEvent.EventType,
            auditEvent.EntityType,
            auditEvent.EntityId,
            auditEvent.UserName,
            auditEvent.Workstation,
            auditEvent.OccurredUtc,
            auditEvent.OldValueJson,
            auditEvent.NewValueJson,
            auditEvent.Reason,
            auditEvent.SignatureId,
            auditEvent.Environment,
            auditEvent.IsDevMode,
            auditEvent.PreviousHash);
    }

    public static string ComputeRecordHash(
        AuditEventType eventType,
        string entityType,
        long? entityId,
        string userName,
        string? workstation,
        DateTime occurredUtc,
        string? oldValueJson,
        string? newValueJson,
        string? reason,
        long? signatureId,
        string? environment,
        bool isDevMode,
        string? previousHash)
    {
        var occurred = NormalizeUtc(occurredUtc);
        var payload = string.Join(
            '\n',
            ((int)eventType).ToString(CultureInfo.InvariantCulture),
            entityType ?? string.Empty,
            entityId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            userName ?? string.Empty,
            workstation ?? string.Empty,
            occurred.ToString("O", CultureInfo.InvariantCulture),
            oldValueJson ?? string.Empty,
            newValueJson ?? string.Empty,
            reason ?? string.Empty,
            signatureId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            environment ?? string.Empty,
            isDevMode ? "1" : "0",
            previousHash ?? string.Empty);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>
    /// Walks hashed rows from <see cref="GenesisPreviousHash"/> by
    /// <see cref="AuditEvent.PreviousHash"/>. Insert/id order is not required.
    /// Unhashed (pre-chain) rows are skipped.
    /// </summary>
    public static RuleResult Verify(IReadOnlyList<AuditEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var hashed = events.Where(e => !string.IsNullOrWhiteSpace(e.RecordHash)).ToList();
        if (hashed.Count == 0)
        {
            return RuleResult.Pass(OkCode, "No hashed audit rows to verify.");
        }

        var byPrevious = hashed.ToLookup(e => e.PreviousHash, StringComparer.Ordinal);
        var roots = byPrevious[GenesisPreviousHash].ToList();
        if (roots.Count != 1)
        {
            return RuleResult.HardStop(
                LinkCode,
                "Audit hash chain must have exactly one GENESIS root.");
        }

        var current = roots[0];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (current is not null)
        {
            if (!seen.Add(current.RecordHash!))
            {
                return RuleResult.HardStop(LinkCode, "Audit hash chain contains a cycle.");
            }

            if (!string.Equals(current.RecordHash, ComputeRecordHash(current), StringComparison.Ordinal))
            {
                return RuleResult.HardStop(
                    PayloadCode,
                    "Audit record hash does not match the stored payload.");
            }

            var children = byPrevious[current.RecordHash].ToList();
            if (children.Count > 1)
            {
                return RuleResult.HardStop(LinkCode, "Audit hash chain has a fork.");
            }

            current = children.Count == 0 ? null : children[0];
        }

        if (seen.Count != hashed.Count)
        {
            return RuleResult.HardStop(
                LinkCode,
                "Audit hash chain has hashed rows that are not reachable from GENESIS.");
        }

        return RuleResult.Pass(OkCode, "Hashed audit rows form a contiguous chain.");
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
    }
}
