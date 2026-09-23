using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin list of ISBT product description codes that can be thawed in place at allocation.
/// </summary>
public sealed class AutoThawProductAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(AutoThawProduct);

    private readonly IRepository<AutoThawProduct> _rows;
    private readonly IRepository<IsbtProductCode> _catalog;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public AutoThawProductAdminService(
        IRepository<AutoThawProduct> rows,
        IRepository<IsbtProductCode> catalog,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _rows = rows;
        _catalog = catalog;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<AutoThawProductDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var rows = includeInactive
            ? await _rows.ListAsync(ct)
            : await _rows.ListAsync(r => r.IsActive, ct);
        var catalog = await LoadCatalogAsync(ct);
        return rows
            .OrderBy(r => r.ProductCode)
            .Select(r => AutoThawProductDto.From(r, DescriptionFor(catalog, r.ProductCode)))
            .ToList();
    }

    public async Task<AutoThawProductDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        var catalog = await LoadCatalogAsync(ct);
        return AutoThawProductDto.From(entity, DescriptionFor(catalog, entity.ProductCode));
    }

    public async Task<EvaluationResult<AutoThawProductDto>> CreateAsync(
        SaveAutoThawProductRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, AutoThawProductAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var code = Normalize(request.ProductCode);
        var catalog = await FindCatalogAsync(code, ct);
        var entity = new AutoThawProduct
        {
            ProductCode = code,
            IsActive = true
        };
        var duplicate = await _rows.AnyAsync(r => r.ProductCode == code, ct);
        var validation = AutoThawProductValidator.Validate(
            entity,
            codeUnknown: catalog is null,
            codeRetired: IsRetired(catalog),
            duplicateCode: duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<AutoThawProductDto>.Blocked(validation);
        }

        await _rows.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = AutoThawProductDto.From(entity, catalog?.Description);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<AutoThawProductDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<AutoThawProductDto>> SetActiveAsync(
        long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active ? AutoThawProductAuthorizationRule.EvaluateActivate : AutoThawProductAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<AutoThawProductDto>.Fail("Auto-thaw product not found.");
        }

        var catalog = await FindCatalogAsync(entity.ProductCode, ct);
        if (active)
        {
            var validation = AutoThawProductValidator.Validate(
                entity,
                codeUnknown: catalog is null,
                codeRetired: IsRetired(catalog),
                duplicateCode: false);
            if (validation.IsHardStopped)
            {
                return EvaluationResult<AutoThawProductDto>.Blocked(validation);
            }
        }

        var old = AutoThawProductDto.From(entity, catalog?.Description);
        entity.IsActive = active;
        _rows.Update(entity);
        var dto = AutoThawProductDto.From(entity, catalog?.Description);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<AutoThawProductDto>.Ok(dto, new RuleEvaluation([]));
    }

    internal static string Normalize(string? code) =>
        string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();

    private async Task<IsbtProductCode?> FindCatalogAsync(string code, CancellationToken ct) =>
        await _catalog.FirstOrDefaultAsync(p => p.ProductDescriptionCode == code, ct);

    private async Task<Dictionary<string, IsbtProductCode>> LoadCatalogAsync(CancellationToken ct)
    {
        var rows = await _catalog.ListAsync(ct);
        return rows
            .GroupBy(p => p.ProductDescriptionCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static string? DescriptionFor(IReadOnlyDictionary<string, IsbtProductCode> catalog, string code) =>
        catalog.TryGetValue(code, out var row) ? row.Description : null;

    private static bool IsRetired(IsbtProductCode? code) =>
        code?.RetiredDate is DateOnly retired && retired <= DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<EvaluationResult<AutoThawProductDto>?> RejectUnauthorizedAsync(
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
            ? EvaluationResult<AutoThawProductDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
