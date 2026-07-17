using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// The issue / return / transfusion workflows. <see cref="IssueUnitAsync"/> runs the
/// full <see cref="IssueGate"/> before any unit leaves inventory: HardStops block
/// unconditionally; Warnings require an authorized override (reason + authorizer),
/// which is recorded as an append-only <see cref="Override"/>. Every state change is
/// guarded, written to append-only history, and audited in the same transaction.
/// </summary>
public sealed class IssuingService
{
    private readonly IInventoryRepository _inventory;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Return> _returns;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<Override> _overrides;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly BloodAttributeCompatLoader _bloodAttributeCompat;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;

    public IssuingService(
        IInventoryRepository inventory,
        IRepository<Issue> issues,
        IRepository<Allocation> allocations,
        IRepository<Crossmatch> crossmatches,
        IRepository<Return> returns,
        IRepository<TransfusionEvent> transfusions,
        IRepository<Override> overrides,
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        IRepository<ProductType> productTypes,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        BloodAttributeCompatLoader bloodAttributeCompat,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
        _inventory = inventory;
        _issues = issues;
        _allocations = allocations;
        _crossmatches = crossmatches;
        _returns = returns;
        _transfusions = transfusions;
        _overrides = overrides;
        _patients = patients;
        _specimens = specimens;
        _productTypes = productTypes;
        _bloodTypes = bloodTypes;
        _bloodAttributeCompat = bloodAttributeCompat;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<EvaluationResult<Issue>> IssueUnitAsync(IssueUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unit = await _inventory.GetUnitAsync(request.BloodUnitId, ct);
        if (unit is null)
        {
            return EvaluationResult<Issue>.Fail("Unit not found.");
        }

        if (await _patients.GetByIdAsync(request.PatientId, ct) is null)
        {
            return EvaluationResult<Issue>.Fail("Patient not found.");
        }

        var now = _clock.UtcNow;
        var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == request.PatientId && h.IsCurrent, ct);
        var specimen = await GetCurrentSpecimenAsync(request.PatientId, ct);
        var allocation = await _allocations.FirstOrDefaultAsync(
            a => a.BloodProductId == unit.Id && a.PatientId == request.PatientId && a.Status == AllocationStatus.Reserved, ct);
        var hasValidCrossmatch = await _crossmatches.AnyAsync(
            x => x.BloodProductId == unit.Id && x.PatientId == request.PatientId
                 && x.Result == CrossmatchResult.Compatible
                 && (x.ExpiresUtc == null || x.ExpiresUtc > now), ct);
        var bloodAttrs = await _bloodAttributeCompat.LoadAsync(request.PatientId, unit.Id, ct);

        var context = new IssueGateContext
        {
            IdentityConfirmed = request.IdentityConfirmed,
            SpecimenExists = specimen is not null,
            SpecimenBelongsToPatient = specimen is not null,
            SpecimenExpiresUtc = specimen?.ExpiresUtc,
            PatientBloodTypeKnown = bloodType is not null && bloodType.BloodType.IsKnown,
            PatientAboRh = bloodType?.BloodType ?? new AboRh(AboGroup.Unknown, RhType.Unknown),
            UnitAboRh = unit.BloodType,
            ComponentClass = productType?.ComponentClass ?? ComponentClass.Other,
            UnitStatus = unit.Status,
            UnitExpiresUtc = unit.ExpiresUtc,
            AllocatedToThisPatient = allocation is not null,
            RequiresCrossmatch = productType?.RequiresCrossmatch ?? false,
            HasValidCrossmatch = hasValidCrossmatch,
            IsEmergencyRelease = request.IssueType == IssueType.EmergencyRelease,
            ProductTypeMatchesOrder = request.ProductMatchesOrder,
            PatientSignificantAntibodies = bloodAttrs.PatientSignificantAntibodies,
            PatientAntigens = bloodAttrs.PatientAntigens,
            UnitSignificantAntibodies = bloodAttrs.UnitSignificantAntibodies,
            UnitAntigens = bloodAttrs.UnitAntigens,
            SpecialRequirementsMet = request.SpecialRequirementsMet,
            UnresolvedAboRhDiscrepancy = request.UnresolvedAboRhDiscrepancy,
            NowUtc = now
        };

        var evaluation = IssueGate.Evaluate(context);

        if (evaluation.IsHardStopped)
        {
            // Document the blocked attempt; never silently drop it.
            _audit.Record(
                AuditEventType.Issue,
                nameof(BloodUnit),
                unit.Id,
                newValue: new { Blocked = true, HardStops = evaluation.HardStops.Select(r => r.Code) },
                reason: "Issue blocked by safety gate.");
            await _unitOfWork.SaveChangesAsync(ct);
            return EvaluationResult<Issue>.Blocked(evaluation);
        }

        Override? authorizedOverride = null;
        if (evaluation.RequiresOverride)
        {
            if (string.IsNullOrWhiteSpace(request.OverrideReason) || string.IsNullOrWhiteSpace(request.AuthorizedBy))
            {
                return EvaluationResult<Issue>.Blocked(evaluation);
            }

            authorizedOverride = new Override
            {
                Action = request.IssueType == IssueType.EmergencyRelease ? OverrideAction.EmergencyRelease : OverrideAction.WarningOverride,
                ContextType = nameof(Issue),
                ContextId = unit.Id,
                RuleCode = string.Join(",", evaluation.Warnings.Select(r => r.Code)),
                Reason = request.OverrideReason,
                AuthorizedBy = request.AuthorizedBy,
                OverriddenUtc = now
            };
            await _overrides.AddAsync(authorizedOverride, ct);
        }

        var fromStatus = unit.Status;
        unit.Status = UnitStatus.Issued;

        var issue = new Issue
        {
            AllocationId = allocation?.Id,
            BloodProductId = unit.Id,
            PatientId = request.PatientId,
            IssuedTo = request.IssuedTo,
            IssuedToLocation = request.IssuedToLocation,
            IssuedUtc = now,
            IssuedBy = _currentUser.UserName,
            IssueType = request.IssueType,
            Override = authorizedOverride,
            Status = IssueStatus.Issued
        };
        await _issues.AddAsync(issue, ct);

        if (allocation is not null)
        {
            allocation.Status = AllocationStatus.Consumed;
        }

        _inventory.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = UnitStatus.Issued,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = request.IssueType == IssueType.EmergencyRelease ? "Emergency release" : "Issued to patient",
            ChangedBy = _currentUser.UserName,
            ChangedUtc = now,
            RelatedEntityType = nameof(Issue)
        });

        _audit.Record(
            AuditEventType.Issue,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new { Status = UnitStatus.Issued, request.IssueType, request.PatientId },
            reason: authorizedOverride?.Reason);

        if (authorizedOverride is not null)
        {
            _audit.Record(
                AuditEventType.Override,
                nameof(Issue),
                unit.Id,
                newValue: new { authorizedOverride.RuleCode, authorizedOverride.Action },
                reason: authorizedOverride.Reason);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Issue>.Ok(issue, evaluation);
    }

    public async Task<EvaluationResult<Return>> ReturnUnitAsync(long issueId, ReturnUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return EvaluationResult<Return>.Fail("A reason is required to return a unit.");
        }

        var issue = await _issues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return EvaluationResult<Return>.Fail("Issue not found.");
        }

        if (issue.Status != IssueStatus.Issued)
        {
            return EvaluationResult<Return>.Fail($"An issue with status {issue.Status} cannot be returned.");
        }

        var unit = await _inventory.GetUnitAsync(issue.BloodProductId, ct);
        if (unit is null)
        {
            return EvaluationResult<Return>.Fail("Unit not found.");
        }

        var now = _clock.UtcNow;
        var destination = request.ReissueEligible ? UnitStatus.Available : UnitStatus.Quarantine;

        var returnRecord = new Return
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            ReturnedUtc = now,
            ReturnedBy = _currentUser.UserName,
            Reason = request.Reason,
            ReissueEligible = request.ReissueEligible
        };
        await _returns.AddAsync(returnRecord, ct);

        issue.Status = IssueStatus.Returned;

        // Issued -> Returned (transient) -> Available/Quarantine, recording each step.
        AppendStatus(unit, UnitStatus.Returned, request.Reason, now, nameof(Return), issue.Id);
        if (destination == UnitStatus.Quarantine)
        {
            unit.QuarantineReason = request.Reason;
        }

        AppendStatus(unit, destination, request.Reason, now, nameof(Return), issue.Id);

        _audit.Record(
            AuditEventType.Return,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Issued },
            newValue: new { Status = destination, request.ReissueEligible },
            reason: request.Reason);

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Return>.Ok(returnRecord);
    }

    public async Task<EvaluationResult<TransfusionEvent>> DocumentTransfusionAsync(long issueId, DocumentTransfusionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.FinalDisposition == TransfusionDisposition.Returned)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Use the return workflow to return an unused unit to inventory.");
        }

        var issue = await _issues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Issue not found.");
        }

        if (issue.Status != IssueStatus.Issued)
        {
            return EvaluationResult<TransfusionEvent>.Fail($"An issue with status {issue.Status} cannot be transfused.");
        }

        var unit = await _inventory.GetUnitAsync(issue.BloodProductId, ct);
        if (unit is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Unit not found.");
        }

        var now = _clock.UtcNow;

        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = issue.PatientId,
            StartUtc = request.StartUtc,
            StopUtc = request.StopUtc,
            VolumeTransfused = request.VolumeTransfused,
            Transfusionist = request.Transfusionist,
            ReactionSuspected = request.ReactionSuspected,
            FinalDisposition = request.FinalDisposition,
            DocumentedBy = _currentUser.UserName
        };
        await _transfusions.AddAsync(transfusion, ct);

        issue.Status = IssueStatus.Transfused;
        AppendStatus(unit, UnitStatus.Transfused, $"Transfusion {request.FinalDisposition}", now, nameof(TransfusionEvent), issue.Id);

        _audit.Record(
            AuditEventType.Update,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Issued },
            newValue: new { Status = UnitStatus.Transfused, request.FinalDisposition, request.ReactionSuspected });

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransfusionEvent>.Ok(transfusion);
    }

    private async Task<Specimen?> GetCurrentSpecimenAsync(long patientId, CancellationToken ct)
    {
        var accepted = await _specimens.ListAsync(s => s.PatientId == patientId && s.Status == SpecimenStatus.Accepted, ct);
        return accepted.OrderByDescending(s => s.CollectedUtc).FirstOrDefault();
    }

    private void AppendStatus(BloodUnit unit, UnitStatus toStatus, string reason, DateTime whenUtc, string relatedType, long relatedId)
    {
        var fromStatus = unit.Status;
        unit.Status = toStatus;
        _inventory.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = reason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = whenUtc,
            RelatedEntityType = relatedType,
            RelatedEntityId = relatedId
        });
    }
}
