using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record ExceptionDefinitionDto(
    long Id,
    string RuleCode,
    string Name,
    string? Description,
    int MinSecurityLevel,
    bool IsOverridable,
    bool IsActive)
{
    public static ExceptionDefinitionDto From(ExceptionDefinition e) => new(
        e.Id, e.RuleCode, e.Name, e.Description, e.MinSecurityLevel, e.IsOverridable, e.IsActive);
}

public sealed record SaveExceptionDefinitionRequest(
    string RuleCode,
    string Name,
    string? Description,
    int MinSecurityLevel,
    bool IsOverridable);

public sealed class ExceptionDefinitionAdminService : ConfigAdminServiceBase
{
    private readonly IRepository<ExceptionDefinition> _exceptions;

    public ExceptionDefinitionAdminService(
        IRepository<ExceptionDefinition> exceptions,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _exceptions = exceptions;
    }

    public async Task<IReadOnlyList<ExceptionDefinitionDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _exceptions.ListAsync(ct)
            : await _exceptions.ListAsync(e => e.IsActive, ct);
        return list.OrderBy(e => e.RuleCode).Select(ExceptionDefinitionDto.From).ToList();
    }

    public async Task<ExceptionDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _exceptions.GetByIdAsync(id, ct);
        return entity is null ? null : ExceptionDefinitionDto.From(entity);
    }

    public async Task<ExceptionDefinitionDto?> GetByRuleCodeAsync(string ruleCode, CancellationToken ct = default)
    {
        var entity = await _exceptions.FirstOrDefaultAsync(e => e.RuleCode == ruleCode && e.IsActive, ct);
        return entity is null ? null : ExceptionDefinitionDto.From(entity);
    }

    public async Task<EvaluationResult<ExceptionDefinitionDto>> CreateAsync(SaveExceptionDefinitionRequest request, CancellationToken ct = default)
    {
        var ruleCode = request.RuleCode.Trim().ToUpperInvariant();
        if (await _exceptions.AnyAsync(e => e.RuleCode == ruleCode, ct))
        {
            return EvaluationResult<ExceptionDefinitionDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop("EXC.CODE.DUPLICATE", $"Exception rule code '{ruleCode}' already exists.")]));
        }

        var validation = Validate(request, ruleCode);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ExceptionDefinitionDto>.Blocked(validation);
        }

        var entity = new ExceptionDefinition
        {
            RuleCode = ruleCode,
            Name = request.Name.Trim(),
            Description = NullIfEmpty(request.Description),
            MinSecurityLevel = request.MinSecurityLevel,
            IsOverridable = request.IsOverridable,
            IsActive = true
        };

        await _exceptions.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = ExceptionDefinitionDto.From(entity);
        RecordChange("ExceptionDefinition", entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Create, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ExceptionDefinitionDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ExceptionDefinitionDto>> UpdateAsync(long id, SaveExceptionDefinitionRequest request, CancellationToken ct = default)
    {
        var entity = await _exceptions.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ExceptionDefinitionDto>.Fail("Exception definition not found.");
        }

        var ruleCode = request.RuleCode.Trim().ToUpperInvariant();
        if (await _exceptions.AnyAsync(e => e.RuleCode == ruleCode && e.Id != id, ct))
        {
            return EvaluationResult<ExceptionDefinitionDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop("EXC.CODE.DUPLICATE", $"Exception rule code '{ruleCode}' already exists.")]));
        }

        var validation = Validate(request, ruleCode);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ExceptionDefinitionDto>.Blocked(validation);
        }

        var old = ExceptionDefinitionDto.From(entity);
        entity.RuleCode = ruleCode;
        entity.Name = request.Name.Trim();
        entity.Description = NullIfEmpty(request.Description);
        entity.MinSecurityLevel = request.MinSecurityLevel;
        entity.IsOverridable = request.IsOverridable;
        _exceptions.Update(entity);

        RecordChange("ExceptionDefinition", entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Update, old, ExceptionDefinitionDto.From(entity), null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ExceptionDefinitionDto>.Ok(ExceptionDefinitionDto.From(entity), validation);
    }

    public async Task<EvaluationResult<ExceptionDefinitionDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var entity = await _exceptions.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ExceptionDefinitionDto>.Fail("Exception definition not found.");
        }

        var old = ExceptionDefinitionDto.From(entity);
        entity.IsActive = active;
        _exceptions.Update(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange("ExceptionDefinition", entity.Id, 1, action, ToAuditType(action), old, ExceptionDefinitionDto.From(entity), null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ExceptionDefinitionDto>.Ok(ExceptionDefinitionDto.From(entity), new RuleEvaluation([]));
    }

    private static RuleEvaluation Validate(SaveExceptionDefinitionRequest request, string ruleCode)
    {
        var results = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(ruleCode))
        {
            results.Add(RuleResult.HardStop("EXC.CODE.REQUIRED", "Rule code is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            results.Add(RuleResult.HardStop("EXC.NAME.REQUIRED", "Name is required."));
        }

        if (request.MinSecurityLevel < 0)
        {
            results.Add(RuleResult.HardStop("EXC.LEVEL.INVALID", "Minimum security level cannot be negative."));
        }

        return new RuleEvaluation(results);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
