using BloodBankLIS.Application.Abstractions;

using BloodBankLIS.Application.Common;

using BloodBankLIS.Domain.Entities.Configuration;

using BloodBankLIS.Domain.Enums;

using BloodBankLIS.Domain.Rules;

using BloodBankLIS.Domain.Rules.Config;

using BloodBankLIS.Domain.ValueObjects;



namespace BloodBankLIS.Application.Admin;



/// <summary>

/// Admin management of <see cref="TestDefinition"/>s. New records are drafts; activation is

/// validation-gated; significant edits bump the version and require a change reason. Every

/// action writes an audit event and a configuration-history snapshot.

/// </summary>

public sealed class TestDefinitionAdminService : ConfigAdminServiceBase

{

    private const string EntityType = nameof(TestDefinition);



    private readonly IRepository<TestDefinition> _repo;

    private readonly IRepository<SubtestDefinition> _subtestRepo;

    private readonly IRepository<BloodAttributeDefinition> _bloodAttrRepo;

    private readonly IRepository<SpecimenTypeDefinition> _specimenTypeRepo;



    public TestDefinitionAdminService(

        IRepository<TestDefinition> repo,

        IRepository<SubtestDefinition> subtestRepo,

        IRepository<BloodAttributeDefinition> bloodAttrRepo,

        IRepository<SpecimenTypeDefinition> specimenTypeRepo,

        IUnitOfWork unitOfWork,

        IClock clock,

        ICurrentUser currentUser,

        IAuditWriter audit,

        IConfigurationHistoryWriter history)

        : base(unitOfWork, clock, currentUser, audit, history)

    {

        _repo = repo;

        _subtestRepo = subtestRepo;

        _bloodAttrRepo = bloodAttrRepo;

        _specimenTypeRepo = specimenTypeRepo;

    }



    public async Task<IReadOnlyList<TestDefinitionDto>> ListAsync(bool includeInactive, CancellationToken ct = default)

    {

        var items = includeInactive

            ? await _repo.ListAsync(ct)

            : await _repo.ListAsync(t => t.IsActive, ct);

        return items.OrderBy(t => t.SortOrder).ThenBy(t => t.Code).Select(Map).ToList();

    }



    public async Task<TestDefinitionDto?> GetAsync(long id, CancellationToken ct = default)

    {

        var item = await _repo.GetByIdAsync(id, ct);

        return item is null ? null : Map(item);

    }



    public async Task<EvaluationResult<TestDefinitionDto>> CreateAsync(SaveTestDefinitionRequest req, CancellationToken ct = default)

    {

        ArgumentNullException.ThrowIfNull(req);



        var entity = new TestDefinition { IsDraft = true, IsActive = false, Version = 1 };

        Apply(entity, req);



        var duplicate = await HasActiveDuplicateAsync(entity.Code, 0, ct);

        var activeSubtests = await LoadActiveSubtestCodesAsync(ct);

        var activeBloodAttrs = await LoadActiveBloodAttributeCodesAsync(ct);

        var activeSpecimenTypes = await LoadActiveSpecimenTypeCodesAsync(ct);

        var evaluation = TestDefinitionValidator.Validate(entity, duplicate, activeSubtests, activeBloodAttrs, activeSpecimenTypes);

        if (evaluation.IsHardStopped)

        {

            return EvaluationResult<TestDefinitionDto>.Blocked(evaluation);

        }



        await _repo.AddAsync(entity, ct);

        await UnitOfWork.SaveChangesAsync(ct);



        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,

            oldValue: null, newValue: Map(entity), reason: req.ChangeReason);

        await UnitOfWork.SaveChangesAsync(ct);



        return EvaluationResult<TestDefinitionDto>.Ok(Map(entity), evaluation);

    }



    public async Task<EvaluationResult<TestDefinitionDto>> UpdateAsync(long id, SaveTestDefinitionRequest req, CancellationToken ct = default)

    {

        ArgumentNullException.ThrowIfNull(req);



        var entity = await _repo.GetByIdAsync(id, ct);

        if (entity is null)

        {

            return EvaluationResult<TestDefinitionDto>.Fail("Test definition not found.");

        }



        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))

        {

            return EvaluationResult<TestDefinitionDto>.Fail("A change reason is required to edit an active test definition.");

        }



        var before = Map(entity);

        Apply(entity, req);

        if (entity.IsActive)

        {

            entity.Version += 1;

        }



        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);

        var activeSubtests = await LoadActiveSubtestCodesAsync(ct);

        var activeBloodAttrs = await LoadActiveBloodAttributeCodesAsync(ct);

        var activeSpecimenTypes = await LoadActiveSpecimenTypeCodesAsync(ct);

        var evaluation = TestDefinitionValidator.Validate(entity, duplicate, activeSubtests, activeBloodAttrs, activeSpecimenTypes);

        if (evaluation.IsHardStopped)

        {

            return EvaluationResult<TestDefinitionDto>.Blocked(evaluation);

        }



        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,

            oldValue: before, newValue: Map(entity), reason: req.ChangeReason);

        await UnitOfWork.SaveChangesAsync(ct);



        return EvaluationResult<TestDefinitionDto>.Ok(Map(entity), evaluation);

    }



    public async Task<EvaluationResult<TestDefinitionDto>> ActivateAsync(long id, string? reason, CancellationToken ct = default)

    {

        var entity = await _repo.GetByIdAsync(id, ct);

        if (entity is null)

        {

            return EvaluationResult<TestDefinitionDto>.Fail("Test definition not found.");

        }



        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);

        var activeSubtests = await LoadActiveSubtestCodesAsync(ct);

        var activeBloodAttrs = await LoadActiveBloodAttributeCodesAsync(ct);

        var activeSpecimenTypes = await LoadActiveSpecimenTypeCodesAsync(ct);

        var evaluation = TestDefinitionValidator.Validate(entity, duplicate, activeSubtests, activeBloodAttrs, activeSpecimenTypes);

        if (evaluation.IsHardStopped)

        {

            return EvaluationResult<TestDefinitionDto>.Blocked(evaluation);

        }



        entity.IsActive = true;

        entity.IsDraft = false;

        entity.RetiredUtc = null;

        entity.EffectiveUtc ??= Clock.UtcNow;

        entity.ChangeReason = reason;

        _repo.Update(entity);



        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Activate, AuditEventType.Activate,

            oldValue: null, newValue: Map(entity), reason: reason);

        await UnitOfWork.SaveChangesAsync(ct);



        return EvaluationResult<TestDefinitionDto>.Ok(Map(entity), evaluation);

    }



    public async Task<OperationResult<TestDefinitionDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)

    {

        var entity = await _repo.GetByIdAsync(id, ct);

        if (entity is null)

        {

            return OperationResult<TestDefinitionDto>.Fail("Test definition not found.");

        }



        entity.IsActive = false;

        entity.RetiredUtc = Clock.UtcNow;

        entity.ChangeReason = reason;

        _repo.Update(entity);



        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,

            oldValue: null, newValue: Map(entity), reason: reason);

        await UnitOfWork.SaveChangesAsync(ct);



        return OperationResult<TestDefinitionDto>.Ok(Map(entity));

    }



    public async Task<OperationResult<TestDefinitionDto>> CloneAsync(long id, string newCode, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(newCode))

        {

            return OperationResult<TestDefinitionDto>.Fail("A new code is required to clone.");

        }



        var source = await _repo.GetByIdAsync(id, ct);

        if (source is null)

        {

            return OperationResult<TestDefinitionDto>.Fail("Test definition not found.");

        }



        var clone = new TestDefinition

        {

            Code = newCode.Trim().ToUpperInvariant(),

            Name = source.Name + " (copy)",

            Category = source.Category,

            ResultValueType = source.ResultValueType,

            AllowedResultValues = source.AllowedResultValues,

            RequiredSpecimenType = source.RequiredSpecimenType,

            TestingMethod = source.TestingMethod,

            PerformingDepartment = source.PerformingDepartment,

            SortOrder = source.SortOrder,

            Billable = source.Billable,

            ChargeCodeMapping = source.ChargeCodeMapping,

            VerificationRequired = source.VerificationRequired,

            ContributesToAboRhHistory = source.ContributesToAboRhHistory,

            ContributesToAntibodyHistory = source.ContributesToAntibodyHistory,

            ContributesToCompatibility = source.ContributesToCompatibility,

            BloodAttributeScopeJson = source.BloodAttributeScopeJson,

            BloodAttributeScopeKind = source.BloodAttributeScopeKind,

            ContributesToUnitBloodAttributes = source.ContributesToUnitBloodAttributes,

            PanelSubtestsJson = source.PanelSubtestsJson,

            InterpretationLogicJson = source.InterpretationLogicJson,

            IsDraft = true,

            IsActive = false,

            Version = 1

        };



        await _repo.AddAsync(clone, ct);

        await UnitOfWork.SaveChangesAsync(ct);



        RecordChange(EntityType, clone.Id, clone.Version, ConfigChangeAction.Clone, AuditEventType.Clone,

            oldValue: new { SourceId = source.Id, source.Code }, newValue: Map(clone), reason: $"Cloned from {source.Code}");

        await UnitOfWork.SaveChangesAsync(ct);



        return OperationResult<TestDefinitionDto>.Ok(Map(clone));

    }



    private async Task<HashSet<string>> LoadActiveSubtestCodesAsync(CancellationToken ct)

    {

        var subtests = await _subtestRepo.ListAsync(s => s.IsActive && !s.IsDraft, ct);

        return subtests.Select(s => s.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

    }



    private async Task<HashSet<string>> LoadActiveBloodAttributeCodesAsync(CancellationToken ct)

    {

        var attrs = await _bloodAttrRepo.ListAsync(d => d.IsActive && !d.IsDraft, ct);

        return attrs.Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

    }



    private async Task<HashSet<string>> LoadActiveSpecimenTypeCodesAsync(CancellationToken ct)

    {

        var types = await _specimenTypeRepo.ListAsync(t => t.IsActive && !t.IsDraft, ct);

        return types.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

    }



    private async Task<bool> HasActiveDuplicateAsync(string code, long selfId, CancellationToken ct)

    {

        if (string.IsNullOrWhiteSpace(code))

        {

            return false;

        }



        var normalized = code.Trim().ToUpperInvariant();

        return await _repo.AnyAsync(t => t.IsActive && t.Id != selfId && t.Code == normalized, ct);

    }



    private void Apply(TestDefinition e, SaveTestDefinitionRequest req)

    {

        e.Code = (req.Code ?? string.Empty).Trim().ToUpperInvariant();

        e.Name = req.Name?.Trim() ?? string.Empty;

        e.Category = req.Category;

        e.ResultValueType = req.ResultValueType;

        e.AllowedResultValues = string.IsNullOrWhiteSpace(req.AllowedResultValues) ? null : req.AllowedResultValues.Trim();

        e.PanelSubtestsJson = TestDefinitionValidator.UsesPanelSubtests(req.ResultValueType)

            ? PanelSubtestAssignments.ToJson(MapAssignments(req.PanelSubtestAssignments))

            : null;

        e.InterpretationLogicJson = TestDefinitionValidator.UsesPanelSubtests(req.ResultValueType)

            ? InterpretationLogicDefinitions.ToJson(MapLogic(req.InterpretationLogic))

            : null;

        e.RequiredSpecimenType = string.IsNullOrWhiteSpace(req.RequiredSpecimenType)
            ? null
            : req.RequiredSpecimenType.Trim().ToUpperInvariant();

        e.TestingMethod = req.TestingMethod?.Trim();

        e.PerformingDepartment = req.PerformingDepartment?.Trim();

        e.SortOrder = req.SortOrder;

        e.Billable = req.Billable;

        e.ChargeCodeMapping = req.ChargeCodeMapping?.Trim();

        e.VerificationRequired = req.VerificationRequired;

        e.ContributesToAboRhHistory = req.ContributesToAboRhHistory;

        e.ContributesToAntibodyHistory = req.ContributesToAntibodyHistory;

        e.ContributesToCompatibility = req.ContributesToCompatibility;

        e.BloodAttributeScopeJson = req.ResultValueType == ResultValueType.BloodAttribute

            ? BloodAttributeScope.Serialize((req.BloodAttributeScopeCodes ?? []).Select(c => new BloodAttributeScopeEntry(c)))

            : null;

        e.BloodAttributeScopeKind = req.ResultValueType == ResultValueType.BloodAttribute

            ? req.BloodAttributeScopeKind

            : null;

        e.ContributesToUnitBloodAttributes = req.ContributesToUnitBloodAttributes;

        e.ChangeReason = req.ChangeReason;

    }



    private static TestDefinitionDto Map(TestDefinition t) => new(

        t.Id, t.Code, t.Name, t.Category, t.ResultValueType, t.AllowedResultValues,

        MapAssignmentsDto(t.PanelSubtestsJson),

        MapLogicDto(t.InterpretationLogicJson),

        t.RequiredSpecimenType, t.TestingMethod, t.PerformingDepartment, t.SortOrder, t.Billable, t.ChargeCodeMapping,

        t.VerificationRequired, t.ContributesToAboRhHistory, t.ContributesToAntibodyHistory, t.ContributesToCompatibility,

        BloodAttributeScope.Parse(t.BloodAttributeScopeJson).Select(s => s.Code).ToList(),

        t.BloodAttributeScopeKind,

        t.ContributesToUnitBloodAttributes,

        t.Version, t.IsActive, t.IsDraft, t.EffectiveUtc, t.RetiredUtc, t.ChangeReason);



    private static IReadOnlyList<PanelSubtestAssignment> MapAssignments(IReadOnlyList<PanelSubtestAssignmentDto>? items) =>

        items?.Select(s => new PanelSubtestAssignment(s.SubtestCode.Trim(), s.Required, s.SortOrder)).ToList() ?? [];



    private static IReadOnlyList<PanelSubtestAssignmentDto> MapAssignmentsDto(string? json) =>

        PanelSubtestAssignments.Parse(json)

            .Select(s => new PanelSubtestAssignmentDto(s.SubtestCode, s.Required, s.SortOrder))

            .ToList();



    private static IReadOnlyList<InterpretationLogicRow> MapLogic(IReadOnlyList<InterpretationLogicRowDto>? items) =>

        items?.Select(r => new InterpretationLogicRow(

            r.InterpretationKey.Trim(),

            r.Label.Trim(),

            r.SubtestExpectations))

            .ToList() ?? [];



    private static IReadOnlyList<InterpretationLogicRowDto> MapLogicDto(string? json) =>

        InterpretationLogicDefinitions.Parse(json)

            .Select(r => new InterpretationLogicRowDto(r.InterpretationKey, r.Label, r.SubtestExpectations))

            .ToList();

}


