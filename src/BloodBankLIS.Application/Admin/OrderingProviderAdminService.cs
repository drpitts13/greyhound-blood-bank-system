using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class OrderingProviderAdminService : ConfigAdminServiceBase
{
    private readonly IRepository<OrderingProvider> _providers;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public OrderingProviderAdminService(
        IRepository<OrderingProvider> providers,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _providers = providers;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<OrderingProviderDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _providers.ListAsync(ct)
            : await _providers.ListAsync(p => p.IsActive, ct);
        return list.OrderBy(p => p.Name).Select(OrderingProviderDto.From).ToList();
    }

    public async Task<OrderingProviderDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _providers.GetByIdAsync(id, ct);
        return entity is null ? null : OrderingProviderDto.From(entity);
    }

    public async Task<EvaluationResult<OrderingProviderDto>> CreateAsync(SaveOrderingProviderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, OrderingProviderAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var providerId = request.ProviderId.Trim();
        if (await _providers.AnyAsync(p => p.ProviderId == providerId, ct))
        {
            return EvaluationResult<OrderingProviderDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop("PROVIDER.ID.DUPLICATE", $"Provider id '{providerId}' already exists.")]));
        }

        var entity = new OrderingProvider
        {
            ProviderId = providerId,
            Name = request.Name.Trim(),
            Specialty = NullIfEmpty(request.Specialty),
            Location = NullIfEmpty(request.Location),
            IsActive = true,
            SourceSystem = "Manual"
        };

        var validation = OrderingProviderValidator.Validate(entity, duplicateProviderId: false);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<OrderingProviderDto>.Blocked(validation);
        }

        await _providers.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = OrderingProviderDto.From(entity);
        RecordChange("OrderingProvider", entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OrderingProviderDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<OrderingProviderDto>> UpdateAsync(long id, SaveOrderingProviderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, OrderingProviderAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _providers.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<OrderingProviderDto>.Fail("Provider not found.");
        }

        var providerId = request.ProviderId.Trim();
        var duplicate = await _providers.AnyAsync(p => p.ProviderId == providerId && p.Id != id, ct);
        var old = OrderingProviderDto.From(entity);

        entity.ProviderId = providerId;
        entity.Name = request.Name.Trim();
        entity.Specialty = NullIfEmpty(request.Specialty);
        entity.Location = NullIfEmpty(request.Location);

        var validation = OrderingProviderValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<OrderingProviderDto>.Blocked(validation);
        }

        _providers.Update(entity);
        RecordChange("OrderingProvider", entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, OrderingProviderDto.From(entity), null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OrderingProviderDto>.Ok(OrderingProviderDto.From(entity), validation);
    }

    public async Task<EvaluationResult<OrderingProviderDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active ? OrderingProviderAuthorizationRule.EvaluateActivate : OrderingProviderAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _providers.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<OrderingProviderDto>.Fail("Provider not found.");
        }

        var old = OrderingProviderDto.From(entity);
        entity.IsActive = active;
        _providers.Update(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange("OrderingProvider", entity.Id, 1, action, ToAuditType(action), old, OrderingProviderDto.From(entity), null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OrderingProviderDto>.Ok(OrderingProviderDto.From(entity), new RuleEvaluation([]));
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<EvaluationResult<OrderingProviderDto>?> RejectUnauthorizedAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(
            CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<OrderingProviderDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
