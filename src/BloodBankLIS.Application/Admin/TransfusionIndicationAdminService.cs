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

/// <summary>Result of loading the draft set: codes added (inactive) and codes skipped because they already exist.</summary>
public sealed record LoadDraftCriteriaResultDto(IReadOnlyList<string> Added, IReadOnlyList<string> Skipped);

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
        if (active)
        {
            var validation = TransfusionIndicationRule.Validate(entity, duplicateCode: false);
            if (validation.IsHardStopped)
            {
                entity.IsActive = false;
                return EvaluationResult<TransfusionIndicationCriterionDto>.Blocked(validation);
            }
        }

        _criteria.Update(entity);
        var dto = TransfusionIndicationCriterionDto.From(entity);
        RecordChange(EntityType, entity.Id, 1,
            active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate,
            active ? AuditEventType.Activate : AuditEventType.Deactivate, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransfusionIndicationCriterionDto>.Ok(dto);
    }

    /// <summary>
    /// OCD-038: adds the unverified draft set INACTIVE for committee review. A code that already exists is skipped,
    /// never overwritten. Each added row is recorded in configuration history.
    /// </summary>
    public async Task<LoadDraftCriteriaResultDto> LoadDraftsAsync(CancellationToken ct = default)
    {
        var existing = (await _criteria.ListAsync(ct)).Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<TransfusionIndicationCriterion>();
        var skipped = new List<string>();
        foreach (var d in PbmDraftCriteria.All)
        {
            if (existing.Contains(d.Code))
            {
                skipped.Add(d.Code);
                continue;
            }

            var entity = new TransfusionIndicationCriterion
            {
                Code = d.Code,
                Name = d.Name,
                ComponentClass = d.ComponentClass,
                LabTestCode = d.LabTestCode,
                Comparison = d.Comparison,
                Threshold = d.Threshold,
                Unit = d.Unit,
                LookbackHours = d.LookbackHours,
                SortOrder = d.SortOrder,
                IsActive = false
            };
            await _criteria.AddAsync(entity, ct);
            added.Add(entity);
        }

        await UnitOfWork.SaveChangesAsync(ct);
        foreach (var entity in added)
        {
            RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null,
                TransfusionIndicationCriterionDto.From(entity),
                "Unverified draft loaded inactive for transfusion committee review (OCD-038).");
        }

        await UnitOfWork.SaveChangesAsync(ct);
        return new LoadDraftCriteriaResultDto(added.Select(a => a.Code).ToList(), skipped);
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
