using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record MtpPackDefinitionDto(
    long Id,
    string Code,
    string Name,
    int RedCellUnits,
    int PlasmaUnits,
    int PlateletUnits,
    int CryoUnits,
    int WholeBloodUnits,
    int SortOrder,
    bool IsActive)
{
    public static MtpPackDefinitionDto From(MtpPackDefinition p) => new(
        p.Id, p.Code, p.Name, p.RedCellUnits, p.PlasmaUnits, p.PlateletUnits, p.CryoUnits, p.WholeBloodUnits, p.SortOrder, p.IsActive);

    /// <summary>For example "4 red cells, 4 plasma, 1 platelets".</summary>
    public string Summary =>
        string.Join(", ", new[]
            {
                (RedCellUnits, "red cells"), (PlasmaUnits, "plasma"), (PlateletUnits, "platelets"),
                (CryoUnits, "cryo"), (WholeBloodUnits, "whole blood")
            }
            .Where(q => q.Item1 > 0)
            .Select(q => $"{q.Item1} {q.Item2}"));
}

public sealed record SaveMtpPackRequest(
    string Code,
    string Name,
    int RedCellUnits,
    int PlasmaUnits,
    int PlateletUnits,
    int CryoUnits,
    int WholeBloodUnits,
    int SortOrder,
    // Why an existing entry is changed; required for an update.
    string? Reason = null);

/// <summary>
/// Facility catalog of MTP packs (OCD-012). Nothing is seeded: each pack is a medical-director decision, versioned
/// through configuration history.
/// </summary>
public sealed class MtpPackAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(MtpPackDefinition);

    private readonly IRepository<MtpPackDefinition> _packs;

    public MtpPackAdminService(
        IRepository<MtpPackDefinition> packs,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _packs = packs;
    }

    public async Task<IReadOnlyList<MtpPackDefinitionDto>> ListAsync(CancellationToken ct = default) =>
        (await _packs.ListAsync(ct))
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Code)
            .Select(MtpPackDefinitionDto.From)
            .ToList();

    public async Task<IReadOnlyList<MtpPackDefinitionDto>> ListActiveAsync(CancellationToken ct = default) =>
        (await ListAsync(ct)).Where(p => p.IsActive).ToList();

    public async Task<EvaluationResult<MtpPackDefinitionDto>> CreateAsync(SaveMtpPackRequest request, CancellationToken ct = default)
    {
        var entity = new MtpPackDefinition { IsActive = true };
        Apply(entity, request);
        var validation = MtpPackRule.Validate(entity, await _packs.AnyAsync(p => p.Code == entity.Code, ct));
        if (validation.IsHardStopped)
        {
            return EvaluationResult<MtpPackDefinitionDto>.Blocked(validation);
        }

        await _packs.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = MtpPackDefinitionDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<MtpPackDefinitionDto>.Ok(dto);
    }

    public async Task<EvaluationResult<MtpPackDefinitionDto>> UpdateAsync(long id, SaveMtpPackRequest request, CancellationToken ct = default)
    {
        var why = ValidatedConfigChangeReasonRule.Evaluate(request.Reason, "an MTP pack");
        if (why.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<MtpPackDefinitionDto>.Blocked(new RuleEvaluation([why]));
        }

        var entity = await _packs.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<MtpPackDefinitionDto>.Fail("Pack not found.");
        }

        var old = MtpPackDefinitionDto.From(entity);
        Apply(entity, request);
        var validation = MtpPackRule.Validate(entity, await _packs.AnyAsync(p => p.Id != id && p.Code == entity.Code, ct));
        if (validation.IsHardStopped)
        {
            return EvaluationResult<MtpPackDefinitionDto>.Blocked(validation);
        }

        _packs.Update(entity);
        var dto = MtpPackDefinitionDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, request.Reason!.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<MtpPackDefinitionDto>.Ok(dto);
    }

    public async Task<EvaluationResult<MtpPackDefinitionDto>> SetActiveAsync(long id, bool active, string? reason = null, CancellationToken ct = default)
    {
        var why = ValidatedConfigChangeReasonRule.Evaluate(reason, "an MTP pack");
        if (why.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<MtpPackDefinitionDto>.Blocked(new RuleEvaluation([why]));
        }

        var entity = await _packs.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<MtpPackDefinitionDto>.Fail("Pack not found.");
        }

        var old = MtpPackDefinitionDto.From(entity);
        entity.IsActive = active;
        _packs.Update(entity);
        var dto = MtpPackDefinitionDto.From(entity);
        RecordChange(EntityType, entity.Id, 1,
            active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate,
            active ? AuditEventType.Activate : AuditEventType.Deactivate, old, dto, reason!.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<MtpPackDefinitionDto>.Ok(dto);
    }

    private static void Apply(MtpPackDefinition entity, SaveMtpPackRequest r)
    {
        entity.Code = (r.Code ?? string.Empty).Trim().ToUpperInvariant();
        entity.Name = (r.Name ?? string.Empty).Trim();
        entity.RedCellUnits = r.RedCellUnits;
        entity.PlasmaUnits = r.PlasmaUnits;
        entity.PlateletUnits = r.PlateletUnits;
        entity.CryoUnits = r.CryoUnits;
        entity.WholeBloodUnits = r.WholeBloodUnits;
        entity.SortOrder = r.SortOrder;
    }
}
