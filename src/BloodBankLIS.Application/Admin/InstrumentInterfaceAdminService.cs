using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

public sealed class InstrumentInterfaceAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(InstrumentInterface);
    private const string ProductionEnvironment = "Production";

    private readonly IInstrumentInterfaceRepository _interfaces;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public InstrumentInterfaceAdminService(
        IInstrumentInterfaceRepository interfaces,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _interfaces = interfaces;
        _permissionEvaluator = permissionEvaluator;
    }

    public static IReadOnlyList<InstrumentDataItemDto> DataItems() =>
        InstrumentDataItemCatalog.All
            .Select(i => new InstrumentDataItemDto(i.Key, i.DisplayName, i.Description, i.DefaultAstmPath, i.Required))
            .ToList();

    public async Task<IReadOnlyList<InstrumentInterfaceDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _interfaces.ListWithChildrenAsync(ct);
        return items.OrderBy(i => i.Name).Select(Map).ToList();
    }

    public async Task<InstrumentInterfaceDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _interfaces.GetWithChildrenAsync(id, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<EvaluationResult<InstrumentInterfaceDto>> CreateAsync(SaveInstrumentInterfaceRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminInstrumentsManage, InstrumentInterfaceAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new InstrumentInterface { IsEnabled = false, Version = 1 };
        Apply(entity, req);
        var children = ToChildren(req);

        var evaluation = await ValidateAsync(entity, children, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<InstrumentInterfaceDto>.Blocked(evaluation);
        }

        await _interfaces.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        await _interfaces.ReplaceChildrenAsync(entity, children.Connection, children.Fields, children.Tests, children.Rules, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Interface,
            oldValue: null, newValue: Map(entity, children), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        var saved = await _interfaces.GetWithChildrenAsync(entity.Id, ct);
        return EvaluationResult<InstrumentInterfaceDto>.Ok(Map(saved!), evaluation);
    }

    public async Task<EvaluationResult<InstrumentInterfaceDto>> UpdateAsync(long id, SaveInstrumentInterfaceRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminInstrumentsManage, InstrumentInterfaceAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _interfaces.GetWithChildrenAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<InstrumentInterfaceDto>.Fail("Instrument interface not found.");
        }

        var touchesProduction = string.Equals(entity.Environment, ProductionEnvironment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(req.Environment, ProductionEnvironment, StringComparison.OrdinalIgnoreCase);
        if ((entity.IsEnabled || touchesProduction) && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<InstrumentInterfaceDto>.Fail("A change reason is required to edit an enabled or production interface.");
        }

        var before = Map(entity);
        Apply(entity, req);
        var children = ToChildren(req);
        entity.Version += 1;

        var evaluation = await ValidateAsync(entity, children, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<InstrumentInterfaceDto>.Blocked(evaluation);
        }

        _interfaces.Update(entity);
        await _interfaces.ReplaceChildrenAsync(entity, children.Connection, children.Fields, children.Tests, children.Rules, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Interface,
            oldValue: before, newValue: Map(entity, children), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        var saved = await _interfaces.GetWithChildrenAsync(id, ct);
        return EvaluationResult<InstrumentInterfaceDto>.Ok(Map(saved!), evaluation);
    }

    public async Task<EvaluationResult<InstrumentInterfaceDto>> SetEnabledAsync(long id, bool enabled, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminInstrumentsManage,
            enabled ? InstrumentInterfaceAuthorizationRule.EvaluateEnable : InstrumentInterfaceAuthorizationRule.EvaluateDisable,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _interfaces.GetWithChildrenAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<InstrumentInterfaceDto>.Fail("Instrument interface not found.");
        }

        if (enabled)
        {
            var children = new ChildSet(
                entity.Connection ?? new InstrumentConnection { Port = 0, InterfaceId = entity.Id },
                entity.FieldMappings.ToList(),
                entity.TestMappings.ToList(),
                entity.Rules.ToList());
            var evaluation = await ValidateAsync(entity, children, ct);
            if (evaluation.IsHardStopped)
            {
                return EvaluationResult<InstrumentInterfaceDto>.Blocked(evaluation);
            }
        }

        entity.IsEnabled = enabled;
        _interfaces.Update(entity);
        var action = enabled ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, entity.Version, action, ToAuditType(action),
            oldValue: null, newValue: Map(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<InstrumentInterfaceDto>.Ok(Map(entity));
    }

    private async Task<RuleEvaluation> ValidateAsync(InstrumentInterface entity, ChildSet children, CancellationToken ct)
    {
        var previous = entity.Connection;
        entity.Connection = children.Connection;
        try
        {
            var duplicateName = !string.IsNullOrWhiteSpace(entity.Name)
                && await _interfaces.AnyAsync(x => x.Id != entity.Id && x.Name == entity.Name, ct);
            var port = children.Connection.Port;
            var duplicatePort = port > 0 && await PortTakenAsync(entity.Id, port, ct);
            return InstrumentInterfaceValidator.Validate(
                entity, duplicateName, duplicatePort, children.Fields, children.Tests, children.Rules);
        }
        finally
        {
            entity.Connection = previous;
        }
    }

    private async Task<bool> PortTakenAsync(long id, int port, CancellationToken ct)
    {
        var others = await _interfaces.ListWithChildrenAsync(ct);
        return others.Any(o => o.Id != id && o.IsEnabled && o.Connection is { Port: var p } && p == port);
    }

    private static void Apply(InstrumentInterface entity, SaveInstrumentInterfaceRequest req)
    {
        entity.Name = req.Name?.Trim() ?? string.Empty;
        entity.Environment = req.Environment?.Trim();
    }

    private static ChildSet ToChildren(SaveInstrumentInterfaceRequest req)
    {
        var connection = new InstrumentConnection
        {
            IpAddress = string.IsNullOrWhiteSpace(req.Connection.IpAddress) ? null : req.Connection.IpAddress.Trim(),
            Port = req.Connection.Port
        };
        var fields = (req.FieldMappings ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m.DataItemKey))
            .Select(m => new InstrumentFieldMapping
            {
                DataItemKey = m.DataItemKey.Trim(),
                AstmPath = m.AstmPath?.Trim() ?? string.Empty,
                IsRequired = m.IsRequired
            })
            .ToList();
        var tests = (req.TestMappings ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m.InstrumentCode))
            .Select(m => new InstrumentTestMapping
            {
                InstrumentCode = m.InstrumentCode.Trim(),
                ApplicationCode = m.ApplicationCode?.Trim() ?? string.Empty,
                Kind = m.Kind,
                Direction = m.Direction,
                SortOrder = m.SortOrder
            })
            .ToList();
        var rules = (req.Rules ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => new InstrumentInterfaceRule
            {
                Name = r.Name.Trim(),
                ConditionExpression = r.ConditionExpression?.Trim() ?? string.Empty,
                ActionExpression = r.ActionExpression?.Trim() ?? string.Empty,
                SortOrder = r.SortOrder,
                IsEnabled = r.IsEnabled,
                StopOnMatch = r.StopOnMatch
            })
            .ToList();
        return new ChildSet(connection, fields, tests, rules);
    }

    private static InstrumentInterfaceDto Map(InstrumentInterface entity) =>
        Map(entity, new ChildSet(
            entity.Connection ?? new InstrumentConnection { Port = 0 },
            entity.FieldMappings.ToList(),
            entity.TestMappings.ToList(),
            entity.Rules.ToList()));

    private static InstrumentInterfaceDto Map(InstrumentInterface entity, ChildSet children)
    {
        var catalog = InstrumentDataItemCatalog.All.ToDictionary(i => i.Key, i => i, StringComparer.OrdinalIgnoreCase);
        return new InstrumentInterfaceDto(
            entity.Id,
            entity.Name,
            entity.IsEnabled,
            entity.Environment,
            entity.Version,
            new InstrumentConnectionDto(children.Connection.IpAddress, children.Connection.Port, children.Connection.IsServer),
            children.Fields.Select(m =>
            {
                catalog.TryGetValue(m.DataItemKey, out var item);
                return new InstrumentFieldMappingDto(m.DataItemKey, m.AstmPath, m.IsRequired || (item?.Required ?? false), item?.DisplayName);
            }).ToList(),
            children.Tests
                .OrderBy(m => m.SortOrder)
                .Select(m => new InstrumentTestMappingDto(m.InstrumentCode, m.ApplicationCode, m.Kind, m.Direction, m.SortOrder))
                .ToList(),
            children.Rules
                .OrderBy(r => r.SortOrder)
                .Select(r => new InstrumentRuleDto(r.Name, r.ConditionExpression, r.ActionExpression, r.SortOrder, r.IsEnabled, r.StopOnMatch))
                .ToList());
    }

    private async Task<EvaluationResult<InstrumentInterfaceDto>?> RejectUnauthorizedAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<InstrumentInterfaceDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private sealed record ChildSet(
        InstrumentConnection Connection,
        List<InstrumentFieldMapping> Fields,
        List<InstrumentTestMapping> Tests,
        List<InstrumentInterfaceRule> Rules);
}
