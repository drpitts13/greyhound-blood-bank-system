using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
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
    private readonly IInterfaceFieldMappingRepository _mappings;

    public Hl7ConfigAdminService(
        IRepository<InterfaceEndpoint> endpoints,
        IInterfaceFieldMappingRepository mappings,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _endpoints = endpoints;
        _mappings = mappings;
    }

    public static IReadOnlyList<InterfaceDataItemDto> DataItems(InterfaceType type, Hl7Direction direction) =>
        InterfaceDataItemCatalog.For(type, direction)
            .Select(i => new InterfaceDataItemDto(i.Key, i.DisplayName, i.Description, i.DefaultHl7Path, i.Required))
            .ToList();

    public static IReadOnlyList<InterfaceVendorDto> Vendors(InterfaceType? type)
    {
        var list = type is null ? InterfaceVendorPresets.All : InterfaceVendorPresets.For(type.Value);
        return list
            .Select(v => new InterfaceVendorDto(v.Code, v.Name, v.Description, v.InterfaceTypes))
            .ToList();
    }

    public static InterfaceVendorPresetDto? VendorPreset(string code, InterfaceType type, Hl7Direction direction)
    {
        var preset = InterfaceVendorPresets.Get(code, type, direction);
        if (preset is null)
        {
            return null;
        }

        var catalog = InterfaceDataItemCatalog.For(type, direction)
            .ToDictionary(i => i.Key, i => i, StringComparer.Ordinal);

        return new InterfaceVendorPresetDto(
            preset.VendorCode,
            preset.VendorName,
            preset.InterfaceType,
            preset.Direction,
            new InterfaceVendorConnectionDto(
                preset.Connection.SendingApplication,
                preset.Connection.SendingFacility,
                preset.Connection.ReceivingApplication,
                preset.Connection.ReceivingFacility),
            preset.Mappings.Select(m =>
            {
                catalog.TryGetValue(m.DataItemKey, out var item);
                return new InterfaceFieldMappingDto(m.DataItemKey, m.Hl7Path, m.IsRequired, item?.DisplayName, item?.Description);
            }).ToList());
    }

    public async Task<IReadOnlyList<Hl7EndpointDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _endpoints.ListAsync(ct);
        var maps = await _mappings.ListAsync(ct);
        var byEndpoint = maps.GroupBy(m => m.EndpointId).ToDictionary(g => g.Key, g => (IReadOnlyList<InterfaceFieldMapping>)g.ToList());
        return items.OrderBy(e => e.Name).Select(e => Map(e, byEndpoint.GetValueOrDefault(e.Id))).ToList();
    }

    public async Task<Hl7EndpointDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var e = await _endpoints.GetByIdAsync(id, ct);
        if (e is null)
        {
            return null;
        }

        var maps = await _mappings.ListAsync(m => m.EndpointId == id, ct);
        return Map(e, maps);
    }

    public async Task<EvaluationResult<Hl7EndpointDto>> CreateAsync(SaveHl7EndpointRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = new InterfaceEndpoint { IsEnabled = false, Version = 1 };
        Apply(entity, req);
        var mappingEntities = ToEntities(req.FieldMappings);

        var evaluation = await ValidateAsync(entity, mappingEntities, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<Hl7EndpointDto>.Blocked(evaluation);
        }

        await _endpoints.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        await _mappings.ReplaceForEndpointAsync(entity, mappingEntities, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,
            oldValue: null, newValue: Map(entity, mappingEntities), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<Hl7EndpointDto>.Ok(Map(entity, mappingEntities), evaluation);
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

        var existingMaps = await _mappings.ListAsync(m => m.EndpointId == id, ct);
        var before = Map(entity, existingMaps);
        Apply(entity, req);
        var mappingEntities = ToEntities(req.FieldMappings);
        entity.Version += 1;

        var evaluation = await ValidateAsync(entity, mappingEntities, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<Hl7EndpointDto>.Blocked(evaluation);
        }

        _endpoints.Update(entity);
        await _mappings.ReplaceForEndpointAsync(entity, mappingEntities, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,
            oldValue: before, newValue: Map(entity, mappingEntities), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<Hl7EndpointDto>.Ok(Map(entity, mappingEntities), evaluation);
    }

    public async Task<EvaluationResult<Hl7EndpointDto>> SetEnabledAsync(long id, bool enabled, string? reason, CancellationToken ct = default)
    {
        var entity = await _endpoints.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<Hl7EndpointDto>.Fail("Endpoint not found.");
        }

        var maps = await _mappings.ListAsync(m => m.EndpointId == id, ct);
        if (enabled)
        {
            var evaluation = await ValidateAsync(entity, maps, ct);
            if (evaluation.IsHardStopped)
            {
                return EvaluationResult<Hl7EndpointDto>.Blocked(evaluation);
            }
        }

        entity.IsEnabled = enabled;
        _endpoints.Update(entity);

        var action = enabled ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, entity.Version, action, ToAuditType(action),
            oldValue: null, newValue: Map(entity, maps), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<Hl7EndpointDto>.Ok(Map(entity, maps));
    }

    private async Task<Domain.Rules.RuleEvaluation> ValidateAsync(
        InterfaceEndpoint e,
        IReadOnlyList<InterfaceFieldMapping> mappings,
        CancellationToken ct)
    {
        var duplicateName = !string.IsNullOrWhiteSpace(e.Name)
            && await _endpoints.AnyAsync(x => x.Id != e.Id && x.Name == e.Name, ct);

        var duplicateHostPort = e.Host is not null && e.Port is not null
            && await _endpoints.AnyAsync(x => x.Id != e.Id && x.IsEnabled && x.Host == e.Host && x.Port == e.Port, ct);

        return Hl7EndpointValidator.Validate(e, duplicateName, duplicateHostPort, mappings);
    }

    private static void Apply(InterfaceEndpoint e, SaveHl7EndpointRequest req)
    {
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.InterfaceType = req.InterfaceType;
        e.Direction = req.Direction;
        e.Transport = req.Transport;
        e.Host = req.Host?.Trim();
        e.Port = req.Port;
        e.Path = req.Path?.Trim();
        e.MessageTypes = string.IsNullOrWhiteSpace(req.MessageTypes)
            ? InterfaceTypeDefaults.MessageTypes(req.InterfaceType)
            : req.MessageTypes.Trim();
        e.VendorCode = string.IsNullOrWhiteSpace(req.VendorCode) ? null : req.VendorCode.Trim();
        e.MappingProfile = e.VendorCode ?? req.MappingProfile?.Trim();
        e.MappingMode = req.MappingMode;
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

    private static List<InterfaceFieldMapping> ToEntities(IReadOnlyList<InterfaceFieldMappingDto>? mappings)
    {
        if (mappings is null || mappings.Count == 0)
        {
            return [];
        }

        return mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.DataItemKey))
            .Select(m => new InterfaceFieldMapping
            {
                DataItemKey = m.DataItemKey.Trim(),
                Hl7Path = m.Hl7Path?.Trim() ?? string.Empty,
                IsRequired = m.IsRequired
            })
            .ToList();
    }

    private static Hl7EndpointDto Map(InterfaceEndpoint e, IReadOnlyList<InterfaceFieldMapping>? mappings)
    {
        var catalog = InterfaceDataItemCatalog.For(e.InterfaceType, e.Direction)
            .ToDictionary(i => i.Key, i => i, StringComparer.Ordinal);

        var mapped = (mappings ?? [])
            .OrderBy(m => m.DataItemKey)
            .Select(m =>
            {
                catalog.TryGetValue(m.DataItemKey, out var item);
                return new InterfaceFieldMappingDto(
                    m.DataItemKey,
                    m.Hl7Path,
                    m.IsRequired || (item?.Required ?? false),
                    item?.DisplayName,
                    item?.Description);
            })
            .ToList();

        return new(
            e.Id, e.Name, e.InterfaceType, e.Direction, e.Transport, e.Host, e.Port, e.Path, e.MessageTypes,
            e.MappingProfile, e.VendorCode, e.MappingMode, e.IsEnabled,
            e.Environment, e.SendingApplication, e.SendingFacility, e.ReceivingApplication, e.ReceivingFacility,
            e.AckTimeoutSeconds, e.MaxRetryCount, e.RetryDelaySeconds, e.MessageLoggingLevel, e.ReplayAllowed, e.Version,
            mapped);
    }
}
