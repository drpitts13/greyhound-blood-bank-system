using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Patients;

/// <summary>
/// Combines a duplicate patient into a survivor without deleting history
/// (SafeTrace / SoftBank identity merge; AABB unique identification).
/// </summary>
public sealed class PatientMergeService
{
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<PatientIdentifier> _identifiers;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<Encounter> _encounters;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<SpecialTransfusionRequirement> _requirements;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<ReactionInvestigation> _reactions;
    private readonly IRepository<AntigenProfile> _antigens;
    private readonly IRepository<BillingEvent> _billing;
    private readonly IRepository<TestResult> _results;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter? _audit;
    private readonly ICurrentUser? _currentUser;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<AntibodyIdentificationWorkup>? _workups;
    private readonly IRepository<DatElutionWorkup>? _datElutions;
    private readonly IRepository<PtagIdentifier>? _ptags;
    private readonly IRepository<BloodComponentCompatibilityDecision>? _decisions;
    private readonly IRepository<PatientMerge>? _mergeLedger;
    private readonly IRepository<PatientMergeMove>? _mergeMoves;
    private readonly IClock? _clock;

    public PatientMergeService(
        IRepository<Patient> patients,
        IRepository<PatientIdentifier> identifiers,
        IRepository<AntibodyHistory> antibodies,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<Encounter> encounters,
        IRepository<Order> orders,
        IRepository<Specimen> specimens,
        IRepository<Allocation> allocations,
        IRepository<Issue> issues,
        IRepository<Crossmatch> crossmatches,
        IRepository<BloodUnit> units,
        IRepository<SpecialTransfusionRequirement> requirements,
        IRepository<TransfusionEvent> transfusions,
        IRepository<ReactionInvestigation> reactions,
        IRepository<AntigenProfile> antigens,
        IRepository<BillingEvent> billing,
        IRepository<TestResult> results,
        IUnitOfWork unitOfWork,
        IAuditWriter? audit = null,
        ICurrentUser? currentUser = null,
        IPermissionEvaluator? permissions = null,
        IRepository<AntibodyIdentificationWorkup>? workups = null,
        IRepository<DatElutionWorkup>? datElutions = null,
        IRepository<PtagIdentifier>? ptags = null,
        IRepository<BloodComponentCompatibilityDecision>? decisions = null,
        IRepository<PatientMerge>? mergeLedger = null,
        IRepository<PatientMergeMove>? mergeMoves = null,
        IClock? clock = null)
    {
        _patients = patients;
        _identifiers = identifiers;
        _antibodies = antibodies;
        _bloodTypes = bloodTypes;
        _encounters = encounters;
        _orders = orders;
        _specimens = specimens;
        _allocations = allocations;
        _issues = issues;
        _crossmatches = crossmatches;
        _units = units;
        _requirements = requirements;
        _transfusions = transfusions;
        _reactions = reactions;
        _antigens = antigens;
        _billing = billing;
        _results = results;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _permissions = permissions;
        _workups = workups;
        _datElutions = datElutions;
        _ptags = ptags;
        _decisions = decisions;
        _mergeLedger = mergeLedger;
        _mergeMoves = mergeMoves;
        _clock = clock;
    }

    public async Task<Patient?> FindByMrnAsync(string mrn, bool followMerge = true, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mrn))
        {
            return null;
        }

        var trimmed = mrn.Trim();
        var patient = await _patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == trimmed, ct);
        if (patient is null)
        {
            var alias = await _identifiers.FirstOrDefaultAsync(
                i => i.IsActive
                     && i.Value == trimmed
                     && (i.IdentifierType == IdentityTokenType.MedicalRecordNumber
                         || i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber),
                ct);
            if (alias is not null)
            {
                patient = await _patients.GetByIdAsync(alias.PatientId, ct);
            }
        }

        if (patient is null || !followMerge)
        {
            return patient;
        }

        var seen = new HashSet<long>();
        while (patient.MergedIntoPatientId is long survivorId && seen.Add(patient.Id))
        {
            var survivor = await _patients.GetByIdAsync(survivorId, ct);
            if (survivor is null)
            {
                break;
            }

            patient = survivor;
        }

        return patient;
    }

    public async Task<OperationResult<Patient>> MergeAsync(
        long survivorId,
        long duplicateId,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        return await MergeCoreAsync(survivorId, duplicateId, reason, ct);
    }

    /// <summary>
    /// Inbound ADT A18/A40 merge. The interface identity is not a directory user
    /// with <c>patient.merge</c>; workspace merge stays on <see cref="MergeAsync"/>.
    /// </summary>
    public Task<OperationResult<Patient>> MergeFromHl7Async(
        long survivorId,
        long duplicateId,
        string? reason,
        CancellationToken ct = default) =>
        MergeCoreAsync(survivorId, duplicateId, reason, ct);

    private async Task<OperationResult<Patient>> MergeCoreAsync(
        long survivorId,
        long duplicateId,
        string? reason,
        CancellationToken ct)
    {
        var survivor = await _patients.GetByIdAsync(survivorId, ct);
        var duplicate = await _patients.GetByIdAsync(duplicateId, ct);
        if (survivor is null || duplicate is null)
        {
            return OperationResult<Patient>.Fail("Survivor and duplicate patients must both exist.");
        }

        var survivorType = await CurrentTypeAsync(survivor.Id, ct);
        var duplicateType = await CurrentTypeAsync(duplicate.Id, ct);
        var evaluation = new RuleEvaluation(PatientMergeRule.Evaluate(
            survivor.Id,
            duplicate.Id,
            survivor.Status,
            duplicate.Status,
            duplicate.MergedIntoPatientId,
            survivorType.Abo,
            survivorType.Rh,
            duplicateType.Abo,
            duplicateType.Rh));

        if (evaluation.IsHardStopped)
        {
            return OperationResult<Patient>.Fail(evaluation.HardStops[0].Message);
        }

        var workupMerge = await EvaluateMergeWorkupsAsync(survivor.Id, duplicate.Id, ct);
        if (workupMerge.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<Patient>.Fail(workupMerge.Message);
        }

        if (workupMerge.Severity == RuleSeverity.Warning)
        {
            evaluation = new RuleEvaluation(evaluation.Results.Append(workupMerge));
        }

        if (duplicate.Status == PatientStatus.Merged && duplicate.MergedIntoPatientId == survivor.Id)
        {
            return OperationResult<Patient>.Ok(survivor, evaluation.Warnings);
        }

        var deceasedBefore = survivor.Deceased;
        var deceasedUtcBefore = survivor.DeceasedUtc;
        var pregnancyBefore = survivor.RecentPregnancyUtc;

        duplicate.Status = PatientStatus.Merged;
        duplicate.MergedIntoPatientId = survivor.Id;
        if (duplicate.Deceased)
        {
            survivor.Deceased = true;
            survivor.DeceasedUtc ??= duplicate.DeceasedUtc;
        }

        if (duplicate.RecentPregnancyUtc is DateTime pregnancy
            && (survivor.RecentPregnancyUtc is null || pregnancy > survivor.RecentPregnancyUtc))
        {
            survivor.RecentPregnancyUtc = pregnancy;
        }

        _patients.Update(duplicate);
        _patients.Update(survivor);

        var moves = new List<PatientMergeMove>();
        var priorMrn = await EnsurePriorMrnAliasAsync(survivor.Id, duplicate.MedicalRecordNumber, ct);
        moves.AddRange(await CombineIdentifiersAsync(survivor.Id, duplicate.Id, ct));
        moves.AddRange(await CombineAntibodiesAsync(survivor.Id, duplicate.Id, ct));
        moves.AddRange(await CombineBloodTypesAsync(survivor.Id, duplicate.Id, ct));
        moves.AddRange(await CombineRequirementsAsync(survivor.Id, duplicate.Id, ct));
        moves.AddRange(await CombineAntigensAsync(survivor.Id, duplicate.Id, ct));

        moves.AddRange(await ReassignAsync(_encounters, e => e.PatientId == duplicate.Id, e => e.PatientId = survivor.Id, nameof(Encounter), ct));
        moves.AddRange(await ReassignAsync(_orders, o => o.PatientId == duplicate.Id, o => o.PatientId = survivor.Id, nameof(Order), ct));
        moves.AddRange(await ReassignAsync(_specimens, s => s.PatientId == duplicate.Id, s => s.PatientId = survivor.Id, nameof(Specimen), ct));
        moves.AddRange(await ReassignAsync(_allocations, a => a.PatientId == duplicate.Id, a => a.PatientId = survivor.Id, nameof(Allocation), ct));
        moves.AddRange(await ReassignAsync(_issues, i => i.PatientId == duplicate.Id, i => i.PatientId = survivor.Id, nameof(Issue), ct));
        moves.AddRange(await ReassignAsync(_crossmatches, x => x.PatientId == duplicate.Id, x => x.PatientId = survivor.Id, nameof(Crossmatch), ct));
        moves.AddRange(await ReassignAsync(_transfusions, t => t.PatientId == duplicate.Id, t => t.PatientId = survivor.Id, nameof(TransfusionEvent), ct));
        moves.AddRange(await ReassignAsync(_reactions, r => r.PatientId == duplicate.Id, r => r.PatientId = survivor.Id, nameof(ReactionInvestigation), ct));
        moves.AddRange(await ReassignAsync(_billing, b => b.PatientId == duplicate.Id, b => b.PatientId = survivor.Id, nameof(BillingEvent), ct));
        moves.AddRange(await ReassignAsync(_results, r => r.PatientId == duplicate.Id, r => r.PatientId = survivor.Id, nameof(TestResult), ct));
        moves.AddRange(await ReassignAsync(_units, u => u.ReservedPatientId == duplicate.Id, u => u.ReservedPatientId = survivor.Id, nameof(BloodUnit), ct));
        if (_workups is not null)
        {
            moves.AddRange(await ReassignAsync(_workups, w => w.PatientId == duplicate.Id, w => w.PatientId = survivor.Id, nameof(AntibodyIdentificationWorkup), ct));
        }

        if (_datElutions is not null)
        {
            moves.AddRange(await ReassignAsync(_datElutions, w => w.PatientId == duplicate.Id, w => w.PatientId = survivor.Id, nameof(DatElutionWorkup), ct));
        }

        if (_ptags is not null)
        {
            moves.AddRange(await ReassignAsync(_ptags, p => p.PatientId == duplicate.Id, p => p.PatientId = survivor.Id, nameof(PtagIdentifier), ct));
        }

        if (_decisions is not null)
        {
            moves.AddRange(await ReassignAsync(
                _decisions,
                d => d.PatientId == duplicate.Id,
                d => d.PatientId = survivor.Id,
                nameof(BloodComponentCompatibilityDecision),
                ct));
        }

        PatientMerge? ledger = null;
        if (_mergeLedger is not null)
        {
            var now = Now();
            ledger = new PatientMerge
            {
                SurvivorPatientId = survivor.Id,
                DuplicatePatientId = duplicate.Id,
                Status = PatientMergeStatus.Active,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Patient records merged." : reason.Trim(),
                MergedBy = _currentUser?.UserName ?? "system",
                MergedUtc = now,
                SurvivorDeceasedBefore = deceasedBefore,
                SurvivorDeceasedUtcBefore = deceasedUtcBefore,
                SurvivorRecentPregnancyUtcBefore = pregnancyBefore,
                SurvivorDeceasedAfter = survivor.Deceased,
                SurvivorDeceasedUtcAfter = survivor.DeceasedUtc,
                SurvivorRecentPregnancyUtcAfter = survivor.RecentPregnancyUtc,
                Moves = moves
            };
            await _mergeLedger.AddAsync(ledger, ct);
        }

        _audit?.Record(
            AuditEventType.Merge,
            nameof(Patient),
            survivor.Id,
            oldValue: new { DuplicateId = duplicate.Id, DuplicateMrn = duplicate.MedicalRecordNumber },
            newValue: new { SurvivorId = survivor.Id, SurvivorMrn = survivor.MedicalRecordNumber },
            reason: string.IsNullOrWhiteSpace(reason) ? "Patient records merged." : reason.Trim());

        await _unitOfWork.SaveChangesAsync(ct);
        if (ledger is not null)
        {
            if (priorMrn is not null)
            {
                ledger.PriorMrnIdentifierId = priorMrn.Id;
            }

            ledger.MergedUtc = duplicate.ModifiedUtc ?? ledger.CreatedUtc;
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<Patient>.Ok(survivor, evaluation.Warnings);
    }

    /// <summary>
    /// Restores a merged duplicate when the merge ledger is intact and the survivor
    /// has had no new clinical activity since the merge.
    /// </summary>
    public async Task<OperationResult<Patient>> UnmergeAsync(
        long duplicateId,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnmergeUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var duplicate = await _patients.GetByIdAsync(duplicateId, ct);
        if (duplicate is null)
        {
            return OperationResult<Patient>.Fail("Patient not found.");
        }

        var activeLedger = _mergeLedger is null
            ? null
            : await _mergeLedger.FirstOrDefaultAsync(
                m => m.DuplicatePatientId == duplicateId && m.Status == PatientMergeStatus.Active,
                ct);
        var alreadyUnmerged = _mergeLedger is not null
            && activeLedger is null
            && await _mergeLedger.AnyAsync(
                m => m.DuplicatePatientId == duplicateId && m.Status == PatientMergeStatus.Unmerged,
                ct);

        var survivor = activeLedger is null
            ? null
            : await _patients.GetByIdAsync(activeLedger.SurvivorPatientId, ct);
        var moves = activeLedger is null || _mergeMoves is null
            ? Array.Empty<PatientMergeMove>()
            : await _mergeMoves.ListAsync(m => m.PatientMergeId == activeLedger.Id, ct);

        var activity = activeLedger is not null
            && await HasActivitySinceAsync(activeLedger.SurvivorPatientId, activeLedger.MergedUtc, ct);
        var changed = activeLedger is not null && await MovedRowChangedAsync(activeLedger, moves, ct);
        var drifted = activeLedger is not null
            && survivor is not null
            && SurvivorFieldsDrifted(survivor, activeLedger);

        var evaluation = new RuleEvaluation(PatientUnmergeRule.Evaluate(
            reason,
            duplicate.Status,
            ledgerFound: activeLedger is not null,
            ledgerAlreadyUnmerged: alreadyUnmerged,
            activitySinceMerge: activity,
            movedRowChanged: changed,
            survivorFieldDrifted: drifted));
        if (evaluation.IsHardStopped)
        {
            return OperationResult<Patient>.Fail(evaluation.HardStops[0].Message);
        }

        if (activeLedger is null || survivor is null)
        {
            return OperationResult<Patient>.Fail("This patient record is not merged.");
        }

        foreach (var move in moves)
        {
            if (move.Action == PatientMergeMoveAction.SkippedDuplicate)
            {
                continue;
            }

            await RestoreMoveAsync(move, duplicate.Id, ct);
        }

        if (activeLedger.PriorMrnIdentifierId is long priorId)
        {
            var prior = await _identifiers.GetByIdAsync(priorId, ct);
            if (prior is not null)
            {
                prior.IsActive = false;
            }
        }

        survivor.Deceased = activeLedger.SurvivorDeceasedBefore;
        survivor.DeceasedUtc = activeLedger.SurvivorDeceasedUtcBefore;
        survivor.RecentPregnancyUtc = activeLedger.SurvivorRecentPregnancyUtcBefore;
        _patients.Update(survivor);

        duplicate.Status = PatientStatus.Active;
        duplicate.MergedIntoPatientId = null;
        _patients.Update(duplicate);

        activeLedger.Status = PatientMergeStatus.Unmerged;
        activeLedger.UnmergeReason = reason!.Trim();
        activeLedger.UnmergedBy = _currentUser?.UserName ?? "system";
        activeLedger.UnmergedUtc = Now();
        _mergeLedger?.Update(activeLedger);

        _audit?.Record(
            AuditEventType.Unmerge,
            nameof(Patient),
            duplicate.Id,
            oldValue: new
            {
                Status = PatientStatus.Merged,
                SurvivorId = survivor.Id,
                SurvivorMrn = survivor.MedicalRecordNumber,
                DuplicateMrn = duplicate.MedicalRecordNumber
            },
            newValue: new
            {
                Status = PatientStatus.Active,
                SurvivorId = survivor.Id,
                DuplicateId = duplicate.Id
            },
            reason: reason.Trim());

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Patient>.Ok(duplicate);
    }

    private async Task<(AboGroup Abo, RhType Rh)> CurrentTypeAsync(long patientId, CancellationToken ct)
    {
        var current = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == patientId && h.IsCurrent, ct);
        return current is null ? (AboGroup.Unknown, RhType.Unknown) : (current.Abo, current.RhD);
    }

    private async Task<PatientIdentifier?> EnsurePriorMrnAliasAsync(long survivorId, string priorMrn, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(priorMrn))
        {
            return null;
        }

        var value = priorMrn.Trim();
        var exists = await _identifiers.AnyAsync(
            i => i.PatientId == survivorId
                 && i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber
                 && i.Value == value,
            ct);
        if (exists)
        {
            return null;
        }

        var created = new PatientIdentifier
        {
            PatientId = survivorId,
            IdentifierType = IdentityTokenType.PriorMedicalRecordNumber,
            Value = value,
            IsActive = true
        };
        await _identifiers.AddAsync(created, ct);
        return created;
    }

    private async Task<List<PatientMergeMove>> CombineIdentifiersAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        var moves = new List<PatientMergeMove>();
        var incoming = await _identifiers.ListAsync(i => i.PatientId == duplicateId, ct);
        var existing = await _identifiers.ListAsync(i => i.PatientId == survivorId, ct);
        foreach (var identifier in incoming)
        {
            var already = existing.Any(e =>
                e.IdentifierType == identifier.IdentifierType
                && string.Equals(e.Value, identifier.Value, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.AssigningAuthority, identifier.AssigningAuthority, StringComparison.OrdinalIgnoreCase));
            if (already)
            {
                moves.Add(Move(nameof(PatientIdentifier), identifier.Id, PatientMergeMoveAction.SkippedDuplicate));
                continue;
            }

            var tracked = await _identifiers.GetByIdAsync(identifier.Id, ct);
            if (tracked is not null)
            {
                tracked.PatientId = survivorId;
                moves.Add(Move(nameof(PatientIdentifier), tracked.Id, PatientMergeMoveAction.Reassigned));
            }
        }

        return moves;
    }

    private async Task<List<PatientMergeMove>> CombineAntibodiesAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        var moves = new List<PatientMergeMove>();
        var incoming = await _antibodies.ListAsync(a => a.PatientId == duplicateId, ct);
        var existing = await _antibodies.ListAsync(a => a.PatientId == survivorId, ct);
        foreach (var antibody in incoming)
        {
            var already = existing.Any(e =>
                string.Equals(e.AntibodySpecificity, antibody.AntibodySpecificity, StringComparison.OrdinalIgnoreCase)
                && e.IsActive);
            if (already && antibody.IsActive)
            {
                moves.Add(Move(nameof(AntibodyHistory), antibody.Id, PatientMergeMoveAction.SkippedDuplicate));
                continue;
            }

            var tracked = await _antibodies.GetByIdAsync(antibody.Id, ct);
            if (tracked is not null)
            {
                tracked.PatientId = survivorId;
                moves.Add(Move(nameof(AntibodyHistory), tracked.Id, PatientMergeMoveAction.Reassigned));
            }
        }

        return moves;
    }

    private async Task<List<PatientMergeMove>> CombineBloodTypesAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        var moves = new List<PatientMergeMove>();
        var survivorHasCurrent = await _bloodTypes.AnyAsync(h => h.PatientId == survivorId && h.IsCurrent, ct);
        var incoming = await _bloodTypes.ListAsync(h => h.PatientId == duplicateId, ct);
        foreach (var history in incoming)
        {
            var tracked = await _bloodTypes.GetByIdAsync(history.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            var demoted = false;
            if (survivorHasCurrent && tracked.IsCurrent)
            {
                tracked.IsCurrent = false;
                demoted = true;
            }

            tracked.PatientId = survivorId;
            moves.Add(Move(
                nameof(PatientBloodTypeHistory),
                tracked.Id,
                demoted ? PatientMergeMoveAction.BloodTypeDemoted : PatientMergeMoveAction.Reassigned));
        }

        return moves;
    }

    private async Task<List<PatientMergeMove>> CombineRequirementsAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        var moves = new List<PatientMergeMove>();
        var incoming = await _requirements.ListAsync(r => r.PatientId == duplicateId, ct);
        var existing = await _requirements.ListAsync(r => r.PatientId == survivorId, ct);
        foreach (var requirement in incoming)
        {
            var already = existing.Any(e =>
                e.IsActive
                && string.Equals(e.AntigenCode, requirement.AntigenCode, StringComparison.OrdinalIgnoreCase)
                && ((e.RequirementDefinitionId is not null
                        && e.RequirementDefinitionId == requirement.RequirementDefinitionId)
                    || (e.RequirementDefinitionId is null
                        && e.RequirementType == requirement.RequirementType)));
            if (already && requirement.IsActive)
            {
                moves.Add(Move(nameof(SpecialTransfusionRequirement), requirement.Id, PatientMergeMoveAction.SkippedDuplicate));
                continue;
            }

            var tracked = await _requirements.GetByIdAsync(requirement.Id, ct);
            if (tracked is not null)
            {
                tracked.PatientId = survivorId;
                moves.Add(Move(nameof(SpecialTransfusionRequirement), tracked.Id, PatientMergeMoveAction.Reassigned));
            }
        }

        return moves;
    }

    private async Task<List<PatientMergeMove>> CombineAntigensAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        var moves = new List<PatientMergeMove>();
        var incoming = await _antigens.ListAsync(a => a.PatientId == duplicateId, ct);
        var existing = await _antigens.ListAsync(a => a.PatientId == survivorId, ct);
        foreach (var antigen in incoming)
        {
            if (existing.Any(e => e.BloodAttributeDefinitionId == antigen.BloodAttributeDefinitionId))
            {
                moves.Add(Move(nameof(AntigenProfile), antigen.Id, PatientMergeMoveAction.SkippedDuplicate));
                continue;
            }

            var tracked = await _antigens.GetByIdAsync(antigen.Id, ct);
            if (tracked is not null)
            {
                tracked.PatientId = survivorId;
                moves.Add(Move(nameof(AntigenProfile), tracked.Id, PatientMergeMoveAction.Reassigned));
            }
        }

        return moves;
    }

    private async Task<RuleResult> EvaluateMergeWorkupsAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        if (_workups is null)
        {
            return AntibodyIdentificationWorkupScopeRule.EvaluateMergeOpenWorkups(false, false);
        }

        var survivorHasOpen = await HasOpenWorkupAsync(survivorId, ct);
        var duplicateHasOpen = await HasOpenWorkupAsync(duplicateId, ct);
        return AntibodyIdentificationWorkupScopeRule.EvaluateMergeOpenWorkups(survivorHasOpen, duplicateHasOpen);
    }

    private Task<bool> HasOpenWorkupAsync(long patientId, CancellationToken ct) =>
        _workups!.AnyAsync(
            w => w.PatientId == patientId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);

    private static async Task<List<PatientMergeMove>> ReassignAsync<T>(
        IRepository<T> repository,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate,
        Action<T> assign,
        string entityType,
        CancellationToken ct) where T : BaseEntity
    {
        var moves = new List<PatientMergeMove>();
        var rows = await repository.ListAsync(predicate, ct);
        foreach (var row in rows)
        {
            var tracked = await repository.GetByIdAsync(row.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            assign(tracked);
            moves.Add(Move(entityType, tracked.Id, PatientMergeMoveAction.Reassigned));
        }

        return moves;
    }

    private static PatientMergeMove Move(string entityType, long entityId, PatientMergeMoveAction action) =>
        new()
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action
        };

    private DateTime Now() => _clock?.UtcNow ?? DateTime.UtcNow;

    private static bool SurvivorFieldsDrifted(Patient survivor, PatientMerge ledger) =>
        survivor.Deceased != ledger.SurvivorDeceasedAfter
        || survivor.DeceasedUtc != ledger.SurvivorDeceasedUtcAfter
        || survivor.RecentPregnancyUtc != ledger.SurvivorRecentPregnancyUtcAfter;

    private async Task<bool> HasActivitySinceAsync(long survivorId, DateTime mergedUtc, CancellationToken ct)
    {
        if (await _orders.AnyAsync(o => o.PatientId == survivorId && o.CreatedUtc > mergedUtc, ct))
        {
            return true;
        }

        if (await _specimens.AnyAsync(s => s.PatientId == survivorId && s.CreatedUtc > mergedUtc, ct))
        {
            return true;
        }

        if (await _results.AnyAsync(r => r.PatientId == survivorId && r.CreatedUtc > mergedUtc, ct))
        {
            return true;
        }

        if (await _crossmatches.AnyAsync(x => x.PatientId == survivorId && x.CreatedUtc > mergedUtc, ct))
        {
            return true;
        }

        if (await _allocations.AnyAsync(a => a.PatientId == survivorId && a.CreatedUtc > mergedUtc, ct))
        {
            return true;
        }

        if (await _issues.AnyAsync(i => i.PatientId == survivorId && i.CreatedUtc > mergedUtc, ct))
        {
            return true;
        }

        return await _transfusions.AnyAsync(t => t.PatientId == survivorId && t.CreatedUtc > mergedUtc, ct);
    }

    private async Task<bool> MovedRowChangedAsync(
        PatientMerge ledger,
        IReadOnlyList<PatientMergeMove> moves,
        CancellationToken ct)
    {
        foreach (var move in moves)
        {
            if (move.Action == PatientMergeMoveAction.SkippedDuplicate)
            {
                continue;
            }

            if (await RowChangedAsync(move, ledger.SurvivorPatientId, ledger.MergedUtc, ct))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> RowChangedAsync(
        PatientMergeMove move,
        long survivorId,
        DateTime mergedUtc,
        CancellationToken ct)
    {
        return move.EntityType switch
        {
            nameof(Encounter) => await ChangedPatientAsync(_encounters, move.EntityId, survivorId, mergedUtc, e => e.PatientId, ct),
            nameof(Order) => await ChangedPatientAsync(_orders, move.EntityId, survivorId, mergedUtc, o => o.PatientId, ct),
            nameof(Specimen) => await ChangedPatientAsync(_specimens, move.EntityId, survivorId, mergedUtc, s => s.PatientId, ct),
            nameof(Allocation) => await ChangedPatientAsync(_allocations, move.EntityId, survivorId, mergedUtc, a => a.PatientId, ct),
            nameof(Issue) => await ChangedPatientAsync(_issues, move.EntityId, survivorId, mergedUtc, i => i.PatientId, ct),
            nameof(Crossmatch) => await ChangedPatientAsync(_crossmatches, move.EntityId, survivorId, mergedUtc, x => x.PatientId, ct),
            nameof(TransfusionEvent) => await ChangedPatientAsync(_transfusions, move.EntityId, survivorId, mergedUtc, t => t.PatientId, ct),
            nameof(ReactionInvestigation) => await ChangedPatientAsync(_reactions, move.EntityId, survivorId, mergedUtc, r => r.PatientId, ct),
            nameof(BillingEvent) => await ChangedNullablePatientAsync(_billing, move.EntityId, survivorId, mergedUtc, b => b.PatientId, ct),
            nameof(TestResult) => await ChangedPatientAsync(_results, move.EntityId, survivorId, mergedUtc, r => r.PatientId, ct),
            nameof(BloodUnit) => await ChangedReservedAsync(move.EntityId, survivorId, mergedUtc, ct),
            nameof(AntibodyIdentificationWorkup) => _workups is null || await ChangedPatientAsync(_workups, move.EntityId, survivorId, mergedUtc, w => w.PatientId, ct),
            nameof(PatientIdentifier) => await ChangedPatientAsync(_identifiers, move.EntityId, survivorId, mergedUtc, i => i.PatientId, ct),
            nameof(AntibodyHistory) => await ChangedPatientAsync(_antibodies, move.EntityId, survivorId, mergedUtc, a => a.PatientId, ct),
            nameof(PatientBloodTypeHistory) => await ChangedPatientAsync(_bloodTypes, move.EntityId, survivorId, mergedUtc, h => h.PatientId, ct),
            nameof(SpecialTransfusionRequirement) => await ChangedPatientAsync(_requirements, move.EntityId, survivorId, mergedUtc, r => r.PatientId, ct),
            nameof(AntigenProfile) => await ChangedPatientAsync(_antigens, move.EntityId, survivorId, mergedUtc, a => a.PatientId, ct),
            nameof(DatElutionWorkup) => _datElutions is null || await ChangedPatientAsync(_datElutions, move.EntityId, survivorId, mergedUtc, w => w.PatientId, ct),
            nameof(PtagIdentifier) => _ptags is null || await ChangedPatientAsync(_ptags, move.EntityId, survivorId, mergedUtc, p => p.PatientId, ct),
            nameof(BloodComponentCompatibilityDecision) => _decisions is null || await ChangedPatientAsync(_decisions, move.EntityId, survivorId, mergedUtc, d => d.PatientId, ct),
            _ => true
        };
    }

    private static async Task<bool> ChangedPatientAsync<T>(
        IRepository<T> repository,
        long id,
        long survivorId,
        DateTime mergedUtc,
        Func<T, long> patientId,
        CancellationToken ct) where T : BaseEntity
    {
        var row = await repository.GetByIdAsync(id, ct);
        return row is null || patientId(row) != survivorId || row.ModifiedUtc > mergedUtc;
    }

    private static async Task<bool> ChangedNullablePatientAsync<T>(
        IRepository<T> repository,
        long id,
        long survivorId,
        DateTime mergedUtc,
        Func<T, long?> patientId,
        CancellationToken ct) where T : BaseEntity
    {
        var row = await repository.GetByIdAsync(id, ct);
        return row is null || patientId(row) != survivorId || row.ModifiedUtc > mergedUtc;
    }

    private async Task<bool> ChangedReservedAsync(long id, long survivorId, DateTime mergedUtc, CancellationToken ct)
    {
        var row = await _units.GetByIdAsync(id, ct);
        return row is null || row.ReservedPatientId != survivorId || row.ModifiedUtc > mergedUtc;
    }

    private async Task RestoreMoveAsync(PatientMergeMove move, long duplicateId, CancellationToken ct)
    {
        switch (move.EntityType)
        {
            case nameof(Encounter):
                await AssignAsync(_encounters, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(Order):
                await AssignAsync(_orders, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(Specimen):
                await AssignAsync(_specimens, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(Allocation):
                await AssignAsync(_allocations, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(Issue):
                await AssignAsync(_issues, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(Crossmatch):
                await AssignAsync(_crossmatches, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(TransfusionEvent):
                await AssignAsync(_transfusions, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(ReactionInvestigation):
                await AssignAsync(_reactions, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(BillingEvent):
                await AssignAsync(_billing, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(TestResult):
                await AssignAsync(_results, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(BloodUnit):
                await AssignAsync(_units, move.EntityId, row => row.ReservedPatientId = duplicateId, ct);
                break;
            case nameof(AntibodyIdentificationWorkup):
                if (_workups is not null)
                {
                    await AssignAsync(_workups, move.EntityId, row => row.PatientId = duplicateId, ct);
                }
                break;
            case nameof(PatientIdentifier):
                await AssignAsync(_identifiers, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(AntibodyHistory):
                await AssignAsync(_antibodies, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(PatientBloodTypeHistory):
                await AssignAsync(_bloodTypes, move.EntityId, row =>
                {
                    row.PatientId = duplicateId;
                    if (move.Action == PatientMergeMoveAction.BloodTypeDemoted)
                    {
                        row.IsCurrent = true;
                    }
                }, ct);
                break;
            case nameof(SpecialTransfusionRequirement):
                await AssignAsync(_requirements, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(AntigenProfile):
                await AssignAsync(_antigens, move.EntityId, row => row.PatientId = duplicateId, ct);
                break;
            case nameof(DatElutionWorkup):
                if (_datElutions is not null)
                {
                    await AssignAsync(_datElutions, move.EntityId, row => row.PatientId = duplicateId, ct);
                }
                break;
            case nameof(PtagIdentifier):
                if (_ptags is not null)
                {
                    await AssignAsync(_ptags, move.EntityId, row => row.PatientId = duplicateId, ct);
                }
                break;
            case nameof(BloodComponentCompatibilityDecision):
                if (_decisions is not null)
                {
                    await AssignAsync(_decisions, move.EntityId, row => row.PatientId = duplicateId, ct);
                }
                break;
        }
    }

    private static async Task AssignAsync<T>(
        IRepository<T> repository,
        long id,
        Action<T> assign,
        CancellationToken ct) where T : BaseEntity
    {
        var row = await repository.GetByIdAsync(id, ct);
        if (row is not null)
        {
            assign(row);
        }
    }

    private async Task<OperationResult<Patient>?> RejectUnmergeUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var userName = _currentUser?.UserName ?? string.Empty;
        var allowed = await _permissions.HasPermissionAsync(
            userName, PermissionCodes.PatientMerge, ct);
        var auth = PatientUnmergeRule.EvaluatePermission(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<Patient>.Fail(auth.Message)
            : null;
    }

    private async Task<OperationResult<Patient>?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var userName = _currentUser?.UserName ?? string.Empty;
        var allowed = await _permissions.HasPermissionAsync(
            userName, PermissionCodes.PatientMerge, ct);
        var auth = PatientAuthorizationRule.EvaluateMerge(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<Patient>.Fail(auth.Message)
            : null;
    }
}
