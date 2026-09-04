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
        IAuditWriter? audit = null)
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

        if (duplicate.Status == PatientStatus.Merged && duplicate.MergedIntoPatientId == survivor.Id)
        {
            return OperationResult<Patient>.Ok(survivor, evaluation.Warnings);
        }

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

        await EnsurePriorMrnAliasAsync(survivor.Id, duplicate.MedicalRecordNumber, ct);
        await CombineIdentifiersAsync(survivor.Id, duplicate.Id, ct);
        await CombineAntibodiesAsync(survivor.Id, duplicate.Id, ct);
        await CombineBloodTypesAsync(survivor.Id, duplicate.Id, ct);
        await CombineRequirementsAsync(survivor.Id, duplicate.Id, ct);
        await CombineAntigensAsync(survivor.Id, duplicate.Id, ct);

        await ReassignAsync(_encounters, e => e.PatientId == duplicate.Id, e => e.PatientId = survivor.Id, ct);
        await ReassignAsync(_orders, o => o.PatientId == duplicate.Id, o => o.PatientId = survivor.Id, ct);
        await ReassignAsync(_specimens, s => s.PatientId == duplicate.Id, s => s.PatientId = survivor.Id, ct);
        await ReassignAsync(_allocations, a => a.PatientId == duplicate.Id, a => a.PatientId = survivor.Id, ct);
        await ReassignAsync(_issues, i => i.PatientId == duplicate.Id, i => i.PatientId = survivor.Id, ct);
        await ReassignAsync(_crossmatches, x => x.PatientId == duplicate.Id, x => x.PatientId = survivor.Id, ct);
        await ReassignAsync(_transfusions, t => t.PatientId == duplicate.Id, t => t.PatientId = survivor.Id, ct);
        await ReassignAsync(_reactions, r => r.PatientId == duplicate.Id, r => r.PatientId = survivor.Id, ct);
        await ReassignAsync(_billing, b => b.PatientId == duplicate.Id, b => b.PatientId = survivor.Id, ct);
        await ReassignAsync(_results, r => r.PatientId == duplicate.Id, r => r.PatientId = survivor.Id, ct);
        await ReassignAsync(_units, u => u.ReservedPatientId == duplicate.Id, u => u.ReservedPatientId = survivor.Id, ct);

        _audit?.Record(
            AuditEventType.Merge,
            nameof(Patient),
            survivor.Id,
            oldValue: new { DuplicateId = duplicate.Id, DuplicateMrn = duplicate.MedicalRecordNumber },
            newValue: new { SurvivorId = survivor.Id, SurvivorMrn = survivor.MedicalRecordNumber },
            reason: string.IsNullOrWhiteSpace(reason) ? "Patient records merged." : reason.Trim());

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Patient>.Ok(survivor, evaluation.Warnings);
    }

    private async Task<(AboGroup Abo, RhType Rh)> CurrentTypeAsync(long patientId, CancellationToken ct)
    {
        var current = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == patientId && h.IsCurrent, ct);
        return current is null ? (AboGroup.Unknown, RhType.Unknown) : (current.Abo, current.RhD);
    }

    private async Task EnsurePriorMrnAliasAsync(long survivorId, string priorMrn, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(priorMrn))
        {
            return;
        }

        var value = priorMrn.Trim();
        var exists = await _identifiers.AnyAsync(
            i => i.PatientId == survivorId
                 && i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber
                 && i.Value == value,
            ct);
        if (exists)
        {
            return;
        }

        await _identifiers.AddAsync(new PatientIdentifier
        {
            PatientId = survivorId,
            IdentifierType = IdentityTokenType.PriorMedicalRecordNumber,
            Value = value,
            IsActive = true
        }, ct);
    }

    private async Task CombineIdentifiersAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
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
                continue;
            }

            var tracked = await _identifiers.GetByIdAsync(identifier.Id, ct);
            if (tracked is not null)
            {
                tracked.PatientId = survivorId;
            }
        }
    }

    private async Task CombineAntibodiesAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        var incoming = await _antibodies.ListAsync(a => a.PatientId == duplicateId, ct);
        var existing = await _antibodies.ListAsync(a => a.PatientId == survivorId, ct);
        foreach (var antibody in incoming)
        {
            var already = existing.Any(e =>
                string.Equals(e.AntibodySpecificity, antibody.AntibodySpecificity, StringComparison.OrdinalIgnoreCase)
                && e.IsActive);
            if (already && antibody.IsActive)
            {
                continue;
            }

            var tracked = await _antibodies.GetByIdAsync(antibody.Id, ct);
            if (tracked is not null)
            {
                tracked.PatientId = survivorId;
            }
        }
    }

    private async Task CombineBloodTypesAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        var survivorHasCurrent = await _bloodTypes.AnyAsync(h => h.PatientId == survivorId && h.IsCurrent, ct);
        var incoming = await _bloodTypes.ListAsync(h => h.PatientId == duplicateId, ct);
        foreach (var history in incoming)
        {
            var tracked = await _bloodTypes.GetByIdAsync(history.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            if (survivorHasCurrent && tracked.IsCurrent)
            {
                tracked.IsCurrent = false;
            }

            tracked.PatientId = survivorId;
        }
    }

    private async Task CombineRequirementsAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        var incoming = await _requirements.ListAsync(r => r.PatientId == duplicateId, ct);
        var existing = await _requirements.ListAsync(r => r.PatientId == survivorId, ct);
        foreach (var requirement in incoming)
        {
            var already = existing.Any(e =>
                e.RequirementType == requirement.RequirementType
                && string.Equals(e.AntigenCode, requirement.AntigenCode, StringComparison.OrdinalIgnoreCase)
                && e.IsActive);
            if (already && requirement.IsActive)
            {
                continue;
            }

            var tracked = await _requirements.GetByIdAsync(requirement.Id, ct);
            if (tracked is not null)
            {
                tracked.PatientId = survivorId;
            }
        }
    }

    private async Task CombineAntigensAsync(long survivorId, long duplicateId, CancellationToken ct)
    {
        var incoming = await _antigens.ListAsync(a => a.PatientId == duplicateId, ct);
        var existing = await _antigens.ListAsync(a => a.PatientId == survivorId, ct);
        foreach (var antigen in incoming)
        {
            if (existing.Any(e => e.BloodAttributeDefinitionId == antigen.BloodAttributeDefinitionId))
            {
                continue;
            }

            var tracked = await _antigens.GetByIdAsync(antigen.Id, ct);
            if (tracked is not null)
            {
                tracked.PatientId = survivorId;
            }
        }
    }

    private static async Task ReassignAsync<T>(
        IRepository<T> repository,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate,
        Action<T> assign,
        CancellationToken ct) where T : BaseEntity
    {
        var rows = await repository.ListAsync(predicate, ct);
        foreach (var row in rows)
        {
            var tracked = await repository.GetByIdAsync(row.Id, ct);
            if (tracked is not null)
            {
                assign(tracked);
            }
        }
    }
}
