using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Immunohematology;

/// <summary>
/// Antibody-identification workups. Assistance may propose exclusions and possible
/// specificities; it never posts Identified antibodies. History is written only
/// after technologist interpretation and, by default, supervisor acceptance.
/// </summary>
public sealed class AntibodyIdentificationService
{
    public static readonly string[] DefaultInterpretivePhases = ["IS", "37C", "AHG"];

    private readonly IRepository<AntibodyPanelManufacturer> _manufacturers;
    private readonly IRepository<AntibodyPanelLot> _lots;
    private readonly IRepository<AntibodyPanelCell> _cells;
    private readonly IRepository<AntibodyPanelCellAntigen> _cellAntigens;
    private readonly IRepository<AntibodyIdentificationWorkup> _workups;
    private readonly IRepository<AntibodyIdentificationWorkupLot> _workupLots;
    private readonly IRepository<AntibodyIdentificationReaction> _reactions;
    private readonly IRepository<AntibodyIdentificationFinding> _findings;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<AntigenProfile> _antigenProfiles;
    private readonly IRepository<BloodAttributeDefinition> _attributes;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<TestResult>? _results;
    private readonly IRepository<TestDefinition>? _testDefinitions;
    private readonly FacilityPolicyService _policies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public AntibodyIdentificationService(
        IRepository<AntibodyPanelManufacturer> manufacturers,
        IRepository<AntibodyPanelLot> lots,
        IRepository<AntibodyPanelCell> cells,
        IRepository<AntibodyPanelCellAntigen> cellAntigens,
        IRepository<AntibodyIdentificationWorkup> workups,
        IRepository<AntibodyIdentificationWorkupLot> workupLots,
        IRepository<AntibodyIdentificationReaction> reactions,
        IRepository<AntibodyIdentificationFinding> findings,
        IRepository<AntibodyHistory> antibodies,
        IRepository<AntigenProfile> antigenProfiles,
        IRepository<BloodAttributeDefinition> attributes,
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        FacilityPolicyService policies,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null,
        IRepository<TestResult>? results = null,
        IRepository<TestDefinition>? testDefinitions = null)
    {
        _manufacturers = manufacturers;
        _lots = lots;
        _cells = cells;
        _cellAntigens = cellAntigens;
        _workups = workups;
        _workupLots = workupLots;
        _reactions = reactions;
        _findings = findings;
        _antibodies = antibodies;
        _antigenProfiles = antigenProfiles;
        _attributes = attributes;
        _patients = patients;
        _specimens = specimens;
        _policies = policies;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
        _results = results;
        _testDefinitions = testDefinitions;
    }

    public async Task<IReadOnlyList<AntibodyPanelLotListItemDto>> ListLotsAsync(
        bool includeExpired = false,
        CancellationToken ct = default)
    {
        var today = Today();
        var lots = await _lots.ListAsync(ct);
        var manufacturers = (await _manufacturers.ListAsync(ct)).ToDictionary(m => m.Id);
        return lots
            .Where(l => includeExpired || (l.IsActive && l.ExpiresOn >= today))
            .OrderBy(l => l.IsSelectedCellLot)
            .ThenBy(l => l.PanelName)
            .ThenBy(l => l.LotNumber)
            .Select(l => ToLotDto(l, manufacturers.GetValueOrDefault(l.ManufacturerId)?.Name ?? "", today))
            .ToList();
    }

    public async Task<IReadOnlyList<AntibodyIdWorkupListItemDto>> ListWorkupsAsync(long patientId, CancellationToken ct = default)
    {
        var workups = await _workups.ListAsync(w => w.PatientId == patientId, ct);
        return await MapWorkupListAsync(workups, ct);
    }

    public async Task<IReadOnlyList<AntibodyIdWorkupListItemDto>> ListOpenWorkupsAsync(CancellationToken ct = default)
    {
        var workups = (await _workups.ListAsync(ct))
            .Where(w => AntibodyIdentificationHistoryPostRule.IsOpen(w.Status))
            .ToList();
        return await MapWorkupListAsync(workups, ct);
    }

    public async Task<AntibodyIdWorkupDetailDto?> GetWorkupAsync(long workupId, CancellationToken ct = default)
    {
        var workup = await _workups.GetByIdAsync(workupId, ct);
        return workup is null ? null : await MapDetailAsync(workup, ct);
    }

    public async Task<EvaluationResult<AntibodyIdWorkupDetailDto>> CreateWorkupAsync(
        long patientId,
        CreateAntibodyIdWorkupRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<AntibodyIdWorkupDetailDto>(patientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var specimenChecks = new List<RuleResult>();
        if (request.SpecimenId is long specimenId)
        {
            var specimen = await _specimens.GetByIdAsync(specimenId, ct);
            if (specimen is null || specimen.PatientId != patientId)
            {
                return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Specimen was not found for this patient.");
            }

            specimenChecks.AddRange(EvaluateSpecimenForScope(specimen, completing: false));
            var specimenGate = new RuleEvaluation(specimenChecks);
            if (specimenGate.IsHardStopped)
            {
                return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(specimenGate);
            }
        }

        var lotIds = new List<long> { request.PrimaryLotId };
        if (request.AdditionalLotIds is { Count: > 0 })
        {
            lotIds.AddRange(request.AdditionalLotIds);
        }

        var today = Today();
        var lotChecks = new List<RuleResult>();
        foreach (var lotId in lotIds.Distinct())
        {
            var lot = await _lots.GetByIdAsync(lotId, ct);
            if (lot is null)
            {
                return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail($"Antibody panel lot {lotId} was not found.");
            }

            lotChecks.Add(AntibodyPanelLotValidityRule.Evaluate(lot.IsActive, lot.ExpiresOn, today));
        }

        var lotEvaluation = new RuleEvaluation(lotChecks);
        if (lotEvaluation.IsHardStopped)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(lotEvaluation);
        }

        var open = (await _workups.ListAsync(w => w.PatientId == patientId, ct))
            .Where(w => AntibodyIdentificationHistoryPostRule.IsOpen(w.Status))
            .ToList();
        var overlap = AntibodyIdentificationWorkupScopeRule.EvaluateOverlappingOpen(
            creatingUnscoped: request.SpecimenId is null,
            hasOpenUnscoped: open.Any(w => w.SpecimenId is null),
            hasOpenOnSameSpecimen: request.SpecimenId is long sid && open.Any(w => w.SpecimenId == sid),
            hasAnyOpen: open.Count > 0);
        if (overlap.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(new RuleEvaluation([overlap]));
        }

        var unscoped = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenScope(request.SpecimenId is not null);

        var workup = new AntibodyIdentificationWorkup
        {
            PatientId = patientId,
            SpecimenId = request.SpecimenId,
            SourceResultId = await ResolveSourceResultIdAsync(request.SpecimenId, ct),
            PrimaryLotId = request.PrimaryLotId,
            Status = AntibodyWorkupStatus.InProgress
        };
        await _workups.AddAsync(workup, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var lotId in lotIds.Distinct())
        {
            await _workupLots.AddAsync(new AntibodyIdentificationWorkupLot
            {
                WorkupId = workup.Id,
                LotId = lotId,
                IsPrimary = lotId == request.PrimaryLotId
            }, ct);
        }

        _audit.Record(
            AuditEventType.Result,
            nameof(AntibodyIdentificationWorkup),
            workup.Id,
            newValue: new { workup.PatientId, workup.PrimaryLotId, workup.SpecimenId },
            reason: "Antibody-identification workup opened.");
        await _unitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<AntibodyIdWorkupDetailDto>.Ok(
            await MapDetailAsync(workup, ct),
            new RuleEvaluation(lotChecks.Concat(specimenChecks).Append(unscoped).Append(overlap)));
    }

    public async Task<EvaluationResult<AntibodyIdWorkupDetailDto>> LinkSpecimenAsync(
        long workupId,
        LinkAntibodyIdSpecimenRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        var workup = await _workups.GetByIdAsync(workupId, ct);
        if (workup is null)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Antibody-identification workup not found.");
        }

        var canLink = AntibodyIdentificationWorkupScopeRule.EvaluateCanLinkSpecimen(workup.Status);
        if (canLink.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(new RuleEvaluation([canLink]));
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<AntibodyIdWorkupDetailDto>(workup.PatientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var specimen = await _specimens.GetByIdAsync(request.SpecimenId, ct);
        if (specimen is null || specimen.PatientId != workup.PatientId)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Specimen was not found for this patient.");
        }

        var usability = EvaluateSpecimenForScope(specimen, completing: false);
        var usabilityEval = new RuleEvaluation(usability);
        if (usabilityEval.IsHardStopped)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(usabilityEval);
        }

        if (workup.SpecimenId == request.SpecimenId)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Ok(
                await MapDetailAsync(workup, ct),
                new RuleEvaluation(usability.Append(AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenScope(true))));
        }

        var open = (await _workups.ListAsync(w => w.PatientId == workup.PatientId && w.Id != workup.Id, ct))
            .Where(w => AntibodyIdentificationHistoryPostRule.IsOpen(w.Status))
            .ToList();
        var overlap = AntibodyIdentificationWorkupScopeRule.EvaluateOverlappingOpen(
            creatingUnscoped: false,
            hasOpenUnscoped: false,
            hasOpenOnSameSpecimen: open.Any(w => w.SpecimenId == request.SpecimenId),
            hasAnyOpen: open.Count > 0);
        if (overlap.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(new RuleEvaluation([overlap]));
        }

        workup.SpecimenId = request.SpecimenId;
        workup.SourceResultId = await ResolveSourceResultIdAsync(request.SpecimenId, ct);
        if (workup.InterpretedUtc is not null || workup.ReviewedUtc is not null)
        {
            await InvalidateJudgmentAfterPanelChangeAsync(workup, ct);
        }
        else
        {
            _workups.Update(workup);
        }

        _audit.Record(
            AuditEventType.Result,
            nameof(AntibodyIdentificationWorkup),
            workup.Id,
            newValue: new { workup.SpecimenId, workup.SourceResultId },
            reason: "Antibody-identification workup linked to a specimen. Identification-of-record scope is now specimen-scoped.");
        await _unitOfWork.SaveChangesAsync(ct);
        await RefreshAssistFindingsAsync(workup, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<AntibodyIdWorkupDetailDto>.Ok(
            await MapDetailAsync(workup, ct),
            new RuleEvaluation(usability.Append(AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenScope(true))));
    }

    public async Task<EvaluationResult<AntibodyIdWorkupDetailDto>> AttachLotsAsync(
        long workupId,
        AttachAntibodyIdLotsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        var workup = await _workups.GetByIdAsync(workupId, ct);
        if (workup is null)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Antibody-identification workup not found.");
        }

        if (workup.Status is AntibodyWorkupStatus.Completed or AntibodyWorkupStatus.Voided)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Completed or voided workups cannot add panel lots.");
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<AntibodyIdWorkupDetailDto>(workup.PatientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var incoming = (request.LotIds ?? []).Distinct().ToList();
        if (incoming.Count == 0)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("At least one panel or selected-cell lot is required.");
        }

        var existing = (await _workupLots.ListAsync(l => l.WorkupId == workupId, ct))
            .Select(l => l.LotId)
            .ToHashSet();
        var toAdd = incoming.Where(id => !existing.Contains(id)).ToList();
        if (toAdd.Count == 0)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Those lots are already on this workup.");
        }

        var today = Today();
        var lotChecks = new List<RuleResult>();
        foreach (var lotId in toAdd)
        {
            var lot = await _lots.GetByIdAsync(lotId, ct);
            if (lot is null)
            {
                return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail($"Antibody panel lot {lotId} was not found.");
            }

            lotChecks.Add(AntibodyPanelLotValidityRule.Evaluate(lot.IsActive, lot.ExpiresOn, today));
        }

        var lotEvaluation = new RuleEvaluation(lotChecks);
        if (lotEvaluation.IsHardStopped)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(lotEvaluation);
        }

        foreach (var lotId in toAdd)
        {
            await _workupLots.AddAsync(new AntibodyIdentificationWorkupLot
            {
                WorkupId = workupId,
                LotId = lotId,
                IsPrimary = false
            }, ct);
        }

        await InvalidateJudgmentAfterPanelChangeAsync(workup, ct);

        _audit.Record(
            AuditEventType.Result,
            nameof(AntibodyIdentificationWorkup),
            workupId,
            newValue: new { AttachedLotIds = toAdd },
            reason: "Selected-cell or additional panel lot attached to antibody-identification workup.");
        await _unitOfWork.SaveChangesAsync(ct);
        await RefreshAssistFindingsAsync(workup, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<AntibodyIdWorkupDetailDto>.Ok(await MapDetailAsync(workup, ct), lotEvaluation);
    }

    public async Task<OperationResult<AntibodyIdWorkupDetailDto>> RecordReactionsAsync(
        long workupId,
        IReadOnlyList<RecordAntibodyIdReactionRequest> reactions,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedOpAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        var workup = await _workups.GetByIdAsync(workupId, ct);
        if (workup is null)
        {
            return OperationResult<AntibodyIdWorkupDetailDto>.Fail("Antibody-identification workup not found.");
        }

        if (workup.Status is AntibodyWorkupStatus.Completed or AntibodyWorkupStatus.Voided)
        {
            return OperationResult<AntibodyIdWorkupDetailDto>.Fail("Completed or voided workups cannot be edited.");
        }

        var patientGate = await RejectMergedOrMissingPatientOpAsync<AntibodyIdWorkupDetailDto>(workup.PatientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var allowedCells = await AllowedCellIdsAsync(workupId, ct);
        var existing = (await _reactions.ListAsync(r => r.WorkupId == workupId, ct))
            .GroupBy(r => (r.CellId, Phase: r.PhaseCode.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.Last());

        foreach (var incoming in reactions)
        {
            if (!allowedCells.Contains(incoming.CellId))
            {
                return OperationResult<AntibodyIdWorkupDetailDto>.Fail($"Cell {incoming.CellId} is not on this workup.");
            }

            var phase = string.IsNullOrWhiteSpace(incoming.PhaseCode) ? "" : incoming.PhaseCode.Trim();
            if (phase.Length == 0)
            {
                return OperationResult<AntibodyIdWorkupDetailDto>.Fail("A reaction phase is required.");
            }

            if (existing.TryGetValue((incoming.CellId, phase.ToUpperInvariant()), out var row))
            {
                row.Strength = incoming.Strength;
                _reactions.Update(row);
            }
            else
            {
                var created = new AntibodyIdentificationReaction
                {
                    WorkupId = workupId,
                    CellId = incoming.CellId,
                    PhaseCode = phase,
                    Strength = incoming.Strength
                };
                await _reactions.AddAsync(created, ct);
                existing[(incoming.CellId, phase.ToUpperInvariant())] = created;
            }
        }

        if (reactions.Count > 0 && (workup.InterpretedUtc is not null || workup.ReviewedUtc is not null))
        {
            await InvalidateJudgmentAfterPanelChangeAsync(workup, ct);
        }
        else if (workup.Status == AntibodyWorkupStatus.InProgress)
        {
            workup.Status = AntibodyWorkupStatus.PendingInterpretation;
            _workups.Update(workup);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        if (reactions.Count > 0)
        {
            await RefreshAssistFindingsAsync(workup, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<AntibodyIdWorkupDetailDto>.Ok(await MapDetailAsync(workup, ct));
    }

    public async Task<OperationResult<AntibodyIdWorkupDetailDto>> RecordDatAsync(
        long workupId, RecordAntibodyIdDatRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedOpAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        var workup = await RequireEditableAsync(workupId, ct);
        if (!workup.Succeeded)
        {
            return OperationResult<AntibodyIdWorkupDetailDto>.Fail(workup.Error ?? "Workup is not editable.");
        }

        var current = workup.Value!;
        var method = string.IsNullOrWhiteSpace(request.DatMethod) ? null : request.DatMethod.Trim();
        var datChanged = current.DatResult != request.DatResult
                         || !string.Equals(current.DatMethod, method, StringComparison.Ordinal);
        current.DatResult = request.DatResult;
        current.DatMethod = method;
        if (datChanged)
        {
            await InvalidateJudgmentAfterPanelChangeAsync(current, ct);
        }
        else
        {
            _workups.Update(current);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        if (datChanged)
        {
            await RefreshAssistFindingsAsync(current, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<AntibodyIdWorkupDetailDto>.Ok(await MapDetailAsync(current, ct));
    }

    public async Task<OperationResult<AntibodyIdWorkupDetailDto>> RecordCommentAsync(
        long workupId, string? comment, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedOpAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        var workup = await RequireEditableAsync(workupId, ct);
        if (!workup.Succeeded)
        {
            return OperationResult<AntibodyIdWorkupDetailDto>.Fail(workup.Error ?? "Workup is not editable.");
        }

        workup.Value!.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        _workups.Update(workup.Value);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<AntibodyIdWorkupDetailDto>.Ok(await MapDetailAsync(workup.Value, ct));
    }

    public async Task<EvaluationResult<AntibodyIdAssistDto>> RunAssistAsync(long workupId, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync<AntibodyIdAssistDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        var workup = await _workups.GetByIdAsync(workupId, ct);
        if (workup is null)
        {
            return EvaluationResult<AntibodyIdAssistDto>.Fail("Antibody-identification workup not found.");
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<AntibodyIdAssistDto>(workup.PatientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var assist = await RefreshAssistFindingsAsync(workup, ct);

        _audit.Record(
            AuditEventType.Result,
            nameof(AntibodyIdentificationWorkup),
            workupId,
            newValue: new
            {
                AssistFindings = assist.Findings.Select(f => new { f.Specificity, f.Classification }).ToList(),
                Advisory = true
            },
            reason: "Antibody-identification assistance evaluated. Not an identification.");
        await _unitOfWork.SaveChangesAsync(ct);

        var stored = (await _findings.ListAsync(f => f.WorkupId == workupId, ct))
            .Where(f => f.Source is AntibodyIdSource.Assist or AntibodyIdSource.History
                        && f.Rationale != "Superseded by a later assist evaluation.")
            .Select(ToFindingDto)
            .ToList();

        return EvaluationResult<AntibodyIdAssistDto>.Ok(
            new AntibodyIdAssistDto(stored, assist.Evaluation.Warnings),
            assist.Evaluation);
    }

    public async Task<EvaluationResult<AntibodyIdWorkupDetailDto>> RecordInterpretationAsync(
        long workupId,
        RecordAntibodyIdInterpretationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(request.Interpretation))
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Technologist interpretation is required.");
        }

        var workup = await _workups.GetByIdAsync(workupId, ct);
        if (workup is null)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Antibody-identification workup not found.");
        }

        if (workup.Status is AntibodyWorkupStatus.Completed or AntibodyWorkupStatus.Voided)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Completed or voided workups cannot be interpreted.");
        }

        var catalog = await LoadAntibodyCatalogAsync(ct);
        var patientAntigens = await LoadPatientAntigenSnapshotsAsync(workup.PatientId, ct);
        var attributeCodes = catalog.ToDictionary(c => c.Id, c => c.Code);
        var resolvedFindings = new List<(AntibodyIdInterpretationItem Item, AntibodyIdentificationCatalogResolver.Resolution Resolution)>();
        var catalogWarnings = new List<RuleResult>();
        foreach (var item in request.Findings ?? [])
        {
            var resolution = AntibodyIdentificationCatalogResolver.Resolve(
                item.BloodAttributeDefinitionId, item.Specificity, catalog);
            catalogWarnings.Add(AntibodyIdentificationCatalogResolver.EvaluateIdentifiedCatalog(
                item.Classification, resolution.CatalogMatched, resolution.Specificity));
            var antigenCode = resolution.DefinitionId is long id && attributeCodes.TryGetValue(id, out var code)
                ? code
                : null;
            var versusType = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusPatientType(
                item.Classification, antigenCode, resolution.Specificity, patientAntigens);
            if (versusType.Severity == RuleSeverity.Warning)
            {
                catalogWarnings.Add(versusType);
            }

            resolvedFindings.Add((item, resolution));
        }

        var policy = await _policies.GetAntibodyIdentificationPolicyAsync(ct);
        var assistInput = await BuildAssistInputAsync(workup, policy, ct);
        var assist = AntibodyIdentificationAssistEvaluator.Evaluate(assistInput);
        var versusExcluded = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusAssistExclusion(
            resolvedFindings.Select(f => new AntibodyIdentificationRecordedFinding(
                f.Resolution.Specificity,
                f.Resolution.DefinitionId is long id && attributeCodes.TryGetValue(id, out var code) ? code : null,
                f.Item.Classification,
                AntibodyIdSource.Technologist)),
            assist.Findings);
        if (versusExcluded.Severity == RuleSeverity.Warning)
        {
            catalogWarnings.Add(versusExcluded);
        }

        var identifiedNames = resolvedFindings
            .Where(f => f.Item.Classification == AntibodyIdClassification.Identified)
            .Select(f => f.Resolution.Specificity)
            .ToList();
        catalogWarnings.Add(AntibodyIdentificationInterpretationRule.EvaluateUnexcludedAtCompletion(
            assist.Findings.Where(f => f.Classification == AntibodyIdClassification.CannotExclude).Select(f => f.Specificity),
            identifiedNames));
        catalogWarnings.Add(AntibodyIdentificationInterpretationRule.EvaluateHistoryRemainsAtCompletion(assistInput.HistoricalAntibodies));
        catalogWarnings.AddRange(assist.Evaluation.Warnings.Where(w =>
            w.Code is AntibodyIdentificationAssistEvaluator.HistoricalUndetectedCode
                or AntibodyIdentificationAssistEvaluator.SelectedCellNeededCode
                or AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode));
        catalogWarnings.Add(AntibodyIdentificationInterpretationRule.EvaluateIdentifiedWillPost(identifiedNames.Count));

        var recorded = resolvedFindings
            .Select(f => new AntibodyIdentificationRecordedFinding(
                f.Resolution.Specificity,
                f.Resolution.DefinitionId?.ToString(),
                f.Item.Classification,
                AntibodyIdSource.Technologist))
            .ToList();
        var assistCheck = AntibodyIdentificationInterpretationRule.EvaluateAssistMustNotIdentify(recorded);
        if (assistCheck.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(new RuleEvaluation([assistCheck]));
        }

        var existing = await _findings.ListAsync(f => f.WorkupId == workupId && f.Source == AntibodyIdSource.Technologist, ct);
        foreach (var row in existing)
        {
            row.Rationale = "Superseded by a later technologist interpretation.";
            _findings.Update(row);
        }

        foreach (var (item, resolution) in resolvedFindings)
        {
            if (item.Classification == AntibodyIdClassification.Identified && item.SourceWouldBeAssist())
            {
                return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(new RuleEvaluation(
                [
                    RuleResult.HardStop(
                        AntibodyIdentificationInterpretationRule.AssistIdentifiedCode,
                        "Assisted findings cannot be recorded as Identified.")
                ]));
            }

            await _findings.AddAsync(new AntibodyIdentificationFinding
            {
                WorkupId = workupId,
                BloodAttributeDefinitionId = resolution.DefinitionId,
                Specificity = string.IsNullOrWhiteSpace(resolution.Specificity)
                    ? item.Specificity.Trim()
                    : resolution.Specificity,
                Classification = item.Classification,
                Source = AntibodyIdSource.Technologist,
                Rationale = item.Rationale
            }, ct);
        }

        workup.TechnologistInterpretation = request.Interpretation.Trim();
        workup.TechnologistUser = _currentUser.UserName;
        workup.InterpretedUtc = _clock.UtcNow;
        workup.Status = AntibodyWorkupStatus.PendingSupervisorReview;
        workup.SupervisorAccepted = false;
        workup.SupervisorUser = null;
        workup.ReviewedUtc = null;
        workup.SupervisorComment = null;
        _workups.Update(workup);

        _audit.Record(
            AuditEventType.Result,
            nameof(AntibodyIdentificationWorkup),
            workupId,
            newValue: new
            {
                workup.TechnologistUser,
                workup.TechnologistInterpretation,
                Findings = (request.Findings ?? []).Select(f => new { f.Specificity, f.Classification }).ToList()
            },
            reason: "Technologist antibody-identification interpretation recorded.");
        await _unitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<AntibodyIdWorkupDetailDto>.Ok(
            await MapDetailAsync(workup, ct),
            catalogWarnings.Count == 0 ? null : new RuleEvaluation(catalogWarnings));
    }

    public async Task<EvaluationResult<AntibodyIdWorkupDetailDto>> ReviewAsync(
        long workupId,
        ReviewAntibodyIdWorkupRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoOverride, ImmunoAuthorizationRule.EvaluateAntibodyIdReview, ct);
        if (denied is not null)
        {
            return denied;
        }

        var workup = await _workups.GetByIdAsync(workupId, ct);
        if (workup is null)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Antibody-identification workup not found.");
        }

        if (workup.Status is AntibodyWorkupStatus.Completed or AntibodyWorkupStatus.Voided)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Completed or voided workups cannot be reviewed.");
        }

        if (string.IsNullOrWhiteSpace(workup.TechnologistInterpretation))
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(new RuleEvaluation(
            [
                AntibodyIdentificationInterpretationRule.EvaluateInterpretationRecorded(false)
            ]));
        }

        var currentInterp = AntibodyIdentificationInterpretationRule.EvaluateReadyForSupervisorReview(
            workup.InterpretedUtc is not null);
        if (currentInterp.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(new RuleEvaluation([currentInterp]));
        }

        var policy = await _policies.GetAntibodyIdentificationPolicyAsync(ct);
        var self = AntibodyIdentificationInterpretationRule.EvaluateSupervisorReview(
            policy,
            supervisorReviewed: true,
            supervisorAccepted: request.Accepted,
            technologistUser: workup.TechnologistUser,
            supervisorUser: _currentUser.UserName);
        if (self.Severity == RuleSeverity.HardStop && self.Code == AntibodyIdentificationInterpretationRule.ReviewSelfCode)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(new RuleEvaluation([self]));
        }

        IReadOnlyList<RuleResult> reviewClinical = [];
        if (request.Accepted)
        {
            reviewClinical = await EvaluateLiveClinicalResultsAsync(workup, ct);
            if (reviewClinical.Any(r => r.Severity == RuleSeverity.HardStop))
            {
                return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(new RuleEvaluation(reviewClinical));
            }

            var reviewAck = AntibodyIdentificationInterpretationRule.EvaluateReviewAcknowledgment(
                reviewClinical, request.WarningAcknowledgment);
            if (reviewAck.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(
                    new RuleEvaluation(reviewClinical.Append(reviewAck)));
            }

            reviewClinical = reviewClinical.Append(reviewAck).ToList();
        }

        workup.SupervisorUser = _currentUser.UserName;
        workup.ReviewedUtc = _clock.UtcNow;
        workup.SupervisorAccepted = request.Accepted;
        workup.SupervisorComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        workup.Status = request.Accepted
            ? AntibodyWorkupStatus.PendingSupervisorReview
            : AntibodyWorkupStatus.PendingInterpretation;
        _workups.Update(workup);

        _audit.Record(
            AuditEventType.Verify,
            nameof(AntibodyIdentificationWorkup),
            workupId,
            newValue: new { workup.SupervisorUser, workup.SupervisorAccepted, workup.SupervisorComment },
            reason: request.Accepted
                ? "Supervisor accepted antibody-identification interpretation."
                : "Supervisor rejected antibody-identification interpretation.");
        await _unitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<AntibodyIdWorkupDetailDto>.Ok(
            await MapDetailAsync(workup, ct),
            reviewClinical.Count == 0 ? null : new RuleEvaluation(reviewClinical));
    }

    public async Task<EvaluationResult<AntibodyIdWorkupDetailDto>> CompleteAsync(
        long workupId,
        CompleteAntibodyIdWorkupRequest? request = null,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        var workup = await _workups.GetByIdAsync(workupId, ct);
        if (workup is null)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Antibody-identification workup not found.");
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<AntibodyIdWorkupDetailDto>(workup.PatientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        if (workup.SourceResultId is null)
        {
            var linked = await ResolveSourceResultIdAsync(workup.SpecimenId, ct);
            if (linked is long sourceResultId)
            {
                workup.SourceResultId = sourceResultId;
                _workups.Update(workup);
            }
        }

        var findings = await _findings.ListAsync(f => f.WorkupId == workupId, ct);
        var recorded = findings
            .Where(f => f.Rationale != "Superseded by a later assist evaluation."
                        && f.Rationale != "Superseded by a later technologist interpretation.")
            .Select(f => new AntibodyIdentificationRecordedFinding(
                f.Specificity, f.BloodAttributeDefinitionId?.ToString(), f.Classification, f.Source))
            .ToList();

        var identifiedToPost = findings.Count(f =>
            f.Source == AntibodyIdSource.Technologist
            && f.Classification == AntibodyIdClassification.Identified
            && !f.PostedToHistory
            && f.Rationale != "Superseded by a later technologist interpretation.");
        var lastPanelChangeUtc = await LastPanelChangeUtcAsync(workupId, ct);
        var policy = await _policies.GetAntibodyIdentificationPolicyAsync(ct);
        var assistInput = await BuildAssistInputAsync(workup, policy, ct);
        var assistAtComplete = AntibodyIdentificationAssistEvaluator.Evaluate(assistInput);
        var incompletePanel = AntibodyIdentificationAssistEvaluator.HasIncompleteInterpretiveReactions(
            assistInput.Cells, assistInput.InterpretivePhases);
        var evaluation = AntibodyIdentificationInterpretationRule.EvaluateCompletion(
            workup.Status,
            workup.InterpretedUtc is not null && !string.IsNullOrWhiteSpace(workup.TechnologistInterpretation),
            recorded,
            policy,
            workup.ReviewedUtc is not null,
            workup.SupervisorAccepted,
            workup.TechnologistUser,
            workup.SupervisorUser,
            workup.InterpretedUtc,
            lastPanelChangeUtc,
            workup.ReviewedUtc);
        var incompleteAtComplete = AntibodyIdentificationInterpretationRule.EvaluateIncompletePanelAtCompletion(
            incompletePanel, identifiedToPost);
        if (incompleteAtComplete.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(
                new RuleEvaluation(evaluation.Results.Append(incompleteAtComplete)));
        }

        if (!evaluation.IsAllowed)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(evaluation);
        }

        var patientAntigens = await LoadPatientAntigenSnapshotsAsync(workup.PatientId, ct);
        var attributes = (await _attributes.ListAsync(a => a.IsActive, ct)).ToDictionary(a => a.Id);
        var completionResults = evaluation.Results.ToList();
        foreach (var finding in findings.Where(f =>
                     f.Source == AntibodyIdSource.Technologist
                     && f.Classification == AntibodyIdClassification.Identified
                     && f.Rationale != "Superseded by a later technologist interpretation."))
        {
            var antigenCode = finding.BloodAttributeDefinitionId is long id && attributes.TryGetValue(id, out var def)
                ? def.Code
                : null;
            var versusType = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusPatientType(
                finding.Classification, antigenCode, finding.Specificity, patientAntigens);
            if (versusType.Severity == RuleSeverity.Warning)
            {
                completionResults.Add(versusType);
            }
        }

        var identifiedForAssist = findings
            .Where(f =>
                f.Source == AntibodyIdSource.Technologist
                && f.Classification == AntibodyIdClassification.Identified
                && f.Rationale != "Superseded by a later technologist interpretation.")
            .Select(f => new AntibodyIdentificationRecordedFinding(
                f.Specificity,
                f.BloodAttributeDefinitionId is long id && attributes.TryGetValue(id, out var def) ? def.Code : null,
                f.Classification,
                f.Source))
            .ToList();
        var versusExcluded = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusAssistExclusion(
            identifiedForAssist, assistAtComplete.Findings);
        if (versusExcluded.Severity == RuleSeverity.Warning)
        {
            completionResults.Add(versusExcluded);
        }

        completionResults.Add(incompleteAtComplete);
        var identifiedNames = findings
            .Where(f =>
                f.Source == AntibodyIdSource.Technologist
                && f.Classification == AntibodyIdClassification.Identified
                && f.Rationale != "Superseded by a later technologist interpretation.")
            .Select(f => f.Specificity)
            .ToList();
        var cannotExclude = assistAtComplete.Findings
            .Where(f => f.Classification == AntibodyIdClassification.CannotExclude)
            .Select(f => f.Specificity)
            .ToList();
        completionResults.Add(AntibodyIdentificationInterpretationRule.EvaluateUnexcludedAtCompletion(
            cannotExclude, identifiedNames));
        completionResults.Add(AntibodyIdentificationInterpretationRule.EvaluateHistoryRemainsAtCompletion(
            assistInput.HistoricalAntibodies));
        foreach (var histUndetected in assistAtComplete.Evaluation.Warnings.Where(w =>
                     w.Code == AntibodyIdentificationAssistEvaluator.HistoricalUndetectedCode))
        {
            completionResults.Add(histUndetected);
        }

        var selectedCell = assistAtComplete.Evaluation.Warnings.FirstOrDefault(w =>
            w.Code == AntibodyIdentificationAssistEvaluator.SelectedCellNeededCode);
        if (selectedCell is not null)
        {
            completionResults.Add(selectedCell);
        }

        completionResults.Add(AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenScope(workup.SpecimenId is not null));
        if (workup.SpecimenId is long linkedSpecimenId)
        {
            var linkedSpecimen = await _specimens.GetByIdAsync(linkedSpecimenId, ct);
            if (linkedSpecimen is not null)
            {
                var atComplete = EvaluateSpecimenForScope(linkedSpecimen, completing: true);
                if (atComplete.Any(r => r.Severity == RuleSeverity.HardStop))
                {
                    return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(
                        new RuleEvaluation(completionResults.Concat(atComplete)));
                }

                completionResults.AddRange(atComplete.Where(r => r.Severity != RuleSeverity.Pass));
            }
        }
        completionResults.Add(AntibodyIdentificationInterpretationRule.EvaluateIdentifiedWillPost(identifiedToPost));

        var autocontrolPositive = await AutocontrolIsPositiveAsync(workup, ct);
        var datAtComplete = AntibodyIdentificationInterpretationRule.EvaluateDatIndicatedAtCompletion(
            autocontrolPositive, workup.DatResult);
        if (datAtComplete.Severity == RuleSeverity.Warning)
        {
            completionResults.Add(datAtComplete);
        }

        evaluation = new RuleEvaluation(completionResults);
        var acknowledgment = AntibodyIdentificationInterpretationRule.EvaluateCompleteAcknowledgment(
            completionResults, request?.WarningAcknowledgment);
        if (acknowledgment.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(
                new RuleEvaluation(completionResults.Append(acknowledgment)));
        }

        completionResults.Add(acknowledgment);
        evaluation = new RuleEvaluation(completionResults);

        var posted = new List<string>();
        foreach (var finding in findings.Where(f =>
                     f.Source == AntibodyIdSource.Technologist
                     && f.Classification == AntibodyIdClassification.Identified
                     && !f.PostedToHistory
                     && f.Rationale != "Superseded by a later technologist interpretation."))
        {
            await PostHistoryAsync(workup, finding, ct);
            finding.PostedToHistory = true;
            _findings.Update(finding);
            posted.Add(finding.Specificity);
        }

        workup.Status = AntibodyWorkupStatus.Completed;
        workup.CompletedUtc = _clock.UtcNow;
        workup.CompletedBy = _currentUser.UserName;
        _workups.Update(workup);

        _audit.Record(
            AuditEventType.Verify,
            nameof(AntibodyIdentificationWorkup),
            workupId,
            newValue: new
            {
                workup.Status,
                PostedAntibodies = posted,
                WarningAcknowledgment = request?.WarningAcknowledgment?.Trim()
            },
            reason: posted.Count == 0
                ? "Antibody-identification workup completed. No antibodies posted to history."
                : "Antibody-identification workup completed. Technologist-identified antibodies posted to history.");
        await _unitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<AntibodyIdWorkupDetailDto>.Ok(await MapDetailAsync(workup, ct), evaluation);
    }

    public async Task<EvaluationResult<AntibodyIdWorkupDetailDto>> VoidAsync(
        long workupId,
        VoidAntibodyIdWorkupRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<AntibodyIdWorkupDetailDto>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyIdWorkup, ct);
        if (denied is not null)
        {
            return denied;
        }

        var workup = await _workups.GetByIdAsync(workupId, ct);
        if (workup is null)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Fail("Antibody-identification workup not found.");
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<AntibodyIdWorkupDetailDto>(workup.PatientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var evaluation = AntibodyIdentificationInterpretationRule.EvaluateVoid(workup.Status, request.Reason);
        if (!evaluation.IsAllowed)
        {
            return EvaluationResult<AntibodyIdWorkupDetailDto>.Blocked(evaluation);
        }

        workup.Status = AntibodyWorkupStatus.Voided;
        workup.VoidReason = request.Reason.Trim();
        _workups.Update(workup);

        _audit.Record(
            AuditEventType.Result,
            nameof(AntibodyIdentificationWorkup),
            workupId,
            newValue: new { workup.Status, workup.VoidReason },
            reason: "Antibody-identification workup voided. No antibodies posted.");
        await _unitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<AntibodyIdWorkupDetailDto>.Ok(await MapDetailAsync(workup, ct), evaluation);
    }

    private async Task PostHistoryAsync(
        AntibodyIdentificationWorkup workup,
        AntibodyIdentificationFinding finding,
        CancellationToken ct)
    {
        var catalog = await LoadAntibodyCatalogAsync(ct);
        var resolution = AntibodyIdentificationCatalogResolver.Resolve(
            finding.BloodAttributeDefinitionId, finding.Specificity, catalog);
        if (resolution.CatalogMatched)
        {
            finding.BloodAttributeDefinitionId = resolution.DefinitionId;
            finding.Specificity = resolution.Specificity;
        }

        var active = await _antibodies.ListAsync(
            a => a.PatientId == workup.PatientId && a.IsActive, ct);
        var existing = active.FirstOrDefault(a =>
            (finding.BloodAttributeDefinitionId is long catalogId && a.BloodAttributeDefinitionId == catalogId)
            || string.Equals(a.AntibodySpecificity, finding.Specificity, StringComparison.Ordinal));
        if (existing is not null)
        {
            return;
        }

        await _antibodies.AddAsync(new AntibodyHistory
        {
            PatientId = workup.PatientId,
            BloodAttributeDefinitionId = finding.BloodAttributeDefinitionId,
            AntibodySpecificity = finding.Specificity,
            Status = AntibodyStatus.Identified,
            SourceResultId = workup.SourceResultId,
            Comment = "Posted from supervisor-accepted antibody-identification workup.",
            IsActive = true
        }, ct);

        _audit.Record(
            AuditEventType.Antibody,
            nameof(AntibodyHistory),
            workup.PatientId,
            newValue: new
            {
                finding.Specificity,
                WorkupId = workup.Id,
                finding.BloodAttributeDefinitionId
            },
            reason: "Identified on reviewed antibody-identification workup.");
    }

    private async Task<AntibodyIdentificationAssistInput> BuildAssistInputAsync(
        AntibodyIdentificationWorkup workup,
        AntibodyIdentificationPolicy policy,
        CancellationToken ct)
    {
        var lotLinks = await _workupLots.ListAsync(l => l.WorkupId == workup.Id, ct);
        var lotIds = lotLinks.Select(l => l.LotId).Distinct().ToList();
        var cells = (await _cells.ListAsync(c => lotIds.Contains(c.LotId), ct))
            .OrderBy(c => c.SortOrder)
            .ToList();
        var cellIds = cells.Select(c => c.Id).ToHashSet();
        var antigens = await _cellAntigens.ListAsync(a => cellIds.Contains(a.CellId), ct);
        var reactions = await _reactions.ListAsync(r => r.WorkupId == workup.Id, ct);
        var attributes = (await _attributes.ListAsync(a => a.IsActive, ct)).ToDictionary(a => a.Id);
        var phenotype = await _antigenProfiles.ListAsync(p => p.PatientId == workup.PatientId, ct);
        var history = await _antibodies.ListAsync(a => a.PatientId == workup.PatientId, ct);

        var snapshots = cells.Select(cell =>
        {
            var antigenMap = antigens
                .Where(a => a.CellId == cell.Id && attributes.ContainsKey(a.BloodAttributeDefinitionId))
                .ToDictionary(
                    a => attributes[a.BloodAttributeDefinitionId].Code,
                    a => a.Expression,
                    StringComparer.Ordinal);
            var reactionMap = reactions
                .Where(r => r.CellId == cell.Id)
                .GroupBy(r => r.PhaseCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last().Strength, StringComparer.OrdinalIgnoreCase);
            return new AntibodyIdentificationCellSnapshot(
                cell.Id.ToString(),
                cell.CellNumber,
                cell.Role,
                antigenMap,
                reactionMap);
        }).ToList();

        var catalog = attributes.Values
            .Select(a => new AntibodyIdAntigenInfo(a.Code, a.AntibodyName))
            .ToList();
        var patientAntigens = phenotype
            .Where(p => attributes.ContainsKey(p.BloodAttributeDefinitionId))
            .Select(p => new PatientAntigenSnapshot(
                attributes[p.BloodAttributeDefinitionId].Code,
                p.Result,
                AntigenTypingMethodInfo.IndicatesPredictedGenotype(p.Method)))
            .ToList();
        var historical = history
            .Select(a => new HistoricalAntibodySnapshot(
                a.AntibodySpecificity,
                a.BloodAttributeDefinitionId is long id && attributes.TryGetValue(id, out var def) ? def.Code : null,
                a.Status,
                a.IsActive))
            .ToList();

        return new AntibodyIdentificationAssistInput(
            snapshots,
            DefaultInterpretivePhases,
            catalog,
            patientAntigens,
            historical,
            workup.DatResult,
            policy);
    }

    private async Task<AntibodyIdWorkupDetailDto> MapDetailAsync(AntibodyIdentificationWorkup workup, CancellationToken ct)
    {
        var today = Today();
        var lotLinks = await _workupLots.ListAsync(l => l.WorkupId == workup.Id, ct);
        var lots = (await _lots.ListAsync(ct)).ToDictionary(l => l.Id);
        var manufacturers = (await _manufacturers.ListAsync(ct)).ToDictionary(m => m.Id);
        var lotDtos = lotLinks
            .Select(link => lots.TryGetValue(link.LotId, out var lot)
                ? ToLotDto(lot, manufacturers.GetValueOrDefault(lot.ManufacturerId)?.Name ?? "", today)
                : null)
            .Where(d => d is not null)
            .Cast<AntibodyPanelLotListItemDto>()
            .ToList();

        var lotIds = lotLinks.Select(l => l.LotId).ToHashSet();
        var selectedLotIds = lots.Values.Where(l => lotIds.Contains(l.Id) && l.IsSelectedCellLot).Select(l => l.Id).ToHashSet();
        var cells = (await _cells.ListAsync(c => lotIds.Contains(c.LotId), ct))
            .OrderBy(c => selectedLotIds.Contains(c.LotId) ? 1 : 0)
            .ThenBy(c => c.SortOrder)
            .ToList();
        var cellIds = cells.Select(c => c.Id).ToHashSet();
        var antigens = await _cellAntigens.ListAsync(a => cellIds.Contains(a.CellId), ct);
        var reactions = await _reactions.ListAsync(r => r.WorkupId == workup.Id, ct);
        var attributes = (await _attributes.ListAsync(ct)).ToDictionary(a => a.Id);
        var findings = await _findings.ListAsync(f => f.WorkupId == workup.Id, ct);

        var cellDtos = cells.Select(cell =>
        {
            var antigenDtos = antigens
                .Where(a => a.CellId == cell.Id && attributes.ContainsKey(a.BloodAttributeDefinitionId))
                .Select(a =>
                {
                    var def = attributes[a.BloodAttributeDefinitionId];
                    return new AntibodyIdCellAntigenDto(def.Id, def.Code, def.AntibodyName, a.Expression);
                })
                .ToList();
            var reactionDtos = reactions
                .Where(r => r.CellId == cell.Id)
                .Select(r => new AntibodyIdReactionDto(r.PhaseCode, r.Strength))
                .ToList();
            return new AntibodyIdCellDto(
                cell.Id,
                cell.CellNumber,
                cell.Role,
                cell.SortOrder,
                selectedLotIds.Contains(cell.LotId) || cell.Role == PanelCellRole.Selected,
                antigenDtos,
                reactionDtos);
        }).ToList();

        var findingDtos = findings
            .Where(f => f.Rationale != "Superseded by a later assist evaluation."
                        && f.Rationale != "Superseded by a later technologist interpretation.")
            .Select(ToFindingDto)
            .ToList();

        return new AntibodyIdWorkupDetailDto(
            workup.Id,
            workup.PatientId,
            workup.SpecimenId,
            workup.SourceResultId,
            workup.Status,
            workup.DatResult,
            workup.DatMethod,
            workup.Comment,
            workup.TechnologistInterpretation,
            workup.TechnologistUser,
            workup.InterpretedUtc,
            workup.SupervisorUser,
            workup.ReviewedUtc,
            workup.SupervisorComment,
            workup.SupervisorAccepted,
            workup.CompletedUtc,
            workup.CompletedBy,
            workup.VoidReason,
            lotDtos,
            cellDtos,
            findingDtos,
            DefaultInterpretivePhases,
            AssistIsAdvisory: true);
    }

    private async Task InvalidateJudgmentAfterPanelChangeAsync(
        AntibodyIdentificationWorkup workup,
        CancellationToken ct)
    {
        var hadJudgment = workup.InterpretedUtc is not null || workup.ReviewedUtc is not null;
        if (!hadJudgment)
        {
            if (workup.Status == AntibodyWorkupStatus.InProgress)
            {
                workup.Status = AntibodyWorkupStatus.PendingInterpretation;
            }

            _workups.Update(workup);
            return;
        }

        var findings = await _findings.ListAsync(
            f => f.WorkupId == workup.Id && f.Source == AntibodyIdSource.Technologist, ct);
        foreach (var finding in findings.Where(f =>
                     f.Rationale != "Superseded by a later technologist interpretation."))
        {
            finding.Rationale = "Superseded by a later technologist interpretation.";
            _findings.Update(finding);
        }

        workup.InterpretedUtc = null;
        workup.SupervisorAccepted = false;
        workup.SupervisorUser = null;
        workup.ReviewedUtc = null;
        workup.SupervisorComment = null;
        if (workup.Status is AntibodyWorkupStatus.PendingSupervisorReview)
        {
            workup.Status = AntibodyWorkupStatus.PendingInterpretation;
        }

        _workups.Update(workup);
        _audit.Record(
            AuditEventType.Result,
            nameof(AntibodyIdentificationWorkup),
            workup.Id,
            newValue: new { PanelChangedAfterJudgment = true, workup.Status },
            reason: "Panel reactions, selected cells, DAT, or specimen scope changed after interpretation or review. Interpretation and supervisor review must be repeated.");
    }

    private async Task<DateTime?> LastPanelChangeUtcAsync(long workupId, CancellationToken ct)
    {
        DateTime? latest = null;
        foreach (var reaction in await _reactions.ListAsync(r => r.WorkupId == workupId, ct))
        {
            var stamp = reaction.ModifiedUtc ?? reaction.CreatedUtc;
            if (latest is null || stamp > latest)
            {
                latest = stamp;
            }
        }

        foreach (var lot in await _workupLots.ListAsync(l => l.WorkupId == workupId, ct))
        {
            var stamp = lot.ModifiedUtc ?? lot.CreatedUtc;
            if (latest is null || stamp > latest)
            {
                latest = stamp;
            }
        }

        return latest;
    }

    private async Task<bool> AutocontrolIsPositiveAsync(AntibodyIdentificationWorkup workup, CancellationToken ct)
    {
        var lotIds = (await _workupLots.ListAsync(l => l.WorkupId == workup.Id, ct))
            .Select(l => l.LotId)
            .ToList();
        var acCells = await _cells.ListAsync(
            c => lotIds.Contains(c.LotId) && c.Role == PanelCellRole.Autocontrol, ct);
        if (acCells.Count == 0)
        {
            return false;
        }

        var acIds = acCells.Select(c => c.Id).ToHashSet();
        var reactions = await _reactions.ListAsync(r => r.WorkupId == workup.Id, ct);
        return reactions.Any(r => acIds.Contains(r.CellId) && ReactionGradeInfo.IsPositive(r.Strength));
    }

    private async Task<IReadOnlyList<PatientAntigenSnapshot>> LoadPatientAntigenSnapshotsAsync(
        long patientId, CancellationToken ct)
    {
        var attributes = (await _attributes.ListAsync(a => a.IsActive, ct)).ToDictionary(a => a.Id);
        var phenotype = await _antigenProfiles.ListAsync(p => p.PatientId == patientId, ct);
        return phenotype
            .Where(p => attributes.ContainsKey(p.BloodAttributeDefinitionId))
            .Select(p => new PatientAntigenSnapshot(
                attributes[p.BloodAttributeDefinitionId].Code,
                p.Result,
                AntigenTypingMethodInfo.IndicatesPredictedGenotype(p.Method)))
            .ToList();
    }

    private async Task<IReadOnlyList<AntibodyCatalogItem>> LoadAntibodyCatalogAsync(CancellationToken ct)
    {
        var attributes = await _attributes.ListAsync(a => a.IsActive, ct);
        return attributes
            .Select(a => new AntibodyCatalogItem(a.Id, a.Code, a.Name, a.AntibodyName))
            .ToList();
    }

    private async Task<IReadOnlyList<RuleResult>> EvaluateLiveClinicalResultsAsync(
        AntibodyIdentificationWorkup workup,
        CancellationToken ct)
    {
        var findings = await _findings.ListAsync(f => f.WorkupId == workup.Id, ct);
        var identifiedToPost = findings.Count(f =>
            f.Source == AntibodyIdSource.Technologist
            && f.Classification == AntibodyIdClassification.Identified
            && !f.PostedToHistory
            && f.Rationale != "Superseded by a later technologist interpretation.");
        var policy = await _policies.GetAntibodyIdentificationPolicyAsync(ct);
        var assistInput = await BuildAssistInputAsync(workup, policy, ct);
        var assist = AntibodyIdentificationAssistEvaluator.Evaluate(assistInput);
        var incomplete = AntibodyIdentificationAssistEvaluator.HasIncompleteInterpretiveReactions(
            assistInput.Cells, assistInput.InterpretivePhases);
        var results = new List<RuleResult>
        {
            AntibodyIdentificationInterpretationRule.EvaluateIncompletePanelAtCompletion(incomplete, identifiedToPost)
        };

        if (workup.SpecimenId is long specimenId)
        {
            var specimen = await _specimens.GetByIdAsync(specimenId, ct);
            if (specimen is not null)
            {
                results.AddRange(EvaluateSpecimenForScope(specimen, completing: true));
            }
        }

        var attributes = (await _attributes.ListAsync(a => a.IsActive, ct)).ToDictionary(a => a.Id);
        var patientAntigens = await LoadPatientAntigenSnapshotsAsync(workup.PatientId, ct);
        foreach (var finding in findings.Where(f =>
                     f.Source == AntibodyIdSource.Technologist
                     && f.Classification == AntibodyIdClassification.Identified
                     && f.Rationale != "Superseded by a later technologist interpretation."))
        {
            var antigenCode = finding.BloodAttributeDefinitionId is long id && attributes.TryGetValue(id, out var def)
                ? def.Code
                : null;
            var versusType = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusPatientType(
                finding.Classification, antigenCode, finding.Specificity, patientAntigens);
            if (versusType.Severity == RuleSeverity.Warning)
            {
                results.Add(versusType);
            }
        }

        var identifiedForAssist = findings
            .Where(f =>
                f.Source == AntibodyIdSource.Technologist
                && f.Classification == AntibodyIdClassification.Identified
                && f.Rationale != "Superseded by a later technologist interpretation.")
            .Select(f => new AntibodyIdentificationRecordedFinding(
                f.Specificity,
                f.BloodAttributeDefinitionId is long id && attributes.TryGetValue(id, out var def) ? def.Code : null,
                f.Classification,
                f.Source))
            .ToList();
        var versusExcluded = AntibodyIdentificationInterpretationRule.EvaluateIdentifiedVersusAssistExclusion(
            identifiedForAssist, assist.Findings);
        if (versusExcluded.Severity == RuleSeverity.Warning)
        {
            results.Add(versusExcluded);
        }

        var identifiedNames = findings
            .Where(f =>
                f.Source == AntibodyIdSource.Technologist
                && f.Classification == AntibodyIdClassification.Identified
                && f.Rationale != "Superseded by a later technologist interpretation.")
            .Select(f => f.Specificity)
            .ToList();
        results.Add(AntibodyIdentificationInterpretationRule.EvaluateUnexcludedAtCompletion(
            assist.Findings.Where(f => f.Classification == AntibodyIdClassification.CannotExclude).Select(f => f.Specificity),
            identifiedNames));
        results.Add(AntibodyIdentificationInterpretationRule.EvaluateHistoryRemainsAtCompletion(assistInput.HistoricalAntibodies));
        results.AddRange(assist.Evaluation.Warnings.Where(w =>
            w.Code is AntibodyIdentificationAssistEvaluator.HistoricalUndetectedCode
                or AntibodyIdentificationAssistEvaluator.SelectedCellNeededCode));
        results.Add(AntibodyIdentificationInterpretationRule.EvaluateIdentifiedWillPost(identifiedToPost));

        var autocontrolPositive = await AutocontrolIsPositiveAsync(workup, ct);
        var dat = AntibodyIdentificationInterpretationRule.EvaluateDatIndicatedAtCompletion(
            autocontrolPositive, workup.DatResult);
        if (dat.Severity == RuleSeverity.Warning)
        {
            results.Add(dat);
        }

        return results;
    }

    private async Task<AntibodyIdentificationAssistResult> RefreshAssistFindingsAsync(
        AntibodyIdentificationWorkup workup,
        CancellationToken ct)
    {
        var policy = await _policies.GetAntibodyIdentificationPolicyAsync(ct);
        var input = await BuildAssistInputAsync(workup, policy, ct);
        var assist = AntibodyIdentificationAssistEvaluator.Evaluate(input);

        var existing = await _findings.ListAsync(f => f.WorkupId == workup.Id, ct);
        foreach (var stale in existing.Where(f => f.Source is AntibodyIdSource.Assist or AntibodyIdSource.History))
        {
            var tracked = await _findings.GetByIdAsync(stale.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            tracked.Classification = AntibodyIdClassification.Inconclusive;
            tracked.Rationale = "Superseded by a later assist evaluation.";
            _findings.Update(tracked);
        }

        var attributes = (await _attributes.ListAsync(a => a.IsActive, ct))
            .ToDictionary(a => a.Code, StringComparer.Ordinal);

        foreach (var finding in assist.Findings)
        {
            long? defId = null;
            if (finding.AttributeCode is not null && attributes.TryGetValue(finding.AttributeCode, out var def))
            {
                defId = def.Id;
            }

            await _findings.AddAsync(new AntibodyIdentificationFinding
            {
                WorkupId = workup.Id,
                BloodAttributeDefinitionId = defId,
                Specificity = finding.Specificity,
                Classification = finding.Classification,
                Source = finding.Classification == AntibodyIdClassification.Historical
                    ? AntibodyIdSource.History
                    : AntibodyIdSource.Assist,
                Rationale = finding.Rationale
            }, ct);
        }

        return assist;
    }

    private IReadOnlyList<RuleResult> EvaluateSpecimenForScope(Specimen specimen, bool completing) =>
    [
        AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenUsable(specimen.Status, completing),
        AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenExpiration(specimen.ExpiresUtc, _clock.UtcNow, completing),
        AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenReadiness(specimen.Status, completing)
    ];

    private async Task<long?> ResolveSourceResultIdAsync(long? specimenId, CancellationToken ct)
    {
        if (specimenId is not long sid || _results is null || _testDefinitions is null)
        {
            return null;
        }

        var defs = await _testDefinitions.ListAsync(
            d => d.IsActive && d.ContributesToAntibodyHistory, ct);
        if (defs.Count == 0)
        {
            return null;
        }

        var codes = defs.Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = await _results.ListAsync(r => r.SpecimenId == sid, ct);
        return results
            .Where(r => codes.Contains(r.TestCode) && r.Status != ResultStatus.Invalidated)
            .OrderByDescending(r => r.Id)
            .Select(r => (long?)r.Id)
            .FirstOrDefault();
    }

    private static AntibodyIdFindingDto ToFindingDto(AntibodyIdentificationFinding f) =>
        new(f.Id, f.BloodAttributeDefinitionId, f.Specificity, f.Classification, f.Source, f.Rationale, f.PostedToHistory);

    private async Task<IReadOnlyList<AntibodyIdWorkupListItemDto>> MapWorkupListAsync(
        IReadOnlyList<AntibodyIdentificationWorkup> workups,
        CancellationToken ct)
    {
        var lots = (await _lots.ListAsync(ct)).ToDictionary(l => l.Id);
        var specimenIds = workups.Select(w => w.SpecimenId).OfType<long>().Distinct().ToList();
        var specimens = specimenIds.Count == 0
            ? new Dictionary<long, Specimen>()
            : (await _specimens.ListAsync(s => specimenIds.Contains(s.Id), ct)).ToDictionary(s => s.Id);
        var patientIds = workups.Select(w => w.PatientId).Distinct().ToList();
        var patients = patientIds.Count == 0
            ? new Dictionary<long, Patient>()
            : (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        return workups
            .OrderByDescending(w => w.CreatedUtc)
            .Select(w =>
            {
                lots.TryGetValue(w.PrimaryLotId, out var lot);
                patients.TryGetValue(w.PatientId, out var patient);
                var accession = w.SpecimenId is long sid && specimens.TryGetValue(sid, out var specimen)
                    ? specimen.AccessionNumber
                    : null;
                var name = patient is null ? null : $"{patient.LastName}, {patient.FirstName}";
                return new AntibodyIdWorkupListItemDto(
                    w.Id, w.PatientId, w.SpecimenId, accession, w.PrimaryLotId,
                    lot?.LotNumber ?? "", lot?.PanelName ?? "",
                    w.Status, w.CreatedUtc, w.CreatedBy,
                    patient?.MedicalRecordNumber, name);
            })
            .ToList();
    }

    private static AntibodyPanelLotListItemDto ToLotDto(AntibodyPanelLot lot, string manufacturer, DateOnly today) =>
        new(lot.Id, lot.ManufacturerId, manufacturer, lot.LotNumber, lot.ExpiresOn, lot.PanelName,
            lot.IsSelectedCellLot, lot.IsActive, lot.ExpiresOn < today);

    private async Task<HashSet<long>> AllowedCellIdsAsync(long workupId, CancellationToken ct)
    {
        var lotIds = (await _workupLots.ListAsync(l => l.WorkupId == workupId, ct)).Select(l => l.LotId).ToHashSet();
        return (await _cells.ListAsync(c => lotIds.Contains(c.LotId), ct)).Select(c => c.Id).ToHashSet();
    }

    private async Task<OperationResult<AntibodyIdentificationWorkup>> RequireEditableAsync(long workupId, CancellationToken ct)
    {
        var workup = await _workups.GetByIdAsync(workupId, ct);
        if (workup is null)
        {
            return OperationResult<AntibodyIdentificationWorkup>.Fail("Antibody-identification workup not found.");
        }

        if (workup.Status is AntibodyWorkupStatus.Completed or AntibodyWorkupStatus.Voided)
        {
            return OperationResult<AntibodyIdentificationWorkup>.Fail("Completed or voided workups cannot be edited.");
        }

        var patientGate = await RejectMergedOrMissingPatientOpAsync<AntibodyIdentificationWorkup>(workup.PatientId, ct);
        return patientGate ?? OperationResult<AntibodyIdentificationWorkup>.Ok(workup);
    }

    private DateOnly Today() => DateOnly.FromDateTime(_clock.UtcNow);

    private async Task<EvaluationResult<T>?> RejectMergedOrMissingPatientAsync<T>(long patientId, CancellationToken ct)
    {
        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return EvaluationResult<T>.Fail("Patient not found.");
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        return clinical.Severity == RuleSeverity.HardStop
            ? EvaluationResult<T>.Fail(clinical.Message)
            : null;
    }

    private async Task<OperationResult<T>?> RejectMergedOrMissingPatientOpAsync<T>(long patientId, CancellationToken ct)
    {
        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return OperationResult<T>.Fail("Patient not found.");
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        return clinical.Severity == RuleSeverity.HardStop
            ? OperationResult<T>.Fail(clinical.Message)
            : null;
    }

    private async Task<EvaluationResult<T>?> RejectUnauthorizedAsync<T>(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(_currentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<T>.Fail(auth.Message)
            : null;
    }

    private async Task<OperationResult<T>?> RejectUnauthorizedOpAsync<T>(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(_currentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<T>.Fail(auth.Message)
            : null;
    }
}

file static class AntibodyIdInterpretationItemExtensions
{
    // Placeholder so a future request flag can mark an item as copied from assist.
    public static bool SourceWouldBeAssist(this AntibodyIdInterpretationItem _) => false;
}
