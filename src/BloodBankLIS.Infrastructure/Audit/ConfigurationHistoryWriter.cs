using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Infrastructure.Audit;

/// <summary>
/// Stages <see cref="ConfigurationChangeHistory"/> snapshots on the same context as the
/// configuration change, so they commit atomically with the change. Stamps actor,
/// workstation, environment, and dev-mode from the current scope.
/// </summary>
public sealed class ConfigurationHistoryWriter : IConfigurationHistoryWriter
{
    private readonly BloodBankDbContext _context;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IEnvironmentInfo _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ConfigurationHistoryWriter(
        BloodBankDbContext context,
        IClock clock,
        ICurrentUser currentUser,
        IEnvironmentInfo environment)
    {
        _context = context;
        _clock = clock;
        _currentUser = currentUser;
        _environment = environment;
    }

    public void Capture(
        string entityType,
        long? entityId,
        int version,
        ConfigChangeAction action,
        object? oldValue = null,
        object? newValue = null,
        string? reason = null,
        long? signatureId = null)
    {
        _context.ConfigurationChangeHistory.Add(new ConfigurationChangeHistory
        {
            EntityType = entityType,
            EntityId = entityId,
            Version = version,
            Action = action,
            OldValueJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            NewValueJson = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            ChangeReason = reason,
            ChangedBy = _currentUser.UserName,
            Workstation = _currentUser.Workstation,
            ChangedUtc = _clock.UtcNow,
            Environment = _environment.EnvironmentName,
            IsDevMode = _environment.IsDevMode,
            SignatureId = signatureId
        });
    }
}
