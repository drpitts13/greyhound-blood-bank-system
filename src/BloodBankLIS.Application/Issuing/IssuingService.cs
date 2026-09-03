using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
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
    private readonly IRepository<ExceptionDefinition> _exceptionDefinitions;
    private readonly IRepository<SpecialTransfusionRequirement> _specialRequirements;
    private readonly IRepository<ProductAttribute> _productAttributes;
    private readonly IRepository<ProductAttributeAssignment> _productAttributeAssignments;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<User> _users;
    private readonly BloodAttributeCompatLoader _bloodAttributeCompat;
    private readonly FacilityPolicyService _policy;
    private readonly ReactionInvestigationService _reactions;
    private readonly IPermissionEvaluator _permissions;
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
        IRepository<ExceptionDefinition> exceptionDefinitions,
        IRepository<SpecialTransfusionRequirement> specialRequirements,
        IRepository<ProductAttribute> productAttributes,
        IRepository<ProductAttributeAssignment> productAttributeAssignments,
        IRepository<Order> orders,
        IRepository<User> users,
        BloodAttributeCompatLoader bloodAttributeCompat,
        FacilityPolicyService policy,
        ReactionInvestigationService reactions,
        IPermissionEvaluator permissions,
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
        _exceptionDefinitions = exceptionDefinitions;
        _specialRequirements = specialRequirements;
        _productAttributes = productAttributes;
        _productAttributeAssignments = productAttributeAssignments;
        _orders = orders;
        _users = users;
        _bloodAttributeCompat = bloodAttributeCompat;
        _policy = policy;
        _reactions = reactions;
        _permissions = permissions;
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

        if (unit.Status == UnitStatus.Recalled)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentRecalled}: Unit is recalled.");
        if (unit.Status == UnitStatus.Quarantine)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentQuarantined}: Unit is quarantined.");
        if (unit.Status is UnitStatus.Issued or UnitStatus.Transfused or UnitStatus.TransfusionStarted)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentAlreadyIssued}: Unit already issued or transfused.");

        var patient = await _patients.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
        {
            return EvaluationResult<Issue>.Fail("Patient not found.");
        }

        // Fresh scan verification required when the unit has a canonical ISBT identity.
        // Legacy units without ComponentIdentity are not blocked (backward compatible).
        if (!string.IsNullOrEmpty(unit.ComponentIdentity) && request.VerifiedScan is not null)
        {
            var scanEval = ComponentScanVerifier.Verify(
                unit,
                request.VerifiedScan.Din,
                request.VerifiedScan.ProductCodeData,
                request.VerifiedScan.ExtendedDivisionCode,
                request.VerifiedScan.AboRhdCode,
                request.VerifiedScan.ExpirationEncoded);
            if (scanEval.IsHardStopped)
                return EvaluationResult<Issue>.Blocked(scanEval);
        }
        else if (!string.IsNullOrEmpty(unit.ComponentIdentity) && request.VerifiedScan is null)
        {
            return EvaluationResult<Issue>.Fail(
                $"{IsbtErrorCodes.UnitScanMismatch}: Fresh component scan verification is required at issue.");
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
        var identity = PatientIdentityMatchRule.Evaluate(
            patient.MedicalRecordNumber,
            patient.DateOfBirth,
            patient.LastName,
            patient.FirstName,
            string.IsNullOrWhiteSpace(request.PatientIdentifier1Value)
                ? null
                : new PatientIdentityMatchRule.IdentityToken(request.PatientIdentifier1Type, request.PatientIdentifier1Value),
            string.IsNullOrWhiteSpace(request.PatientIdentifier2Value)
                ? null
                : new PatientIdentityMatchRule.IdentityToken(request.PatientIdentifier2Type, request.PatientIdentifier2Value));

        var specialMet = await SpecialRequirementsSatisfiedAsync(request.PatientId, unit, bloodAttrs.UnitAntigens, now, ct);
        var productMatches = await ProductMatchesOrderAsync(allocation, request.OrderId, unit.ProductTypeId, ct);
        var unresolvedDelta = await HasUnresolvedAboRhDiscrepancyAsync(request.PatientId, ct);

        var context = new IssueGateContext
        {
            IdentityConfirmed = identity.Severity == RuleSeverity.Pass,
            SpecimenExists = specimen is not null,
            SpecimenBelongsToPatient = specimen is not null && specimen.PatientId == request.PatientId,
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
            ProductTypeMatchesOrder = productMatches,
            PatientSignificantAntibodies = bloodAttrs.PatientSignificantAntibodies,
            PatientAntigens = bloodAttrs.PatientAntigens,
            UnitSignificantAntibodies = bloodAttrs.UnitSignificantAntibodies,
            UnitAntigens = bloodAttrs.UnitAntigens,
            SpecialRequirementsMet = specialMet,
            UnresolvedAboRhDiscrepancy = unresolvedDelta,
            VisualInspectionAcceptable = request.VisualInspectionAcceptable,
            NowUtc = now
        };

        var evaluation = IssueGate.Evaluate(context);
        var requireSecond = await _policy.GetRequireSecondVerifierAsync(ct);
        var electronicId = request.VerifiedScan is not null
            && identity.Severity == RuleSeverity.Pass;
        var dual = DualIdentificationRule.Evaluate(
            _currentUser.UserName, request.SecondVerifier, electronicId, requireSecond);
        if (dual.Severity != RuleSeverity.Pass)
        {
            evaluation = new RuleEvaluation(evaluation.Results.Append(dual).ToList());
        }

        var directory = await EvaluateSecondVerifierDirectoryAsync(request.SecondVerifier, ct);
        if (directory.Severity != RuleSeverity.Pass)
        {
            evaluation = new RuleEvaluation(evaluation.Results.Append(directory).ToList());
        }

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

            // Antigen-negative overrides require supervisor+ per ExceptionDefinitions catalog.
            if (evaluation.Warnings.Any(w => w.Code == BloodAttributeCompatibilityRule.AntigenNegCode))
            {
                var antigenDef = await _exceptionDefinitions.FirstOrDefaultAsync(
                    e => e.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode && e.IsActive, ct);
                var userLevel = await _permissions.GetMaxSecurityLevelAsync(_currentUser.UserName, ct);
                var access = ExceptionOverridePolicy.EvaluateAccess(
                    userLevel, antigenDef, BloodAttributeCompatibilityRule.AntigenNegCode);
                if (access.Severity == RuleSeverity.HardStop)
                {
                    return EvaluationResult<Issue>.Blocked(
                        new RuleEvaluation(evaluation.Results.Append(access).ToList()));
                }
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

        var issuedUtc = request.IssuedUtc ?? now;
        if (issuedUtc.Kind == DateTimeKind.Unspecified)
        {
            issuedUtc = DateTime.SpecifyKind(issuedUtc, DateTimeKind.Utc);
        }
        else if (issuedUtc.Kind == DateTimeKind.Local)
        {
            issuedUtc = issuedUtc.ToUniversalTime();
        }

        var issue = new Issue
        {
            AllocationId = allocation?.Id,
            BloodProductId = unit.Id,
            PatientId = request.PatientId,
            EncounterId = allocation?.EncounterId,
            OrderId = allocation?.OrderId,
            IssuedTo = string.IsNullOrWhiteSpace(request.IssuedTo) ? null : request.IssuedTo.Trim(),
            IssuedToLocation = string.IsNullOrWhiteSpace(request.IssuedToLocation) ? null : request.IssuedToLocation.Trim(),
            IssuedUtc = issuedUtc,
            IssuedBy = _currentUser.UserName,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            IssueType = request.IssueType,
            Override = authorizedOverride,
            Status = IssueStatus.Issued,
            VerifiedScanJson = request.VerifiedScan is null ? null : JsonSerializer.Serialize(request.VerifiedScan),
            ReceivedBy = request.ReceivedBy,
            UnitExpirationAtIssueUtc = unit.ExpiresUtc,
            CrossmatchStatus = request.IssueType == IssueType.EmergencyRelease
                ? CrossmatchClinicalStatus.NotCrossmatchedEmergency
                : hasValidCrossmatch
                    ? CrossmatchClinicalStatus.Compatible
                    : CrossmatchClinicalStatus.NotPerformed,
            EmergencyReleaseDetails = request.IssueType == IssueType.EmergencyRelease
                ? request.OverrideReason
                : null,
            TestsIncompleteAtIssue = request.IssueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion
                && !hasValidCrossmatch,
            VisualInspectionAcceptable = request.VisualInspectionAcceptable,
            SecondVerifier = request.SecondVerifier,
            PatientIdentifier1 = request.PatientIdentifier1Value,
            PatientIdentifier2 = request.PatientIdentifier2Value
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

        if (unit.Status == UnitStatus.TransfusionStarted || unit.Status == UnitStatus.Transfused)
        {
            return EvaluationResult<Return>.Fail(
                $"{IsbtErrorCodes.ComponentAlreadyTransfused}: Cannot return a unit after transfusion has started.");
        }

        var now = _clock.UtcNow;
        var unitUnexpired = unit.ExpiresUtc > now;
        var reissueEval = ReturnReissueRule.Evaluate(
            request.TemperatureAcceptable,
            request.SealIntegrityAcceptable,
            request.VisualInspectionAcceptable,
            request.TimeOutOfStorageAcceptable,
            unitUnexpired);

        if (reissueEval.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Return>.Blocked(new RuleEvaluation([reissueEval]));
        }

        var reissueOk = reissueEval.Severity == RuleSeverity.Pass;
        var destination = reissueOk ? UnitStatus.Available : UnitStatus.Quarantine;
        if (!request.VisualInspectionAcceptable || !request.SealIntegrityAcceptable)
            destination = UnitStatus.Discarded;

        var returnRecord = new Return
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            ReturnedUtc = now,
            ReturnedBy = _currentUser.UserName,
            Reason = request.Reason,
            ReissueEligible = reissueOk,
            ReissueEvaluationJson = JsonSerializer.Serialize(new
            {
                request.TemperatureAcceptable,
                request.SealIntegrityAcceptable,
                request.VisualInspectionAcceptable,
                request.TimeOutOfStorageAcceptable,
                destination
            })
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
            newValue: new { Status = destination, ReissueEligible = reissueOk },
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

        if (unit.Status != UnitStatus.Issued && unit.Status != UnitStatus.TransfusionStarted)
        {
            return EvaluationResult<TransfusionEvent>.Fail(
                $"{IsbtErrorCodes.InvalidStatusTransition}: Unit status {unit.Status} cannot enter transfusion documentation.");
        }

        // ISBT-normalized components require positive patient ID + fresh bedside scan.
        // Legacy units without ComponentIdentity keep the prior documentation path.
        if (!string.IsNullOrEmpty(unit.ComponentIdentity))
        {
            if (!request.PositivePatientIdentification)
            {
                return EvaluationResult<TransfusionEvent>.Fail(
                    $"{IsbtErrorCodes.PatientMismatch}: Positive patient identification is required at transfusion start.");
            }

            if (request.BedsideScan is null)
            {
                return EvaluationResult<TransfusionEvent>.Fail(
                    $"{IsbtErrorCodes.UnitScanMismatch}: Fresh unit scan is required at bedside.");
            }

            var bedside = ComponentScanVerifier.Verify(
                unit,
                request.BedsideScan.Din,
                request.BedsideScan.ProductCodeData,
                request.BedsideScan.ExtendedDivisionCode,
                request.BedsideScan.AboRhdCode,
                request.BedsideScan.ExpirationEncoded);
            if (bedside.IsHardStopped)
                return EvaluationResult<TransfusionEvent>.Blocked(bedside);
        }

        if (unit.ExpiresUtc <= _clock.UtcNow)
        {
            return EvaluationResult<TransfusionEvent>.Fail($"{IsbtErrorCodes.ComponentExpired}: Unit has expired.");
        }

        var requireSecond = await _policy.GetRequireSecondVerifierAsync(ct);
        var electronicId = request.PositivePatientIdentification && request.BedsideScan is not null;
        var dual = DualIdentificationRule.Evaluate(_currentUser.UserName, request.SecondVerifier, electronicId, requireSecond);
        if (dual.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([dual]));
        }

        var directory = await EvaluateSecondVerifierDirectoryAsync(request.SecondVerifier, ct);
        if (directory.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([directory]));
        }

        var now = _clock.UtcNow;

        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = issue.PatientId,
            StartUtc = request.StartUtc ?? now,
            StopUtc = request.StopUtc,
            VolumeTransfused = request.VolumeTransfused,
            Transfusionist = request.Transfusionist,
            ReactionSuspected = request.ReactionSuspected,
            FinalDisposition = request.FinalDisposition,
            DocumentedBy = _currentUser.UserName,
            SecondVerifier = request.SecondVerifier,
            Location = request.Location,
            PatientIdentificationMethod = request.PatientIdentificationMethod,
            UnitIdentificationMethod = request.UnitIdentificationMethod,
            WorkstationId = _currentUser.Workstation,
            BedsideScanVerificationJson = request.BedsideScan is null ? null : JsonSerializer.Serialize(request.BedsideScan)
        };
        await _transfusions.AddAsync(transfusion, ct);

        if (unit.Status == UnitStatus.Issued)
            AppendStatus(unit, UnitStatus.TransfusionStarted, "Transfusion started", now, nameof(TransfusionEvent), issue.Id);

        var terminal = request.FinalDisposition == TransfusionDisposition.Stopped
            ? UnitStatus.TransfusionStopped
            : UnitStatus.Transfused;

        issue.Status = IssueStatus.Transfused;
        AppendStatus(unit, terminal, $"Transfusion {request.FinalDisposition}", now, nameof(TransfusionEvent), issue.Id);

        _audit.Record(
            AuditEventType.Update,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Issued },
            newValue: new { Status = terminal, request.FinalDisposition, request.ReactionSuspected });

        await _unitOfWork.SaveChangesAsync(ct);

        if (request.ReactionSuspected)
        {
            await _reactions.OpenForTransfusionAsync(transfusion, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return EvaluationResult<TransfusionEvent>.Ok(transfusion);
    }

    private async Task<Specimen?> GetCurrentSpecimenAsync(long patientId, CancellationToken ct)
    {
        var accepted = await _specimens.ListAsync(s => s.PatientId == patientId && s.Status == SpecimenStatus.Accepted, ct);
        return accepted.OrderByDescending(s => s.CollectedUtc).FirstOrDefault();
    }

    private async Task<bool> SpecialRequirementsSatisfiedAsync(
        long patientId,
        BloodUnit unit,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var rows = await _specialRequirements.ListAsync(r => r.PatientId == patientId && r.IsActive, ct);
        var refs = rows.Select(r => new SpecialTransfusionRequirementRule.RequirementRef(
            r.RequirementType, r.AntigenCode, r.EffectiveUtc, r.ExpiresUtc, r.IsActive)).ToList();

        var assignments = await _productAttributeAssignments.ListAsync(
            a => a.ProductTypeId == unit.ProductTypeId && a.IsActive, ct);
        var attrIds = assignments.Select(a => a.ProductAttributeId).ToHashSet();
        var attributes = await _productAttributes.ListAsync(a => attrIds.Contains(a.Id) && a.IsActive, ct);
        var codes = attributes.Select(a => a.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = SpecialTransfusionRequirementRule.Evaluate(refs, codes, unitAntigens, nowUtc);
        return SpecialTransfusionRequirementRule.AllMet(results);
    }

    private async Task<bool> ProductMatchesOrderAsync(Allocation? allocation, long? requestOrderId, long unitProductTypeId, CancellationToken ct)
    {
        var orderId = requestOrderId ?? allocation?.OrderId;
        if (orderId is null)
        {
            return true;
        }

        var order = await _orders.GetByIdAsync(orderId.Value, ct);
        if (order is null || order.ProductTypeId is null)
        {
            return true;
        }

        return order.ProductTypeId == unitProductTypeId;
    }

    private async Task<bool> HasUnresolvedAboRhDiscrepancyAsync(long patientId, CancellationToken ct)
    {
        var history = await _bloodTypes.ListAsync(h => h.PatientId == patientId, ct);
        var current = history.FirstOrDefault(h => h.IsCurrent);
        if (current is null || !current.BloodType.IsKnown)
        {
            return false;
        }

        return history.Any(h =>
            !h.IsCurrent
            && h.BloodType.IsKnown
            && h.BloodType != current.BloodType);
    }

    private async Task<RuleResult> EvaluateSecondVerifierDirectoryAsync(string? secondVerifier, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secondVerifier))
        {
            return SecondVerifierDirectoryRule.Evaluate(secondVerifier, isActiveUser: false);
        }

        var upper = secondVerifier.Trim().ToUpperInvariant();
        var match = await _users.FirstOrDefaultAsync(
            u => u.IsActive && !u.IsLocked && !u.IsServiceAccount && u.UserName.ToUpper() == upper, ct);
        return SecondVerifierDirectoryRule.Evaluate(secondVerifier, match is not null);
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
