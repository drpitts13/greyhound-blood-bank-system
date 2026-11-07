using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Admin;

public sealed class InstrumentInterfaceAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(InstrumentInterface);
    private const string ProductionEnvironment = "Production";

    private readonly IInstrumentInterfaceRepository _interfaces;
    private readonly IPermissionEvaluator? _permissionEvaluator;
    private readonly IRepository<TestDefinition>? _tests;
    private readonly IRepository<SubtestDefinition>? _subtests;
    private readonly IRepository<PhaseDefinition>? _phases;
    private readonly InterfaceUnmappedValueService? _unmapped;

    public InstrumentInterfaceAdminService(
        IInstrumentInterfaceRepository interfaces,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null,
        IRepository<TestDefinition>? tests = null,
        IRepository<SubtestDefinition>? subtests = null,
        IRepository<PhaseDefinition>? phases = null,
        InterfaceUnmappedValueService? unmapped = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _interfaces = interfaces;
        _permissionEvaluator = permissionEvaluator;
        _tests = tests;
        _subtests = subtests;
        _phases = phases;
        _unmapped = unmapped;
    }

    /// <summary>The translation fields, each with the local values it may use (tests, subtests, or error actions).</summary>
    public async Task<IReadOnlyList<InstrumentTranslationFieldDto>> TranslationFieldsAsync(CancellationToken ct = default)
    {
        var tests = await TestValuesAsync(ct);
        var subtests = await SubtestValuesAsync(tests, ct);
        IReadOnlyList<InterfaceLocalValueDto> actions = Enum.GetValues<InstrumentErrorAction>()
            .Select(a => new InterfaceLocalValueDto(a.ToString(), a switch
            {
                InstrumentErrorAction.Hold => "Do not post; review in instrument failures",
                InstrumentErrorAction.Warn => "Post, with the code noted for the verifier",
                _ => "No effect"
            }, "Action"))
            .ToList();
        return InstrumentTranslationKeys.All
            .Select(f => new InstrumentTranslationFieldDto(f.Key, f.DisplayName, f.Description, f.Outbound, f.AllowsContext, f.Key switch
            {
                InstrumentTranslationKeys.OrderTestCode or InstrumentTranslationKeys.ResultTestCode => tests,
                InstrumentTranslationKeys.SubtestCode => subtests,
                InstrumentTranslationKeys.ErrorCode => actions,
                _ => []
            }))
            .ToList();
    }

    /// <summary>What the instrument's saved translations do with a value (inbound, or outbound for order test codes).</summary>
    public async Task<OperationResult<InstrumentTranslationCheckDto>> CheckAsync(long id, string key, string? value, string? context, CancellationToken ct = default)
    {
        var entity = await _interfaces.GetWithChildrenAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<InstrumentTranslationCheckDto>.Fail("Instrument interface not found.");
        }

        var field = InstrumentTranslationKeys.Find(key);
        if (field is null)
        {
            return OperationResult<InstrumentTranslationCheckDto>.Fail($"Unknown translation field '{key}'.");
        }

        var translator = InstrumentValueTranslator.From(entity.Translations);
        var input = value?.Trim() ?? string.Empty;
        var ctx = string.IsNullOrWhiteSpace(context) ? null : context.Trim();
        if (field.Outbound)
        {
            var sent = translator.ToInstrument(key, input, ctx);
            return OperationResult<InstrumentTranslationCheckDto>.Ok(new InstrumentTranslationCheckDto(
                input, sent ?? input, sent is not null, ctx,
                sent is null ? $"{input} would not be sent to the instrument: it has no instrument code." : null));
        }

        if (key == InstrumentTranslationKeys.ErrorCode)
        {
            var (action, translated) = translator.ErrorAction(input, ctx);
            var meaning = translator.Meaning(input, ctx);
            return OperationResult<InstrumentTranslationCheckDto>.Ok(new InstrumentTranslationCheckDto(
                input, action.ToString(), translated, ctx,
                translated
                    ? (meaning is null ? "No meaning recorded for this code." : $"Means: {meaning}.")
                    : "Not in this instrument's error code table, so the table cannot decide: the result is held (OCD-066)."));
        }

        var outcome = translator.Inbound(key, input, ctx);
        string? note = !outcome.Translated && key is InstrumentTranslationKeys.ResultTestCode or InstrumentTranslationKeys.SubtestCode
            ? $"No translation: a result with {input} would not post and the message would go to instrument failures."
            : outcome.Row?.Context is { Length: > 0 } limited ? $"From the row for test {limited}." : null;
        return OperationResult<InstrumentTranslationCheckDto>.Ok(new InstrumentTranslationCheckDto(input, outcome.Value, outcome.Translated, ctx, note));
    }

    private async Task<IReadOnlyList<InterfaceLocalValueDto>> TestValuesAsync(CancellationToken ct) =>
        _tests is null
            ? []
            : (await _tests.ListAsync(t => t.IsActive && !t.IsDraft, ct))
                .OrderBy(t => t.Code)
                .Select(t => new InterfaceLocalValueDto(t.Code, t.Name, "Test"))
                .ToList();

    // Subtests an analyte can fill: ABO/Rh panel reactions, configured subtests and phases, and single-value tests.
    private async Task<IReadOnlyList<InterfaceLocalValueDto>> SubtestValuesAsync(IReadOnlyList<InterfaceLocalValueDto> tests, CancellationToken ct)
    {
        var values = new List<InterfaceLocalValueDto>
        {
            new(InstrumentTranslationKeys.Keep, "Store under the instrument's own code", "Special"),
            new(InstrumentTranslationKeys.Ignore, "Received but not stored", "Special")
        };
        values.AddRange(AboRhPanelSubtestCodes.All.Select(c => new InterfaceLocalValueDto(c, c, "ABO/Rh panel")));
        if (_subtests is not null)
        {
            values.AddRange((await _subtests.ListAsync(s => s.IsActive && !s.IsDraft, ct))
                .OrderBy(s => s.Code).Select(s => new InterfaceLocalValueDto(s.Code, s.Name, "Subtest")));
        }

        if (_phases is not null)
        {
            values.AddRange((await _phases.ListAsync(p => p.IsActive && !p.IsDraft, ct))
                .OrderBy(p => p.Code).Select(p => new InterfaceLocalValueDto(p.Code, p.Name, "Phase")));
        }

        values.AddRange(tests);
        return values
            .GroupBy(v => v.Value, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>Local tests and subtests must exist; a test limit must be an active test. Skipped without the catalog.</summary>
    private async Task<IReadOnlyList<RuleResult>> ValidateLocalValuesAsync(IReadOnlyList<InstrumentValueTranslation> rows, CancellationToken ct)
    {
        if (_tests is null || rows.Count == 0)
        {
            return [];
        }

        var testValues = await TestValuesAsync(ct);
        var tests = testValues.Select(v => v.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var subtests = (await SubtestValuesAsync(testValues, ct)).Select(v => v.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<RuleResult>();
        foreach (var row in rows)
        {
            var local = row.LocalValue.Trim();
            var known = row.DataItemKey switch
            {
                InstrumentTranslationKeys.OrderTestCode or InstrumentTranslationKeys.ResultTestCode => tests.Contains(local),
                InstrumentTranslationKeys.SubtestCode => subtests.Contains(local),
                _ => true
            };
            if (!known)
            {
                var what = row.DataItemKey == InstrumentTranslationKeys.SubtestCode ? "subtest, phase, or test" : "test";
                results.Add(RuleResult.HardStop("INST-XLAT-LOCAL",
                    $"'{local}' is not an active {what} in the LIS ({InstrumentTranslationKeys.Find(row.DataItemKey)?.DisplayName})."));
            }

            if (!string.IsNullOrWhiteSpace(row.Context) && !tests.Contains(row.Context.Trim()))
            {
                results.Add(RuleResult.HardStop("INST-XLAT-CONTEXT", $"Test limit '{row.Context.Trim()}' is not an active test."));
            }
        }

        return results;
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
        await _interfaces.ReplaceChildrenAsync(entity, children.Connection, children.Fields, children.Translations, children.Rules, ct);
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
        await _interfaces.ReplaceChildrenAsync(entity, children.Connection, children.Fields, children.Translations, children.Rules, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Interface,
            oldValue: before, newValue: Map(entity, children), reason: req.ChangeReason);
        if (_unmapped is not null)
        {
            await _unmapped.ResolveInstrumentCoveredAsync(entity.Id, children.Translations, ct);
        }

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
                entity.Translations.ToList(),
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
            var evaluation = InstrumentInterfaceValidator.Validate(
                entity, duplicateName, duplicatePort, children.Fields, children.Translations, children.Rules);
            var local = await ValidateLocalValuesAsync(children.Translations, ct);
            return local.Count == 0 ? evaluation : new RuleEvaluation(evaluation.Results.Concat(local).ToList());
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
        entity.InterpretationMatch = req.InterpretationMatch;
        entity.InterpretationAnalyteCodes = string.IsNullOrWhiteSpace(req.InterpretationAnalyteCodes)
            ? null
            : req.InterpretationAnalyteCodes.Trim();
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
        var translations = (req.Translations ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.InstrumentValue) || !string.IsNullOrWhiteSpace(t.LocalValue))
            .Select(t => new InstrumentValueTranslation
            {
                DataItemKey = t.DataItemKey?.Trim() ?? string.Empty,
                InstrumentValue = t.InstrumentValue?.Trim() ?? string.Empty,
                LocalValue = t.LocalValue?.Trim() ?? string.Empty,
                Context = string.IsNullOrWhiteSpace(t.Context) ? null : t.Context.Trim(),
                // Order test codes only go to the instrument; every other field only comes from it.
                Direction = t.DataItemKey?.Trim() == InstrumentTranslationKeys.OrderTestCode
                    ? InterfaceTranslationDirection.Outbound
                    : InterfaceTranslationDirection.Inbound,
                SortOrder = t.SortOrder,
                Description = string.IsNullOrWhiteSpace(t.Description) ? null : t.Description.Trim()
            })
            .GroupBy(t => (t.DataItemKey, Context: t.Context?.ToUpperInvariant(), Instrument: t.InstrumentValue.ToUpperInvariant(), Local: t.LocalValue.ToUpperInvariant()))
            .Select(g => g.First())
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
        return new ChildSet(connection, fields, translations, rules);
    }

    private static InstrumentInterfaceDto Map(InstrumentInterface entity) =>
        Map(entity, new ChildSet(
            entity.Connection ?? new InstrumentConnection { Port = 0 },
            entity.FieldMappings.ToList(),
            entity.Translations.ToList(),
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
            entity.InterpretationMatch,
            entity.InterpretationAnalyteCodes,
            new InstrumentConnectionDto(children.Connection.IpAddress, children.Connection.Port, children.Connection.IsServer),
            children.Fields.Select(m =>
            {
                catalog.TryGetValue(m.DataItemKey, out var item);
                return new InstrumentFieldMappingDto(m.DataItemKey, m.AstmPath, m.IsRequired || (item?.Required ?? false), item?.DisplayName);
            }).ToList(),
            children.Translations
                .OrderBy(t => t.DataItemKey).ThenBy(t => t.SortOrder).ThenBy(t => t.InstrumentValue)
                .Select(t => new InstrumentTranslationDto(t.DataItemKey, t.InstrumentValue, t.LocalValue, t.Direction, t.SortOrder, t.Context, t.Description))
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
        List<InstrumentValueTranslation> Translations,
        List<InstrumentInterfaceRule> Rules);
}
