using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

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
    private readonly IRepository<Issue>? _issues;
    private readonly IAuditWriter? _audit;
    private readonly FacilityPolicyService? _policy;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<AntibodyIdentificationWorkup>? _workups;
    private readonly CrossmatchSettingsReader? _crossmatchSettings;
    private readonly IRepository<ProductRetypeResult>? _retypes;
    private readonly IRepository<ExceptionDefinition>? _exceptionDefinitions;
    private readonly IRepository<Override>? _overrides;
    private readonly AlertService? _alerts;
    private readonly IRepository<Deviation>? _deviations;
    private readonly IRepository<ProductAboCompatibility>? _aboCompatibilities;
    private readonly IRepository<ProductRhCompatibility>? _rhCompatibilities;
    private readonly IRepository<SpecialTransfusionRequirement>? _specialRequirements;
    private readonly IRepository<SpecialRequirementDefinition>? _requirementDefinitions;
    private readonly IRepository<ProductAttributeAssignment>? _productAttributeAssignments;
    private readonly IRepository<ProductAttribute>? _productAttributes;
    private readonly IRepository<BloodComponentSpecialTest>? _unitSpecialTests;

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
        ICurrentUser currentUser,
        IRepository<Issue>? issues = null,
        IAuditWriter? audit = null,
        FacilityPolicyService? policy = null,
        IPermissionEvaluator? permissions = null,
        IRepository<AntibodyIdentificationWorkup>? workups = null,
        CrossmatchSettingsReader? crossmatchSettings = null,
        IRepository<ProductRetypeResult>? retypes = null,
        IRepository<ExceptionDefinition>? exceptionDefinitions = null,
        IRepository<Override>? overrides = null,
        IRepository<ProductAboCompatibility>? aboCompatibilities = null,
        IRepository<ProductRhCompatibility>? rhCompatibilities = null,
        AlertService? alerts = null,
        IRepository<Deviation>? deviations = null,
        IRepository<SpecialTransfusionRequirement>? specialRequirements = null,
        IRepository<SpecialRequirementDefinition>? requirementDefinitions = null,
        IRepository<ProductAttributeAssignment>? productAttributeAssignments = null,
        IRepository<ProductAttribute>? productAttributes = null,
        IRepository<BloodComponentSpecialTest>? unitSpecialTests = null)
    {
        _specialRequirements = specialRequirements;
        _requirementDefinitions = requirementDefinitions;
        _productAttributeAssignments = productAttributeAssignments;
        _productAttributes = productAttributes;
        _unitSpecialTests = unitSpecialTests;
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
        _issues = issues;
        _deviations = deviations;
        _audit = audit;
        _policy = policy;
        _permissions = permissions;
        _workups = workups;
        _crossmatchSettings = crossmatchSettings;
        _retypes = retypes;
        _exceptionDefinitions = exceptionDefinitions;
        _overrides = overrides;
        _aboCompatibilities = aboCompatibilities;
        _rhCompatibilities = rhCompatibilities;
        _alerts = alerts;
    }

    public Task<EvaluationResult<Crossmatch>> RecordCrossmatchAsync(
        RecordCrossmatchRequest request,
        CancellationToken ct = default) =>
        RecordCrossmatchCoreAsync(request, skipPermission: false, orderId: null, encounterId: null, ct);

    /// <summary>
    /// System electronic XM after an authorized allocation. Eligibility is still
    /// re-checked immediately before insert.
    /// </summary>
    public Task<EvaluationResult<Crossmatch>> RecordElectronicCrossmatchSystemAsync(
        RecordCrossmatchRequest request,
        long? orderId,
        long? encounterId,
        CancellationToken ct = default) =>
        RecordCrossmatchCoreAsync(request, skipPermission: true, orderId, encounterId, ct);

    private async Task<EvaluationResult<Crossmatch>> RecordCrossmatchCoreAsync(
        RecordCrossmatchRequest request,
        bool skipPermission,
        long? orderId,
        long? encounterId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!skipPermission)
        {
            var denied = await RejectUnauthorizedAsync<Crossmatch>(
                PermissionCodes.CompatibilityCrossmatch,
                CompatibilityAuthorizationRule.EvaluateCrossmatch,
                ct);
            if (denied is not null)
            {
                return denied;
            }
        }

        var unit = await _inventory.GetUnitAsync(request.BloodUnitId, ct);
        if (unit is null)
        {
            return EvaluationResult<Crossmatch>.Fail("Unit not found.");
        }

        var xmPatient = await RejectMergedPatientAsync<Crossmatch>(request.PatientId, ct);
        if (xmPatient is not null)
        {
            return xmPatient;
        }

        var specimen = await _specimens.GetByIdAsync(request.SpecimenId, ct);
        if (specimen is null)
        {
            return EvaluationResult<Crossmatch>.Fail("Specimen not found.");
        }

        var retroIssue = await OpenRetrospectiveIssueAsync(unit.Id, request.PatientId, ct);
        var specimenCheck = CrossmatchSpecimenRule.Evaluate(
            specimen.AccessionNumber,
            specimen.ExpiresUtc,
            _clock.UtcNow,
            retroIssue is not null && CrossmatchSpecimenRule.IsPreTransfusionSpecimen(specimen.CollectedUtc, retroIssue.IssuedUtc));
        if (specimenCheck.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Crossmatch>.Blocked(new RuleEvaluation([specimenCheck]));
        }

        if (specimenCheck.Severity == RuleSeverity.Warning)
        {
            var refused = await EvaluateExpiredSpecimenExceptionAsync(specimenCheck, request, ct);
            if (refused is not null)
            {
                return EvaluationResult<Crossmatch>.Blocked(refused);
            }
        }

        var result = request.Result;

        if (request.Method == CrossmatchMethod.Electronic)
        {
            var blocked = await EvaluateElectronicEligibilityAsync(request.PatientId, request.SpecimenId, request.AntibodyScreenNegative, ct);
            if (blocked is not null)
            {
                return EvaluationResult<Crossmatch>.Blocked(new RuleEvaluation([blocked]));
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
            ClinicalStatus = CrossmatchValidityRule.ClinicalStatusFor(result),
            PerformedUtc = _clock.UtcNow,
            PerformedBy = _currentUser.UserName,
            ExpiresUtc = specimen.ExpiresUtc ?? _clock.UtcNow.AddHours(DefaultCrossmatchValidityHours),
            Comment = request.Comment,
            OrderId = orderId,
            EncounterId = encounterId
        };

        if (request.Method == CrossmatchMethod.Electronic)
        {
            var raced = await EvaluateElectronicEligibilityAsync(request.PatientId, request.SpecimenId, request.AntibodyScreenNegative, ct);
            if (raced is not null)
            {
                return EvaluationResult<Crossmatch>.Blocked(new RuleEvaluation([raced]));
            }
        }

        await _crossmatches.AddAsync(crossmatch, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit?.Record(
            AuditEventType.Crossmatch,
            nameof(Crossmatch),
            crossmatch.Id,
            newValue: new { crossmatch.BloodProductId, crossmatch.PatientId, crossmatch.Method, crossmatch.Result },
            reason: request.Method == CrossmatchMethod.Electronic
                ? "Electronic crossmatch recorded."
                : "Serologic crossmatch recorded.");
        if (specimenCheck.Severity == RuleSeverity.Warning)
        {
            await _overrides!.AddAsync(new Override
            {
                Action = OverrideAction.WarningOverride,
                ContextType = nameof(Crossmatch),
                ContextId = crossmatch.Id,
                RuleCode = CrossmatchSpecimenRule.RetrospectiveCode,
                Reason = request.OverrideReason!.Trim(),
                AuthorizedBy = request.AuthorizedBy!.Trim(),
                OverriddenUtc = _clock.UtcNow
            }, ct);
            _audit?.Record(
                AuditEventType.Override,
                nameof(Crossmatch),
                crossmatch.Id,
                newValue: new { crossmatch.BloodProductId, crossmatch.PatientId, crossmatch.SpecimenId, RuleCode = CrossmatchSpecimenRule.RetrospectiveCode },
                reason: $"Retrospective crossmatch on expired pre-transfusion specimen {specimen.AccessionNumber}: {request.OverrideReason!.Trim()} (authorized by {request.AuthorizedBy!.Trim()}).");
        }

        await _unitOfWork.SaveChangesAsync(ct);
        var retroIncompatible = await TryCloseRetrospectiveCrossmatchAsync(crossmatch, unit, ct);

        var notices = new List<RuleResult>();
        if (specimenCheck.Severity == RuleSeverity.Warning)
        {
            notices.Add(RuleResult.Warning(
                CrossmatchSpecimenRule.RetrospectiveCode,
                $"Recorded as an exception on expired pre-transfusion specimen {specimen.AccessionNumber}. The override goes to supervisor review."));
        }

        if (retroIncompatible is not null)
        {
            notices.Add(retroIncompatible);
        }

        if (request.Method != CrossmatchMethod.Electronic)
        {
            var xmOpen = await EvaluateSerologicOpenWorkupAsync(request.PatientId, ct);
            if (xmOpen.Severity != RuleSeverity.Pass)
            {
                notices.Add(xmOpen);
            }
        }

        return EvaluationResult<Crossmatch>.Ok(crossmatch, notices.Count == 0 ? null : new RuleEvaluation(notices));
    }

    /// <summary>The issue still waiting for a retrospective crossmatch for this unit and patient, if any.</summary>
    private async Task<Issue?> OpenRetrospectiveIssueAsync(long unitId, long patientId, CancellationToken ct) =>
        _issues is null
            ? null
            : await _issues.FirstOrDefaultAsync(i =>
                i.BloodProductId == unitId
                && i.PatientId == patientId
                && i.TestsIncompleteAtIssue
                && i.RetrospectiveCrossmatchCompletedUtc == null
                && i.Status != IssueStatus.Returned, ct);

    /// <summary>
    /// OCD-050: the exception needs a reason, the authorizer, and an active exception definition
    /// the user may use. Returns the refusal, or null when the crossmatch may be recorded.
    /// </summary>
    private async Task<RuleEvaluation?> EvaluateExpiredSpecimenExceptionAsync(
        RuleResult warning,
        RecordCrossmatchRequest request,
        CancellationToken ct)
    {
        var evaluation = new RuleEvaluation([warning]);
        if (string.IsNullOrWhiteSpace(request.OverrideReason) || string.IsNullOrWhiteSpace(request.AuthorizedBy))
        {
            return OverrideFieldsRule.WithMissing(evaluation, request.OverrideReason, request.AuthorizedBy);
        }

        if (_permissions is null || _exceptionDefinitions is null || _overrides is null)
        {
            return new RuleEvaluation([RuleResult.HardStop(
                "EXC-DEF-MISSING",
                "The expired-specimen exception cannot be authorized here.")]);
        }

        var definition = await _exceptionDefinitions.FirstOrDefaultAsync(
            e => e.RuleCode == CrossmatchSpecimenRule.RetrospectiveCode && e.IsActive, ct);
        var userLevel = await _permissions.GetMaxSecurityLevelAsync(_currentUser.UserName, ct);
        var access = ExceptionOverridePolicy.EvaluateAccess(userLevel, definition, CrossmatchSpecimenRule.RetrospectiveCode);
        return access.Severity == RuleSeverity.HardStop ? new RuleEvaluation([access]) : null;
    }

    /// <summary>
    /// Completes an emergency/MTP follow-up when a post-issue compatible XM is recorded.
    /// Incompatible results stay on the worklist with an updated clinical status, raise a critical
    /// alert, and return a warning for the person who recorded the crossmatch.
    /// </summary>
    private async Task<RuleResult?> TryCloseRetrospectiveCrossmatchAsync(Crossmatch xm, BloodUnit unit, CancellationToken ct)
    {
        if (_issues is null)
        {
            return null;
        }

        var open = await _issues.FirstOrDefaultAsync(i =>
            i.BloodProductId == xm.BloodProductId
            && i.PatientId == xm.PatientId
            && i.TestsIncompleteAtIssue
            && i.RetrospectiveCrossmatchCompletedUtc == null
            && i.Status != IssueStatus.Returned
            && i.IssuedUtc <= xm.PerformedUtc, ct);

        if (open is null)
        {
            return null;
        }

        RuleResult? notice = null;
        if (xm.Result == CrossmatchResult.Compatible)
        {
            open.CrossmatchStatus = CrossmatchClinicalStatus.Compatible;
            open.RetrospectiveCrossmatchCompletedUtc = xm.PerformedUtc;
            open.RetrospectiveCrossmatchId = xm.Id;
            _issues.Update(open);
            _audit?.Record(
                AuditEventType.Crossmatch,
                nameof(Issue),
                open.Id,
                newValue: new { xm.Id, xm.Result },
                reason: "Retrospective crossmatch completed.");
        }
        else if (xm.Result == CrossmatchResult.Incompatible)
        {
            open.CrossmatchStatus = CrossmatchClinicalStatus.Incompatible;
            _issues.Update(open);
            _audit?.Record(
                AuditEventType.Crossmatch,
                nameof(Issue),
                open.Id,
                newValue: new { xm.Id, xm.Result },
                reason: "Retrospective crossmatch incompatible; follow-up remains open.");
            notice = RetrospectiveCrossmatchIncompatibleRule.Warning(unit.UnitNumber, unit.Status);
        }
        else
        {
            return null;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        if (notice is not null)
        {
            var patient = await _patients.GetByIdAsync(open.PatientId, ct);
            var name = patient is null ? null : string.Join(", ", new[] { patient.LastName, patient.FirstName }.Where(n => !string.IsNullOrWhiteSpace(n)));
            var deviationId = await OpenRetrospectiveIncompatibleDeviationAsync(open, xm, unit, patient?.MedicalRecordNumber, name, ct);
            notice = RetrospectiveCrossmatchIncompatibleRule.Warning(unit.UnitNumber, unit.Status, deviationId);
            if (_alerts is not null)
            {
                await _alerts.RaiseAsync(RetrospectiveCrossmatchIncompatibleRule.Alert(
                    open.Id, patient?.MedicalRecordNumber, name, unit.UnitNumber, unit.Status, deviationId), ct);
            }
        }

        return notice;
    }

    /// <summary>
    /// OCD-046: an incompatible retrospective crossmatch requires a deviation. One is opened per issue; a repeat
    /// incompatible crossmatch on the same issue reuses it.
    /// </summary>
    private async Task<long?> OpenRetrospectiveIncompatibleDeviationAsync(
        Issue issue, Crossmatch xm, BloodUnit unit, string? mrn, string? patientName, CancellationToken ct)
    {
        if (_deviations is null)
        {
            return null;
        }

        var existing = await _deviations.FirstOrDefaultAsync(
            d => d.ContextType == RetrospectiveCrossmatchIncompatibleRule.DeviationContextType && d.ContextId == issue.Id, ct);
        if (existing is not null)
        {
            return existing.Id;
        }

        var deviation = new Deviation
        {
            Title = RetrospectiveCrossmatchIncompatibleRule.DeviationTitle(unit.UnitNumber),
            Description = RetrospectiveCrossmatchIncompatibleRule.DeviationDescription(
                issue.Id, mrn, patientName, unit.UnitNumber, unit.Status, xm.Id),
            Severity = RetrospectiveCrossmatchIncompatibleRule.DeviationSeverityLevel,
            Status = DeviationStatus.Open,
            ContextType = RetrospectiveCrossmatchIncompatibleRule.DeviationContextType,
            ContextId = issue.Id,
            ReportedBy = _currentUser.UserName,
            ReportedUtc = _clock.UtcNow
        };
        await _deviations.AddAsync(deviation, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit?.Record(
            AuditEventType.Deviation,
            nameof(Deviation),
            deviation.Id,
            newValue: new { deviation.Title, deviation.Severity, deviation.ContextType, deviation.ContextId, CrossmatchId = xm.Id },
            reason: "Incompatible retrospective crossmatch after emergency release (OCD-046).");
        await _unitOfWork.SaveChangesAsync(ct);
        return deviation.Id;
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

        var evalPatient = await RejectMergedPatientAsync<RuleEvaluation>(request.PatientId, ct);
        if (evalPatient is not null)
        {
            return evalPatient;
        }

        var results = new List<RuleResult>();
        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == request.PatientId && h.IsCurrent, ct);
        var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);

        if (bloodType is not null && productType is not null && unit.BloodType.IsKnown)
        {
            results.AddRange(await EvaluateAboRhAsync(
                request.PatientId, bloodType.BloodType, unit.BloodType, productType, ct));
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

            // The same check as the chart's Allocate panel and issue: a unit that cannot be issued to this patient
            // is not reserved for them either.
            results.AddRange((await UnmetSpecialRequirementsAsync(request.PatientId, unit, productType, bloodAttrs.UnitAntigens, ct))
                .Select(message => RuleResult.HardStop(SpecialTransfusionRequirementRule.Code, message)));
        }

        await AppendAllocateOpenWorkupAsync(results, request.PatientId, ct);

        var evaluation = new RuleEvaluation(results);
        return EvaluationResult<RuleEvaluation>.Ok(evaluation, evaluation);
    }

    public async Task<EvaluationResult<Allocation>> AllocateUnitAsync(AllocateUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<Allocation>(
            PermissionCodes.CompatibilityAllocate,
            CompatibilityAuthorizationRule.EvaluateAllocate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _inventory.GetUnitAsync(request.BloodUnitId, ct);
        if (unit is null)
        {
            return EvaluationResult<Allocation>.Fail("Unit not found.");
        }

        var allocPatient = await RejectMergedPatientAsync<Allocation>(request.PatientId, ct);
        if (allocPatient is not null)
        {
            return allocPatient;
        }

        var autoDir = AutologousDirectedRule.EvaluateIssue(
            unit.DonationRestriction, unit.ReservedPatientId, request.PatientId);
        if (autoDir.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Allocation>.Blocked(new RuleEvaluation([autoDir]));
        }

        if (await _allocations.AnyAsync(a => a.BloodProductId == unit.Id && a.Status == AllocationStatus.Reserved, ct))
        {
            return EvaluationResult<Allocation>.Fail("Unit already has an active allocation.");
        }

        var targetStatus = UnitStatus.Assigned;
        var results = new List<RuleResult> { InventoryStatusTransition.Evaluate(unit.Status, targetStatus) };

        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == request.PatientId && h.IsCurrent, ct);
        var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        var retypeConfirmed = await ProductRetypeConfirmation.IsConfirmedAsync(_retypes, productType, unit.Id, ct);
        var retypeGate = ProductRetypeAllocationRule.Evaluate(productType?.RequiresRetype == true, retypeConfirmed);
        if (retypeGate.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Allocation>.Blocked(new RuleEvaluation([retypeGate]));
        }

        if (bloodType is not null && productType is not null && unit.BloodType.IsKnown)
        {
            results.AddRange(await EvaluateAboRhAsync(
                request.PatientId, bloodType.BloodType, unit.BloodType, productType, ct));
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

            // The same check as the chart's Allocate panel and issue: a unit that cannot be issued to this patient
            // is not reserved for them either.
            results.AddRange((await UnmetSpecialRequirementsAsync(request.PatientId, unit, productType, bloodAttrs.UnitAntigens, ct))
                .Select(message => RuleResult.HardStop(SpecialTransfusionRequirementRule.Code, message)));
        }

        await AppendAllocateOpenWorkupAsync(results, request.PatientId, ct);

        var evaluation = new RuleEvaluation(results);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<Allocation>.Blocked(evaluation);
        }

        var antigenNegWarnings = evaluation.Warnings
            .Where(w => w.Code == BloodAttributeCompatibilityRule.AntigenNegCode)
            .ToList();
        if (antigenNegWarnings.Count > 0)
        {
            var antigenBlock = await AuthorizeAntigenNegOverrideAsync(evaluation, request, ct);
            if (antigenBlock is not null)
            {
                return EvaluationResult<Allocation>.Blocked(antigenBlock);
            }
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

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (InventoryConcurrency.AsFailure<Allocation>(ex) is { } conflict)
        {
            return conflict;
        }

        if (antigenNegWarnings.Count > 0 && _overrides is not null)
        {
            await _overrides.AddAsync(new Override
            {
                Action = OverrideAction.WarningOverride,
                ContextType = nameof(Allocation),
                ContextId = allocation.Id,
                RuleCode = BloodAttributeCompatibilityRule.AntigenNegCode,
                Reason = request.OverrideReason!.Trim(),
                AuthorizedBy = request.AuthorizedBy!.Trim(),
                OverriddenUtc = _clock.UtcNow
            }, ct);
        }

        _audit?.Record(
            AuditEventType.Assignment,
            nameof(Allocation),
            allocation.Id,
            oldValue: new { Status = fromStatus, unit.UnitNumber, unit.Din, BloodProductId = unit.Id },
            newValue: new
            {
                unit.Status,
                allocation.PatientId,
                allocation.AssignmentType,
                unit.UnitNumber,
                unit.Din,
                BloodProductId = unit.Id
            },
            reason: "Unit assigned to patient.");
        await _unitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<Allocation>.Ok(allocation, evaluation);
    }

    private async Task<IReadOnlyList<string>> UnmetSpecialRequirementsAsync(
        long patientId,
        BloodUnit unit,
        ProductType productType,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        CancellationToken ct)
    {
        var requirements = await SpecialRequirementLoader.ForPatientAsync(_specialRequirements, _requirementDefinitions, patientId, ct);
        if (requirements.Count == 0)
        {
            return [];
        }

        var attributesByProduct = await SpecialRequirementLoader.ProductAttributeCodesByProductTypeAsync(
            _productAttributeAssignments, _productAttributes, ct);
        var negativeTests = await SpecialRequirementLoader.NegativeTestCodesByUnitAsync(_unitSpecialTests, [unit.Id], ct);
        return SpecialTransfusionRequirementRule.UnmetUnitRequirements(
            requirements,
            attributesByProduct.GetValueOrDefault(unit.ProductTypeId) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            unitAntigens,
            _clock.UtcNow,
            SpecialRequirementLoader.Typing(unit, productType.ComponentClass, negativeTests));
    }

    /// <summary>
    /// Releases several reservations with one reason, each through <see cref="ReleaseAllocationAsync"/> (its own checks and
    /// audit); one that fails is reported and does not stop the others.
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<ReleaseAllocationOutcomeDto>>> ReleaseAllocationsAsync(
        ReleaseAllocationsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return OperationResult<IReadOnlyList<ReleaseAllocationOutcomeDto>>.Fail("A reason is required to release units to stock.");
        }

        var ids = (request.AllocationIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            return OperationResult<IReadOnlyList<ReleaseAllocationOutcomeDto>>.Fail("Choose at least one unit to release.");
        }

        var outcomes = new List<ReleaseAllocationOutcomeDto>();
        foreach (var id in ids)
        {
            var result = await ReleaseAllocationAsync(id, request.Reason.Trim(), ct);
            outcomes.Add(new ReleaseAllocationOutcomeDto(id, result.Succeeded, result.Succeeded ? null : result.Error));
        }

        return OperationResult<IReadOnlyList<ReleaseAllocationOutcomeDto>>.Ok(outcomes);
    }

    public async Task<EvaluationResult<Allocation>> ReleaseAllocationAsync(long allocationId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return EvaluationResult<Allocation>.Fail("A reason is required to release an allocation.");
        }

        var denied = await RejectUnauthorizedAsync<Allocation>(
            PermissionCodes.CompatibilityAllocate,
            CompatibilityAuthorizationRule.EvaluateRelease,
            ct);
        if (denied is not null)
        {
            return denied;
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

        _audit?.Record(
            AuditEventType.Assignment,
            nameof(Allocation),
            allocation.Id,
            oldValue: new
            {
                AllocationStatus = AllocationStatus.Reserved,
                UnitStatus = fromStatus,
                unit.UnitNumber,
                unit.Din,
                BloodProductId = unit.Id
            },
            newValue: new
            {
                AllocationStatus = allocation.Status,
                UnitStatus = unit.Status,
                unit.UnitNumber,
                unit.Din,
                BloodProductId = unit.Id
            },
            reason: reason.Trim());

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Allocation>.Ok(allocation);
    }

    /// <summary>
    /// Re-reads antibody history, screen, ABO, and open workups at record time.
    /// A second call immediately before save closes the window where another
    /// operator posts history after the UI showed electronic XM as eligible.
    /// </summary>
    private async Task<RuleResult?> EvaluateElectronicEligibilityAsync(
        long patientId,
        long specimenId,
        bool requestAntibodyScreenNegative,
        CancellationToken ct)
    {
        if (_policy is not null && !await _policy.GetAllowElectronicCrossmatchAsync(ct))
        {
            return RuleResult.HardStop(
                "XM-EC-DISABLED",
                "Electronic crossmatch is disabled in facility policy until AABB 5.16 validation is complete.");
        }

        var currentAboRhConfirmed = await _bloodTypes.AnyAsync(
            h => h.PatientId == patientId && h.IsCurrent && h.Abo != AboGroup.Unknown && h.RhD != RhType.Unknown, ct);
        var history = await _bloodTypes.ListAsync(h => h.PatientId == patientId, ct);
        var secondAbo = SecondAboDeterminationRule.HasSecondConcordant(
            SecondAboDeterminationRule.FromHistory(history));
        var hasAntibodyHistory = await _antibodyScreenCompat.HasAntibodyHistoryAsync(patientId, ct);
        // The current screen is the one on the specimen crossmatched: verified and negative, not just "no positive on file".
        var screenNegative = requestAntibodyScreenNegative
            && !await _antibodyScreenCompat.HasPositiveAntibodyScreenAsync(patientId, ct)
            && await _antibodyScreenCompat.HasVerifiedNegativeScreenOnSpecimenAsync(specimenId, _clock.UtcNow, ct);
        var hasOpenWorkup = _workups is not null && await _workups.AnyAsync(
            w => w.PatientId == patientId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);
        var requiresExtendedXm = await _antibodyScreenCompat.HasActiveExtendedCrossmatchRequirementAsync(patientId, ct);
        var counts = await _antibodyScreenCompat.CountNegativeAntibodyScreensAsync(patientId, ct);
        var settings = _crossmatchSettings is null
            ? null
            : await _crossmatchSettings.GetActiveAsync(ct);
        var eligibility = ElectronicCrossmatchEligibilityRule.Evaluate(
            currentAboRhConfirmed,
            screenNegative,
            hasAntibodyHistory,
            secondAbo,
            hasOpenWorkup,
            requiresExtendedXm,
            counts.Visits,
            counts.Specimens,
            counts.Tests,
            settings?.ElectronicXmMinimumVisits ?? 0,
            settings?.ElectronicXmMinimumSpecimens ?? 0,
            settings?.ElectronicXmMinimumTests ?? 0);
        return eligibility.Severity == RuleSeverity.HardStop ? eligibility : null;
    }

    private async Task<RuleResult> EvaluateSerologicOpenWorkupAsync(long patientId, CancellationToken ct)
    {
        var hasOpenWorkup = _workups is not null && await _workups.AnyAsync(
            w => w.PatientId == patientId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);
        return AntibodyIdentificationHistoryPostRule.EvaluateCrossmatchOpenWorkup(hasOpenWorkup);
    }

    private async Task AppendAllocateOpenWorkupAsync(
        List<RuleResult> results, long patientId, CancellationToken ct)
    {
        var hasOpenWorkup = _workups is not null && await _workups.AnyAsync(
            w => w.PatientId == patientId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);
        var allocateOpen = AntibodyIdentificationHistoryPostRule.EvaluateAllocateOpenWorkup(hasOpenWorkup);
        if (allocateOpen.Severity != RuleSeverity.Pass)
        {
            results.Add(allocateOpen);
        }
    }

    /// <summary>
    /// Null when reserve may continue. A warning-only evaluation asks for reason and
    /// authorizer. A hard stop means the signed-in user cannot override.
    /// </summary>
    private async Task<RuleEvaluation?> AuthorizeAntigenNegOverrideAsync(
        RuleEvaluation evaluation,
        AllocateUnitRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.OverrideReason) || string.IsNullOrWhiteSpace(request.AuthorizedBy))
        {
            return OverrideFieldsRule.WithMissing(evaluation, request.OverrideReason, request.AuthorizedBy);
        }

        if (_permissions is null || _exceptionDefinitions is null || _overrides is null)
        {
            return new RuleEvaluation(evaluation.Results.Append(RuleResult.HardStop(
                "EXC-DEF-MISSING",
                "Antigen-negative override cannot be authorized.")).ToList());
        }

        var antigenDef = await _exceptionDefinitions.FirstOrDefaultAsync(
            e => e.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode && e.IsActive, ct);
        var userLevel = await _permissions.GetMaxSecurityLevelAsync(_currentUser.UserName, ct);
        var access = ExceptionOverridePolicy.EvaluateAccess(
            userLevel, antigenDef, BloodAttributeCompatibilityRule.AntigenNegCode);
        if (access.Severity == RuleSeverity.HardStop)
        {
            return new RuleEvaluation(evaluation.Results.Append(access).ToList());
        }

        return null;
    }

    private async Task<IReadOnlyList<RuleResult>> EvaluateAboRhAsync(
        long patientId,
        AboRh patientType,
        AboRh unitType,
        ProductType product,
        CancellationToken ct)
    {
        var patient = await _patients.GetByIdAsync(patientId, ct);
        var policy = await ProductAboRhPolicyLoader.ForProductAsync(
            _aboCompatibilities, _rhCompatibilities, product.Id, ct);
        return AboCompatibilityRule.Evaluate(
            patientType, unitType, product.ComponentClass, patient?.Sex ?? Sex.Unknown, policy);
    }

    private async Task<EvaluationResult<T>?> RejectMergedPatientAsync<T>(long patientId, CancellationToken ct)
    {
        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return EvaluationResult<T>.Fail("Patient not found.");
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        return clinical.Severity == RuleSeverity.HardStop
            ? EvaluationResult<T>.Blocked(new RuleEvaluation([clinical]))
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
            ? EvaluationResult<T>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
