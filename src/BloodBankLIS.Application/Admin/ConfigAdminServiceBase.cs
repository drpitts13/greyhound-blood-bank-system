using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Shared plumbing for admin configuration services: records both the global audit event
/// and the versioned configuration-history snapshot in the same unit of work.
/// </summary>
public abstract class ConfigAdminServiceBase
{
    protected ConfigAdminServiceBase(
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
    {
        UnitOfWork = unitOfWork;
        Clock = clock;
        CurrentUser = currentUser;
        Audit = audit;
        History = history;
    }

    protected IUnitOfWork UnitOfWork { get; }

    protected IClock Clock { get; }

    protected ICurrentUser CurrentUser { get; }

    protected IAuditWriter Audit { get; }

    protected IConfigurationHistoryWriter History { get; }

    /// <summary>Writes an audit event and a configuration-history snapshot for one change.</summary>
    protected void RecordChange(
        string entityType,
        long? entityId,
        int version,
        ConfigChangeAction action,
        AuditEventType auditType,
        object? oldValue,
        object? newValue,
        string? reason)
    {
        Audit.Record(auditType, entityType, entityId, oldValue, newValue, reason);
        History.Capture(entityType, entityId, version, action, oldValue, newValue, reason);
    }

    protected static AuditEventType ToAuditType(ConfigChangeAction action) => action switch
    {
        ConfigChangeAction.Create => AuditEventType.Create,
        ConfigChangeAction.Update => AuditEventType.Update,
        ConfigChangeAction.Activate => AuditEventType.Activate,
        ConfigChangeAction.Deactivate => AuditEventType.Deactivate,
        ConfigChangeAction.Clone => AuditEventType.Clone,
        ConfigChangeAction.Import => AuditEventType.Import,
        ConfigChangeAction.Export => AuditEventType.Export,
        _ => AuditEventType.Configure
    };
}
