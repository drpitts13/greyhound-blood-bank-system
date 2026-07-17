using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Infrastructure.Audit;

/// <summary>
/// Stages explicit audit events (named clinical actions) on the same context as the
/// change they describe, so they commit atomically when the unit of work saves.
/// </summary>
public sealed class AuditWriter : IAuditWriter
{
    private readonly BloodBankDbContext _context;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IEnvironmentInfo? _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // IEnvironmentInfo is optional so existing call sites/tests that construct the writer
    // directly keep working; DI supplies the real environment when registered.
    public AuditWriter(BloodBankDbContext context, IClock clock, ICurrentUser currentUser, IEnvironmentInfo? environment = null)
    {
        _context = context;
        _clock = clock;
        _currentUser = currentUser;
        _environment = environment;
    }

    public void Record(
        AuditEventType eventType,
        string entityType,
        long? entityId,
        object? oldValue = null,
        object? newValue = null,
        string? reason = null,
        long? signatureId = null)
    {
        _context.AuditEvents.Add(new AuditEvent
        {
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            UserName = _currentUser.UserName,
            Workstation = _currentUser.Workstation,
            OccurredUtc = _clock.UtcNow,
            OldValueJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            NewValueJson = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            Reason = reason,
            SignatureId = signatureId,
            Environment = _environment?.EnvironmentName,
            IsDevMode = _environment?.IsDevMode ?? false
        });
    }
}
