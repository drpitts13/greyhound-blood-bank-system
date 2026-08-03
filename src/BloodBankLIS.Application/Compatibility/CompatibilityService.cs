using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compatibility;

/// <summary>
/// Crossmatch recording and unit allocation (reservation). Electronic crossmatch is
/// gated by <see cref="ElectronicCrossmatchEligibilityRule"/>; allocation runs ABO/Rh
/// antigen/antibody compatibility, non-ABORH antigen-negative checks, and the inventory
/// transition guard before reserving a unit to a patient (see docs/workflows.md section 4).
/// </summary>
public sealed class CompatibilityService
{
    /// <summary>Fallback crossmatch validity window when the specimen has no expiry.</summary>
    public const int DefaultCrossmatchValidityHours = 72;

    private readonly IInventoryRepository _inventory;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly BloodAttributeCompatLoader _bloodAttributeCompat;
    private readonly AntibodyScreenCompatLoader _antibodyScreenCompat;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public CompatibilityService(
        IInventoryRepository inventory,
        IRepository<Crossmatch> crossmatches,
        IRepository<Allocation> allocations,
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        IRepository<ProductType> productTypes,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        BloodAttributeCompatLoader bloodAttributeCompat,
        AntibodyScreenCompatLoader antibodyScreenCompat,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser)
    {
        _inventory = inventory;
        _crossmatches = crossmatches;
        _allocations = allocations;
        _patients = patients;
        _specimens = specimens;
        _productTypes = productTypes;
        _bloodTypes = bloodTypes;
        _bloodAttributeCompat = bloodAttributeCompat;
        _antibodyScreenCompat = antibodyScreenCompat;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<EvaluationResult<Crossmatch>> RecordCrossmatchAsync(RecordCrossmatchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unit = await _inventory.GetUnitAsync(request.BloodUnitId, ct);
        if (unit is null)
        {
            return EvaluationResult<Crossmatch>.Fail("Unit not found.");
        }

        if (await _patients.GetByIdAsync(request.PatientId, ct) is null)
        {
            return EvaluationResult<Crossmatch>.Fail("Patient not found.");
        }

        var specimen = await _specimens.GetByIdAsync(request.SpecimenId, ct);
        if (specimen is null)
        {
            return EvaluationResult<Crossmatch>.Fail("Specimen not found.");
        }

        var result = request.Result;

        if (request.Method == CrossmatchMethod.Electronic)
        {
            var currentAboRhConfirmed = await _bloodTypes.AnyAsync(h => h.PatientId == request.PatientId && h.IsCurrent, ct);
            var requiresComplexXm = await _antibodyScreenCompat.RequiresComplexCrossmatchAsync(request.PatientId, ct);
            var screenNegative = request.AntibodyScreenNegative && !await _antibodyScreenCompat.HasPositiveAntibodyScreenAsync(request.PatientId, ct);
            var eligibility = ElectronicCrossmatchEligibilityRule.Evaluate(currentAboRhConfirmed, screenNegative, requiresComplexXm);
            if (eligibility.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<Crossmatch>.Blocked(new RuleEvaluation(new[] { eligibility }));
            }

            result = CrossmatchResult.Compatible;
        }
        else if (result == CrossmatchResult.NotPerformed)
        {
            return EvaluationResult<Crossmatch>.Fail("A serologic crossmatch must record a Compatible or Incompatible result.");
        }

        var crossmatch = new Crossmatch
        {
            BloodProductId = unit.Id,
            PatientId = request.PatientId,
            SpecimenId = specimen.Id,
            Method = request.Method,
            Result = result,
            PerformedUtc = _clock.UtcNow,
            PerformedBy = _currentUser.UserName,
            ExpiresUtc = specimen.ExpiresUtc ?? _clock.UtcNow.AddHours(DefaultCrossmatchValidityHours),
            Comment = request.Comment
        };

        await _crossmatches.AddAsync(crossmatch, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Crossmatch>.Ok(crossmatch);
    }

    public async Task<EvaluationResult<RuleEvaluation>> EvaluateCompatibilityAsync(
        EvaluateCompatibilityRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unit = await _inventory.GetUnitAsync(request.BloodUnitId, ct);
        if (unit is null)
        {
            return EvaluationResult<RuleEvaluation>.Fail("Unit not found.");
        }

        if (await _patients.GetByIdAsync(request.PatientId, ct) is null)
        {
            return EvaluationResult<RuleEvaluation>.Fail("Patient not found.");
        }

        var results = new List<RuleResult>();
        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == request.PatientId && h.IsCurrent, ct);
        var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);

        if (bloodType is not null && productType is not null && unit.BloodType.IsKnown)
        {
            results.AddRange(AboCompatibilityRule.Evaluate(bloodType.BloodType, unit.BloodType, productType.ComponentClass));
        }

        if (productType is not null)
        {
            var bloodAttrs = await _bloodAttributeCompat.LoadAsync(request.PatientId, unit.Id, ct);
            results.AddRange(BloodAttributeCompatibilityRule.Evaluate(
                productType.ComponentClass,
                bloodAttrs.PatientSignificantAntibodies,
                bloodAttrs.PatientAntigens,
                bloodAttrs.UnitSignificantAntibodies,
                bloodAttrs.UnitAntigens));
        }

        var evaluation = new RuleEvaluation(results);
        return EvaluationResult<RuleEvaluation>.Ok(evaluation, evaluation);
    }

    public async Task<EvaluationResult<Allocation>> AllocateUnitAsync(AllocateUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unit = await _inventory.GetUnitAsync(request.BloodUnitId, ct);
        if (unit is null)
        {
            return EvaluationResult<Allocation>.Fail("Unit not found.");
        }

        if (await _patients.GetByIdAsync(request.PatientId, ct) is null)
        {
            return EvaluationResult<Allocation>.Fail("Patient not found.");
        }

        if (await _allocations.AnyAsync(a => a.BloodProductId == unit.Id && a.Status == AllocationStatus.Reserved, ct))
        {
            return EvaluationResult<Allocation>.Fail("Unit already has an active allocation.");
        }

        var targetStatus = UnitStatus.Assigned;
        var results = new List<RuleResult> { InventoryStatusTransition.Evaluate(unit.Status, targetStatus) };

        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == request.PatientId && h.IsCurrent, ct);
        var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        if (bloodType is not null && productType is not null && unit.BloodType.IsKnown)
        {
            results.AddRange(AboCompatibilityRule.Evaluate(bloodType.BloodType, unit.BloodType, productType.ComponentClass));
        }

        if (productType is not null)
        {
            var bloodAttrs = await _bloodAttributeCompat.LoadAsync(request.PatientId, unit.Id, ct);
            results.AddRange(BloodAttributeCompatibilityRule.Evaluate(
                productType.ComponentClass,
                bloodAttrs.PatientSignificantAntibodies,
                bloodAttrs.PatientAntigens,
                bloodAttrs.UnitSignificantAntibodies,
                bloodAttrs.UnitAntigens));
        }

        var evaluation = new RuleEvaluation(results);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<Allocation>.Blocked(evaluation);
        }

        // Antigen-negative mismatches are Warnings (supervisor-overridable); block unless authorized.
        if (evaluation.Warnings.Any(w => w.Code == BloodAttributeCompatibilityRule.AntigenNegCode)
            && !request.AntigenNegOverrideAuthorized)
        {
            return EvaluationResult<Allocation>.Blocked(evaluation);
        }

        var fromStatus = unit.Status;
        unit.Status = targetStatus;

        var allocation = new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = request.PatientId,
            SpecimenId = request.SpecimenId,
            Status = AllocationStatus.Reserved,
            AssignmentType = AssignmentType.Reservation,
            AllocatedUtc = _clock.UtcNow,
            AllocatedBy = _currentUser.UserName,
            ExpiresUtc = request.ExpiresUtc
        };
        await _allocations.AddAsync(allocation, ct);

        _inventory.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = targetStatus,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = "Assigned/allocated to patient",
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow,
            RelatedEntityType = nameof(Allocation)
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Allocation>.Ok(allocation, evaluation);
    }

    public async Task<EvaluationResult<Allocation>> ReleaseAllocationAsync(long allocationId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return EvaluationResult<Allocation>.Fail("A reason is required to release an allocation.");
        }

        var allocation = await _allocations.GetByIdAsync(allocationId, ct);
        if (allocation is null)
        {
            return EvaluationResult<Allocation>.Fail("Allocation not found.");
        }

        if (allocation.Status != AllocationStatus.Reserved)
        {
            return EvaluationResult<Allocation>.Fail($"An allocation with status {allocation.Status} cannot be released.");
        }

        var unit = await _inventory.GetUnitAsync(allocation.BloodProductId, ct);
        if (unit is null)
        {
            return EvaluationResult<Allocation>.Fail("Unit not found.");
        }

        var transition = InventoryStatusTransition.Evaluate(unit.Status, UnitStatus.Available);
        if (transition.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Allocation>.Blocked(new RuleEvaluation(new[] { transition }));
        }

        allocation.Status = AllocationStatus.Released;
        allocation.ReleaseReason = reason;

        var fromStatus = unit.Status;
        unit.Status = UnitStatus.Available;

        _inventory.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = UnitStatus.Available,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = reason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow,
            RelatedEntityType = nameof(Allocation),
            RelatedEntityId = allocation.Id
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Allocation>.Ok(allocation);
    }
}
