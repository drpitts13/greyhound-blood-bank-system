using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin management of HL7 <see cref="InterfaceEndpoint"/>s. Validates host/port and detects
/// duplicate names / host:port collisions. Production endpoints cannot be edited without a
/// change reason. Historical message logs are never touched here.
/// </summary>
public sealed class Hl7ConfigAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(InterfaceEndpoint);
    private const string ProductionEnvironment = "Production";

    private readonly IRepository<InterfaceEndpoint> _endpoints;

    public Hl7ConfigAdminService(
        IRepository<InterfaceEndpoint> endpoints,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _endpoints = endpoints;
    }

    public async Task<IReadOnlyList<Hl7EndpointDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _endpoints.ListAsync(ct);
        return items.OrderBy(e => e.Name).Select(Map).ToList();
    }

    public async Task<Hl7EndpointDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var e = await _endpoints.GetByIdAsync(id, ct);
        return e is null ? null : Map(e);
    }

    public async Task<EvaluationResult<Hl7EndpointDto>> CreateAsync(SaveHl7EndpointRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = new InterfaceEndpoint { IsEnabled = false, Version = 1 };
        Apply(entity, req);

        var evaluation = await ValidateAsync(entity, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<Hl7EndpointDto>.Blocked(evaluation);
        }

        await _endpoints.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,
            oldValue: null, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<Hl7EndpointDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<Hl7EndpointDto>> UpdateAsync(long id, SaveHl7EndpointRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = await _endpoints.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<Hl7EndpointDto>.Fail("Endpoint not found.");
        }

        var touchesProduction = string.Equals(entity.Environment, ProductionEnvironment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(req.Environment, ProductionEnvironment, StringComparison.OrdinalIgnoreCase);
        if ((entity.IsEnabled || touchesProduction) && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<Hl7EndpointDto>.Fail("A change reason is required to edit an enabled or production endpoint.");
        }

        var before = Map(entity);
        Apply(entity, req);
        entity.Version += 1;

        var evaluation = await ValidateAsync(entity, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<Hl7EndpointDto>.Blocked(evaluation);
        }

        _endpoints.Update(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,
            oldValue: before, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<Hl7EndpointDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<Hl7EndpointDto>> SetEnabledAsync(long id, bool enabled, string? reason, CancellationToken ct = default)
    {
        var entity = await _endpoints.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<Hl7EndpointDto>.Fail("Endpoint not found.");
        }

        if (enabled)
        {
            var evaluation = await ValidateAsync(entity, ct);
            if (evaluation.IsHardStopped)
            {
                return EvaluationResult<Hl7EndpointDto>.Blocked(evaluation);
            }
        }

        entity.IsEnabled = enabled;
        _endpoints.Update(entity);

        var action = enabled ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, entity.Version, action, ToAuditType(action),
            oldValue: null, newValue: Map(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<Hl7EndpointDto>.Ok(Map(entity));
    }

    private async Task<Domain.Rules.RuleEvaluation> ValidateAsync(InterfaceEndpoint e, CancellationToken ct)
    {
        var duplicateName = !string.IsNullOrWhiteSpace(e.Name)
            && await _endpoints.AnyAsync(x => x.Id != e.Id && x.Name == e.Name, ct);

        var duplicateHostPort = e.Host is not null && e.Port is not null
            && await _endpoints.AnyAsync(x => x.Id != e.Id && x.IsEnabled && x.Host == e.Host && x.Port == e.Port, ct);

        return Hl7EndpointValidator.Validate(e, duplicateName, duplicateHostPort);
    }

    private static void Apply(InterfaceEndpoint e, SaveHl7EndpointRequest req)
    {
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.Direction = req.Direction;
        e.Transport = req.Transport;
        e.Host = req.Host?.Trim();
        e.Port = req.Port;
        e.Path = req.Path?.Trim();
        e.MessageTypes = req.MessageTypes?.Trim() ?? string.Empty;
        e.MappingProfile = req.MappingProfile?.Trim();
        e.Environment = req.Environment?.Trim();
        e.SendingApplication = req.SendingApplication?.Trim();
        e.SendingFacility = req.SendingFacility?.Trim();
        e.ReceivingApplication = req.ReceivingApplication?.Trim();
        e.ReceivingFacility = req.ReceivingFacility?.Trim();
        e.AckTimeoutSeconds = req.AckTimeoutSeconds;
        e.MaxRetryCount = req.MaxRetryCount;
        e.RetryDelaySeconds = req.RetryDelaySeconds;
        e.MessageLoggingLevel = req.MessageLoggingLevel?.Trim();
        e.ReplayAllowed = req.ReplayAllowed;
    }

    private static Hl7EndpointDto Map(InterfaceEndpoint e) => new(
        e.Id, e.Name, e.Direction, e.Transport, e.Host, e.Port, e.Path, e.MessageTypes, e.MappingProfile, e.IsEnabled,
        e.Environment, e.SendingApplication, e.SendingFacility, e.ReceivingApplication, e.ReceivingFacility,
        e.AckTimeoutSeconds, e.MaxRetryCount, e.RetryDelaySeconds, e.MessageLoggingLevel, e.ReplayAllowed, e.Version);
}
