using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record TransfusionIndicationCriterionDto(
    long Id,
    string Code,
    string Name,
    ComponentClass ComponentClass,
    string? LabTestCode,
    IndicationComparison? Comparison,
    decimal? Threshold,
    string? Unit,
    int LookbackHours,
    int SortOrder,
    bool IsActive)
{
    public static TransfusionIndicationCriterionDto From(TransfusionIndicationCriterion c) => new(
        c.Id, c.Code, c.Name, c.ComponentClass, c.LabTestCode, c.Comparison, c.Threshold, c.Unit, c.LookbackHours, c.SortOrder, c.IsActive);
}

public sealed record SaveTransfusionIndicationCriterionRequest(
    string Code,
    string Name,
    ComponentClass ComponentClass,
    string? LabTestCode,
    IndicationComparison? Comparison,
    decimal? Threshold,
    string? Unit,
    int LookbackHours,
    int SortOrder);

/// <summary>
/// Facility catalog of transfusion indications (OCD-038). Nothing is seeded; each row is a
/// facility decision, versioned through configuration history.
/// </summary>
public sealed class TransfusionIndicationAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(TransfusionIndicationCriterion);

    private readonly IRepository<TransfusionIndicationCriterion> _criteria;

    public TransfusionIndicationAdminService(
        IRepository<TransfusionIndicationCriterion> criteria,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _criteria = criteria;
    }

    public async Task<IReadOnlyList<TransfusionIndicationCriterionDto>> ListAsync(CancellationToken ct = default) =>
        (await _criteria.ListAsync(ct))
            .OrderBy(c => c.ComponentClass).ThenBy(c => c.SortOrder).ThenBy(c => c.Code)
            .Select(TransfusionIndicationCriterionDto.From)
            .ToList();

    public async Task<EvaluationResult<TransfusionIndicationCriterionDto>> CreateAsync(
        SaveTransfusionIndicationCriterionRequest request, CancellationToken ct = default)
    {
        var entity = new TransfusionIndicationCriterion { IsActive = true };
        Apply(entity, request);
        var validation = TransfusionIndicationRule.Validate(entity, await _criteria.AnyAsync(c => c.Code == entity.Code, ct));
        if (validation.IsHardStopped)
        {
            return EvaluationResult<TransfusionIndicationCriterionDto>.Blocked(validation);
        }

        await _criteria.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = TransfusionIndicationCriterionDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransfusionIndicationCriterionDto>.Ok(dto);
    }

    public async Task<EvaluationResult<TransfusionIndicationCriterionDto>> UpdateAsync(
        long id, SaveTransfusionIndicationCriterionRequest request, CancellationToken ct = default)
    {
        var entity = await _criteria.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<TransfusionIndicationCriterionDto>.Fail("Criterion not found.");
        }

        var old = TransfusionIndicationCriterionDto.From(entity);
        Apply(entity, request);
        var validation = TransfusionIndicationRule.Validate(entity, await _criteria.AnyAsync(c => c.Id != id && c.Code == entity.Code, ct));
        if (validation.IsHardStopped)
        {
            return EvaluationResult<TransfusionIndicationCriterionDto>.Blocked(validation);
        }

        _criteria.Update(entity);
        var dto = TransfusionIndicationCriterionDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransfusionIndicationCriterionDto>.Ok(dto);
    }

    public async Task<EvaluationResult<TransfusionIndicationCriterionDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var entity = await _criteria.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<TransfusionIndicationCriterionDto>.Fail("Criterion not found.");
        }

        var old = TransfusionIndicationCriterionDto.From(entity);
        entity.IsActive = active;
        _criteria.Update(entity);
        var dto = TransfusionIndicationCriterionDto.From(entity);
        RecordChange(EntityType, entity.Id, 1,
            active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate,
            active ? AuditEventType.Activate : AuditEventType.Deactivate, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransfusionIndicationCriterionDto>.Ok(dto);
    }

    private static void Apply(TransfusionIndicationCriterion entity, SaveTransfusionIndicationCriterionRequest r)
    {
        entity.Code = (r.Code ?? string.Empty).Trim().ToUpperInvariant();
        entity.Name = r.Name?.Trim() ?? string.Empty;
        entity.ComponentClass = r.ComponentClass;
        entity.LabTestCode = string.IsNullOrWhiteSpace(r.LabTestCode) ? null : r.LabTestCode.Trim().ToUpperInvariant();
        entity.Comparison = r.Comparison;
        entity.Threshold = r.Threshold;
        entity.Unit = string.IsNullOrWhiteSpace(r.Unit) ? null : r.Unit.Trim();
        entity.LookbackHours = r.LookbackHours <= 0 ? 24 : r.LookbackHours;
        entity.SortOrder = r.SortOrder;
    }
}
