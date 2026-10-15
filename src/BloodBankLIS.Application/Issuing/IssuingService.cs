using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Printing;
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
    private readonly IRepository<SpecialRequirementDefinition>? _requirementDefinitions;
    private readonly IRepository<ProductAttribute> _productAttributes;
    private readonly IRepository<ProductAttributeAssignment> _productAttributeAssignments;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<User> _users;
    private readonly IRepository<InventoryLocation> _locations;
    private readonly BloodAttributeCompatLoader _bloodAttributeCompat;
    private readonly AntibodyScreenCompatLoader _antibodyScreenCompat;
    private readonly FacilityPolicyService _policy;
    private readonly ReactionInvestigationService _reactions;
    private readonly IPermissionEvaluator _permissions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IRepository<AntibodyIdentificationWorkup>? _workups;
    private readonly CrossmatchSettingsReader? _crossmatchSettings;
    private readonly IRepository<AntibodyHistory>? _antibodies;
    private readonly IRepository<OrderAction>? _orderActions;
    private readonly IRepository<LabelLayout>? _labelLayouts;
    private readonly IRepository<PtagIdentifier>? _ptagIdentifiers;
    private readonly IRepository<ProductRetypeResult>? _retypes;
    private readonly IRepository<AutoThawRequest>? _autoThawRequests;
    private readonly ElectronicCrossmatchEligibilityService? _electronicCrossmatch;
    private readonly IRepository<ProductAboCompatibility>? _aboCompatibilities;
    private readonly IRepository<ProductRhCompatibility>? _rhCompatibilities;

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
        IRepository<InventoryLocation> locations,
        BloodAttributeCompatLoader bloodAttributeCompat,
        AntibodyScreenCompatLoader antibodyScreenCompat,
        FacilityPolicyService policy,
        ReactionInvestigationService reactions,
        IPermissionEvaluator permissions,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IRepository<AntibodyIdentificationWorkup>? workups = null,
        IRepository<SpecialRequirementDefinition>? requirementDefinitions = null,
        CrossmatchSettingsReader? crossmatchSettings = null,
        IRepository<AntibodyHistory>? antibodies = null,
        IRepository<OrderAction>? orderActions = null,
        IRepository<LabelLayout>? labelLayouts = null,
        IRepository<PtagIdentifier>? ptagIdentifiers = null,
        IRepository<ProductRetypeResult>? retypes = null,
        IRepository<AutoThawRequest>? autoThawRequests = null,
        ElectronicCrossmatchEligibilityService? electronicCrossmatch = null,
        IRepository<ProductAboCompatibility>? aboCompatibilities = null,
        IRepository<ProductRhCompatibility>? rhCompatibilities = null)
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
        _locations = locations;
        _bloodAttributeCompat = bloodAttributeCompat;
        _antibodyScreenCompat = antibodyScreenCompat;
        _policy = policy;
        _reactions = reactions;
        _permissions = permissions;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _workups = workups;
        _requirementDefinitions = requirementDefinitions;
        _crossmatchSettings = crossmatchSettings;
        _antibodies = antibodies;
        _orderActions = orderActions;
        _labelLayouts = labelLayouts;
        _ptagIdentifiers = ptagIdentifiers;
        _retypes = retypes;
        _autoThawRequests = autoThawRequests;
        _electronicCrossmatch = electronicCrossmatch;
        _aboCompatibilities = aboCompatibilities;
        _rhCompatibilities = rhCompatibilities;
    }

    public async Task<EvaluationResult<Issue>> IssueUnitAsync(IssueUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var createAuth = IssueAuthorizationRule.EvaluateCreate(
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueCreate, ct));
        if (createAuth.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Issue>.Blocked(new RuleEvaluation([createAuth]));
        }

        var emergencyAuth = IssueAuthorizationRule.EvaluateEmergency(
            request.IssueType,
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueEmergencyRelease, ct));
        if (emergencyAuth.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Issue>.Blocked(new RuleEvaluation([emergencyAuth]));
        }

        var unit = await _inventory.GetUnitAsync(request.BloodUnitId, ct);
        if (unit is null)
        {
            return EvaluationResult<Issue>.Fail("Unit not found.");
        }

        if (unit.Status == UnitStatus.Recalled)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentRecalled}: Unit is recalled.");
        if (unit.Status == UnitStatus.Quarantine)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentQuarantined}: Unit is quarantined.");
        if (unit.Status == UnitStatus.OnHold)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentOnHold}: Unit is on operational hold.");
        if (unit.Status == UnitStatus.Missing)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentMissing}: Unit is missing from inventory.");
        if (unit.Status == UnitStatus.Damaged)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentDamaged}: Unit container is damaged.");
        if (unit.Status == UnitStatus.ReturnedToSupplier)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentReturnedToSupplier}: Unit was returned to the supplier.");
        if (unit.Status is UnitStatus.Issued or UnitStatus.Transfused or UnitStatus.TransfusionStarted)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentAlreadyIssued}: Unit already issued or transfused.");

        var patient = await _patients.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
        {
            return EvaluationResult<Issue>.Fail("Patient not found.");
        }

        // Fresh scan verification required when the unit has a canonical ISBT identity.
        var issueScan = EvaluateRequiredScan(unit, request.VerifiedScan, "at issue");
        if (issueScan is not null)
        {
            return issueScan;
        }

        var now = _clock.UtcNow;
        var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == request.PatientId && h.IsCurrent, ct);
        var specimen = await GetCurrentSpecimenAsync(request.PatientId, ct);
        var allocation = await _allocations.FirstOrDefaultAsync(
            a => a.BloodProductId == unit.Id && a.PatientId == request.PatientId && a.Status == AllocationStatus.Reserved, ct);

        var unitCrossmatches = await _crossmatches.ListAsync(
            x => x.BloodProductId == unit.Id && x.PatientId == request.PatientId, ct);
        var hasValidCrossmatch = unitCrossmatches.Any(x =>
            CrossmatchValidityRule.IsCurrentCompatible(x.Result, x.ExpiresUtc, now));
        var hasIncompatibleCrossmatch = unitCrossmatches.Any(x =>
            CrossmatchValidityRule.CountsAsIncompatible(x.Result));
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

        var specialMet = await SpecialRequirementsSatisfiedAsync(
            request.PatientId, unit, bloodAttrs.UnitAntigens, now, request.AcknowledgedSpecialRequirementCodes, bloodType, ct);
        var productMatches = await ProductMatchesOrderAsync(allocation, request.OrderId, unit.ProductTypeId, ct);
        var linkedOrderId = request.OrderId ?? allocation?.OrderId;
        var linkedOrder = linkedOrderId is long oid ? await _orders.GetByIdAsync(oid, ct) : null;
        var unresolvedDelta = await HasUnresolvedAboRhDiscrepancyAsync(request.PatientId, ct);
        var location = unit.CurrentLocationId is { } locationId
            ? await _locations.GetByIdAsync(locationId, ct)
            : null;
        var facilityAllowsExm = await _policy.GetAllowElectronicCrossmatchAsync(ct);
        var requireSecondAbo = await _policy.GetRequireSecondAboForCellularIssueAsync(ct);
        var uncrossmatchedGroupO = await _policy.GetUncrossmatchedCellularMustBeGroupOAsync(ct);
        var uncrossmatchedONeg = await _policy.GetUncrossmatchedONegForChildbearingAsync(ct);
        var childbearingAge = await _policy.GetChildbearingAgeYearsAsync(ct);
        var typeHistory = await _bloodTypes.ListAsync(h => h.PatientId == request.PatientId, ct);
        var secondAbo = SecondAboDeterminationRule.HasSecondConcordant(
            typeHistory.Select(h => new SecondAboDeterminationRule.Determination(h.BloodType, h.IsCurrent)).ToList());
        var exmEligible = facilityAllowsExm && await IsElectronicCrossmatchEligibleAsync(request.PatientId, ct);
        var issueType = ResolveIssueType(request.IssueType, location, exmEligible, hasValidCrossmatch);
        var retypeConfirmed = await ProductRetypeConfirmation.IsConfirmedAsync(_retypes, productType, unit.Id, ct);
        var nearExpiryHours = await _policy.GetNearExpiryWarningHoursAsync(ct);
        var postThawDating = await IsPostThawDatingAsync(unit.Id, unit.ExpiresUtc, ct);
        var emergencyUnretyped = issueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion
            && !retypeConfirmed;

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
            UnitNearExpiryWindow = BloodUnitExpirationRule.NearExpiryWindow(
                postThawDating, TimeSpan.FromHours(nearExpiryHours)),
            AllocatedToThisPatient = allocation is not null || emergencyUnretyped,
            RetypeConfirmed = retypeConfirmed,
            RequiresCrossmatch = productType?.RequiresCrossmatch ?? false,
            HasValidCrossmatch = hasValidCrossmatch,
            HasIncompatibleCrossmatch = hasIncompatibleCrossmatch,
            IsEmergencyRelease = issueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion,
            ProductTypeMatchesOrder = productMatches,
            PatientSignificantAntibodies = bloodAttrs.PatientSignificantAntibodies,
            PatientAntigens = bloodAttrs.PatientAntigens,
            UnitSignificantAntibodies = bloodAttrs.UnitSignificantAntibodies,
            UnitAntigens = bloodAttrs.UnitAntigens,
            SpecialRequirementsMet = specialMet,
            UnresolvedAboRhDiscrepancy = unresolvedDelta,
            VisualInspectionAcceptable = request.VisualInspectionAcceptable
                && IssueAppearanceRule.IsAcceptable(request.Appearance),
            Appearance = request.Appearance,
            LocationKnown = location is not null,
            LocationActive = location?.IsActive ?? true,
            LocationAllowsIssue = location?.AllowsIssue ?? true,
            LocationAllowsRemoteIssue = location?.AllowsRemoteIssue ?? false,
            LocationAllowsElectronicIssue = location?.AllowsElectronicIssue ?? true,
            LocationRequiresSecondVerifier = location?.RequiresSecondVerifier ?? false,
            LocationAllowsComponent = location is null
                || InventoryLocationPolicyRule.AllowsComponent(location, productType?.ComponentClass ?? ComponentClass.Other),
            HasSecondVerifier = !string.IsNullOrWhiteSpace(request.SecondVerifier),
            IsRemoteIssue = issueType == IssueType.RemoteIssue,
            IsElectronicIssue = issueType == IssueType.ElectronicIssue,
            ElectronicCrossmatchEligible = exmEligible,
            RequireSecondAboForCellularIssue = requireSecondAbo,
            HasSecondConcordantAboRh = secondAbo,
            PatientSex = patient.Sex,
            AboRhPolicy = productType is null
                ? null
                : await ProductAboRhPolicyLoader.ForProductAsync(
                    _aboCompatibilities, _rhCompatibilities, productType.Id, ct),
            PatientAgeYears = AgeYears(patient.DateOfBirth, now),
            UncrossmatchedMustBeGroupO = uncrossmatchedGroupO,
            UncrossmatchedONegForChildbearing = uncrossmatchedONeg,
            ChildbearingAgeYears = childbearingAge,
            DonationRestriction = unit.DonationRestriction,
            ReservedPatientId = unit.ReservedPatientId,
            IssuePatientId = request.PatientId,
            PatientStatus = patient.Status,
            OrderLinked = linkedOrder is not null,
            OrderIsFulfillable = linkedOrder is null || OrderControlRule.IsFulfillable(linkedOrder.Status),
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
                IssueAuditType(issueType),
                nameof(BloodUnit),
                unit.Id,
                newValue: new
                {
                    Blocked = true,
                    IssueType = issueType,
                    HardStops = evaluation.HardStops.Select(r => r.Code),
                    unit.UnitNumber,
                    unit.Din,
                    BloodProductId = unit.Id
                },
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

            var overrideAuth = IssueAuthorizationRule.EvaluateOverride(
                true,
                issueType,
                await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueOverride, ct));
            if (overrideAuth.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<Issue>.Blocked(
                    new RuleEvaluation(evaluation.Results.Append(overrideAuth).ToList()));
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
                Action = issueType == IssueType.EmergencyRelease ? OverrideAction.EmergencyRelease : OverrideAction.WarningOverride,
                ContextType = nameof(Issue),
                ContextId = unit.Id,
                RuleCode = string.Join(",", evaluation.Warnings.Select(r => r.Code)),
                Reason = request.OverrideReason,
                AuthorizedBy = request.AuthorizedBy,
                OverriddenUtc = now
            };
            await _overrides.AddAsync(authorizedOverride, ct);
        }

        var matchedPtag = await MatchPtagAsync(unit.Id, request.PatientId, request.PtagId, ct);
        if (matchedPtag.Error is not null)
        {
            return EvaluationResult<Issue>.Fail(matchedPtag.Error);
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

        var testsIncomplete = issueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion
            && !hasValidCrossmatch;
        var retroDueUtc = testsIncomplete
            ? issuedUtc.AddHours(await _policy.GetRetrospectiveCrossmatchDueHoursAsync(ct))
            : (DateTime?)null;

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
            IssueType = issueType,
            Override = authorizedOverride,
            Status = IssueStatus.Issued,
            VerifiedScanJson = request.VerifiedScan is null ? null : JsonSerializer.Serialize(request.VerifiedScan),
            ReceivedBy = request.ReceivedBy,
            UnitExpirationAtIssueUtc = unit.ExpiresUtc,
            CrossmatchStatus = testsIncomplete
                ? CrossmatchClinicalStatus.NotCrossmatchedEmergency
                : hasValidCrossmatch || (exmEligible && issueType is IssueType.ElectronicIssue or IssueType.RemoteIssue)
                    ? CrossmatchClinicalStatus.Compatible
                    : CrossmatchClinicalStatus.NotPerformed,
            EmergencyReleaseDetails = testsIncomplete
                ? request.OverrideReason
                : null,
            TestsIncompleteAtIssue = testsIncomplete,
            RetrospectiveCrossmatchDueUtc = retroDueUtc,
            CoolerId = string.IsNullOrWhiteSpace(request.CoolerId) ? null : request.CoolerId.Trim(),
            InTransitDueUtc = issuedUtc.AddHours(
                location?.DefaultInTransitHours ?? await _policy.GetInTransitDueHoursAsync(ct)),
            VisualInspectionAcceptable = request.VisualInspectionAcceptable
                && IssueAppearanceRule.IsAcceptable(request.Appearance),
            IssueAppearance = request.Appearance,
            SecondVerifier = request.SecondVerifier,
            PatientIdentifier1 = request.PatientIdentifier1Value,
            PatientIdentifier2 = request.PatientIdentifier2Value,
            AcknowledgedSpecialRequirementCodes = JoinAcknowledgedCodes(request.AcknowledgedSpecialRequirementCodes),
            PtagCode = matchedPtag.Identifier?.Code
        };
        await _issues.AddAsync(issue, ct);
        if (matchedPtag.Identifier is { } consumed)
        {
            consumed.Issue = issue;
            consumed.ConsumedUtc = now;
        }

        if (allocation is not null)
        {
            allocation.Status = AllocationStatus.Consumed;
        }

        await CancelPendingModificationsOnIssueAsync(unit, allocation, ct);

        _inventory.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = UnitStatus.Issued,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = issueType switch
            {
                IssueType.EmergencyRelease => "Emergency release",
                IssueType.ElectronicIssue => "Electronic issue",
                IssueType.RemoteIssue => "Remote issue",
                _ => "Issued to patient"
            },
            ChangedBy = _currentUser.UserName,
            ChangedUtc = now,
            RelatedEntityType = nameof(Issue)
        });

        _audit.Record(
            IssueAuditType(issueType),
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus, unit.UnitNumber, unit.Din, BloodProductId = unit.Id },
            newValue: new
            {
                Status = UnitStatus.Issued,
                IssueType = issueType,
                request.PatientId,
                unit.UnitNumber,
                unit.Din,
                BloodProductId = unit.Id
            },
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

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (InventoryConcurrency.AsFailure<Issue>(ex) is { } conflict)
        {
            return conflict;
        }

        var hasOpenWorkup = _workups is not null && await _workups.AnyAsync(
            w => w.PatientId == request.PatientId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);
        var issueOpen = AntibodyIdentificationHistoryPostRule.EvaluateIssueOpenWorkup(hasOpenWorkup);
        if (issueOpen.Severity != RuleSeverity.Pass)
        {
            evaluation = new RuleEvaluation(evaluation.Results.Append(issueOpen).ToList());
        }

        return EvaluationResult<Issue>.Ok(issue, evaluation);
    }

    public Task<Issue?> GetAsync(long id, CancellationToken ct = default) =>
        _issues.GetByIdAsync(id, ct);

    /// <summary>
    /// Emergency / MTP issues still waiting for a retrospective compatible crossmatch.
    /// Returned units drop off; transfused units stay until XM is documented.
    /// </summary>
    public async Task<IReadOnlyList<RetrospectiveCrossmatchWorkItemDto>> ListPendingRetrospectiveCrossmatchesAsync(
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var rows = await _issues.ListAsync(i =>
            i.TestsIncompleteAtIssue
            && i.CrossmatchStatus != CrossmatchClinicalStatus.Compatible
            && i.RetrospectiveCrossmatchCompletedUtc == null
            && i.Status != IssueStatus.Returned, ct);

        var context = await LoadIssueWorklistContextAsync(rows, ct);
        var authorizers = await LoadEmergencyReleaseAuthorizersAsync(rows, ct);
        var issuePlaces = await LoadIssuePlacesAsync(rows, ct);
        return rows
            .OrderBy(i => i.RetrospectiveCrossmatchDueUtc ?? i.IssuedUtc)
            .Select(i =>
            {
                var row = context.For(i, now);
                authorizers.TryGetValue(i.OverrideId ?? 0, out var authorizer);
                return RetrospectiveCrossmatchWorkItemDto.From(
                    i, now, row.Mrn, row.DisplayName, row.UnitNumber, row.CurrentBloodType,
                    row.HasAntibodyHistory, row.AntibodySummary, row.SpecimenExpiresUtc, row.SpecimenExpired,
                    row.SpecimenId, row.AccessionNumber, row.ProductName, row.UnitExpiresUtc, row.UnitBloodType,
                    row.CrossmatchExpiresUtc, row.LatestCrossmatchResult, authorizer, issuePlaces);
            })
            .ToList();
    }

    private async Task<Dictionary<long, string?>> LoadEmergencyReleaseAuthorizersAsync(
        IReadOnlyList<Issue> rows,
        CancellationToken ct)
    {
        var overrideIds = rows.Where(i => i.OverrideId is long).Select(i => i.OverrideId!.Value).Distinct().ToList();
        if (overrideIds.Count == 0)
        {
            return [];
        }

        var overrides = await _overrides.ListAsync(o => overrideIds.Contains(o.Id), ct);
        return overrides.ToDictionary(
            o => o.Id,
            o => string.IsNullOrWhiteSpace(o.AuthorizedBy) ? null : o.AuthorizedBy.Trim());
    }

    /// <summary>
    /// SoftBank cooler / remote-issue worklist: issued units not yet received at the ward.
    /// </summary>
    public async Task<IReadOnlyList<InTransitWorkItemDto>> ListInTransitAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var rows = await _issues.ListAsync(i =>
            i.Status == IssueStatus.Issued && i.WardReceivedUtc == null, ct);

        var context = await LoadIssueWorklistContextAsync(rows, ct);
        var authorizers = await LoadEmergencyReleaseAuthorizersAsync(rows, ct);
        var issuePlaces = await LoadIssuePlacesAsync(rows, ct);
        return rows
            .OrderBy(i => i.InTransitDueUtc ?? i.IssuedUtc)
            .Select(i =>
            {
                var row = context.For(i, now);
                authorizers.TryGetValue(i.OverrideId ?? 0, out var authorizer);
                return InTransitWorkItemDto.From(
                    i, now, row.Mrn, row.DisplayName, row.UnitNumber, row.CurrentBloodType,
                    row.HasAntibodyHistory, row.AntibodySummary, row.SpecimenExpiresUtc, row.SpecimenExpired,
                    row.ProductName, row.UnitExpiresUtc, row.UnitBloodType, row.AccessionNumber, row.CrossmatchExpiresUtc, row.LatestCrossmatchResult, authorizer, issuePlaces);
            })
            .ToList();
    }

    /// <summary>
    /// SafeTrace / SoftBank second-sample board: reserved units for patients who still
    /// need a concordant historical or second-sample ABO/Rh before routine RBC issue.
    /// </summary>
    public async Task<IReadOnlyList<SecondAboWorkItemDto>> ListPendingSecondAboAsync(
        CancellationToken ct = default)
    {
        if (!await _policy.GetRequireSecondAboForCellularIssueAsync(ct))
        {
            return [];
        }

        var reserved = await _allocations.ListAsync(a => a.Status == AllocationStatus.Reserved, ct);
        if (reserved.Count == 0)
        {
            return [];
        }

        var patientIds = reserved.Select(a => a.PatientId).Distinct().ToList();
        var patients = await _patients.ListAsync(p => patientIds.Contains(p.Id), ct);
        var history = await _bloodTypes.ListAsync(h => patientIds.Contains(h.PatientId), ct);
        var historyByPatient = history.GroupBy(h => h.PatientId).ToDictionary(g => g.Key, g => g.ToList());
        var reservedByPatient = reserved.GroupBy(a => a.PatientId).ToDictionary(g => g.Key, g => g.Count());

        var items = new List<SecondAboWorkItemDto>();
        foreach (var patient in patients.OrderBy(p => p.LastName).ThenBy(p => p.FirstName))
        {
            var rows = historyByPatient.GetValueOrDefault(patient.Id) ?? [];
            var current = rows.FirstOrDefault(h => h.IsCurrent);
            var second = SecondAboDeterminationRule.HasSecondConcordant(
                rows.Select(h => new SecondAboDeterminationRule.Determination(h.BloodType, h.IsCurrent)).ToList());
            if (second || current is null || !current.BloodType.IsKnown)
            {
                continue;
            }

            items.Add(new SecondAboWorkItemDto(
                patient.Id,
                patient.MedicalRecordNumber,
                $"{patient.LastName}, {patient.FirstName}",
                current.BloodType.ToString(),
                reservedByPatient.GetValueOrDefault(patient.Id),
                "Second concordant ABO/Rh is required before non-emergency red-cell issue."));
        }

        return items;
    }

    /// <summary>
    /// Issued units not yet returned or transfused. After ward receipt they leave
    /// the in-transit board and remain here so return and transfusion identify
    /// the unit by number instead of a raw issue id.
    /// </summary>
    public async Task<IReadOnlyList<OutstandingIssueWorkItemDto>> ListOutstandingIssuedAsync(
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var rows = await _issues.ListAsync(i => i.Status == IssueStatus.Issued, ct);
        var context = await LoadIssueWorklistContextAsync(rows, ct);
        var authorizers = await LoadEmergencyReleaseAuthorizersAsync(rows, ct);
        var issuePlaces = await LoadIssuePlacesAsync(rows, ct);
        return rows
            .OrderBy(i => i.IssuedUtc)
            .Select(i =>
            {
                var row = context.For(i, now);
                authorizers.TryGetValue(i.OverrideId ?? 0, out var authorizer);
                return OutstandingIssueWorkItemDto.From(
                    i, now, row.Mrn, row.DisplayName, row.DateOfBirth, row.UnitNumber, row.CurrentBloodType,
                    row.HasAntibodyHistory, row.AntibodySummary, row.SpecimenExpiresUtc, row.SpecimenExpired,
                    row.ProductName, row.UnitExpiresUtc, row.UnitBloodType, row.AccessionNumber, row.CrossmatchExpiresUtc, row.LatestCrossmatchResult, authorizer, issuePlaces);
            })
            .ToList();
    }

    /// <summary>
    /// Reserved allocations waiting to leave the blood bank. The issue desk uses
    /// unit number and MRN instead of opening the patient chart after reserve.
    /// </summary>
    public async Task<IReadOnlyList<ReadyToIssueWorkItemDto>> ListReadyToIssueAllocationsAsync(
        CancellationToken ct = default)
    {
        var reserved = await _allocations.ListAsync(a => a.Status == AllocationStatus.Reserved, ct);
        if (reserved.Count == 0)
        {
            return [];
        }

        var now = _clock.UtcNow;
        var patientIds = reserved.Select(a => a.PatientId).Distinct().ToList();
        var patients = (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id);
        var historyByPatient = (await _bloodTypes.ListAsync(h => patientIds.Contains(h.PatientId), ct))
            .GroupBy(h => h.PatientId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var requireSecondAbo = await _policy.GetRequireSecondAboForCellularIssueAsync(ct);
        var nearExpiryWindow = TimeSpan.FromHours(await _policy.GetNearExpiryWarningHoursAsync(ct));
        var antibodies = _antibodies is null
            ? []
            : await _antibodies.ListAsync(a => patientIds.Contains(a.PatientId), ct);
        var antibodiesByPatient = antibodies
            .GroupBy(a => a.PatientId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.IsActive).ThenBy(a => a.AntibodySpecificity).ToList());
        var specimens = await _specimens.ListAsync(s => patientIds.Contains(s.PatientId), ct);
        var xms = await _crossmatches.ListAsync(
            x => patientIds.Contains(x.PatientId), ct);
        var openWorkupPatientIds = await OpenAntibodyWorkupPatientIdsAsync(patientIds, ct);
        var reactionPatientIds = (await _transfusions.ListAsync(
                t => patientIds.Contains(t.PatientId) && t.ReactionSuspected, ct))
            .Select(t => t.PatientId)
            .ToHashSet();
        var electronicCrossmatchNotices = await ElectronicCrossmatchNoticesAsync(patientIds, ct);
        var requirementsByPatient = await ReadyToIssueRequirementsByPatientAsync(patientIds, ct);
        var attributesByProduct = await ProductAttributeCodesByProductTypeAsync(ct);

        var items = new List<ReadyToIssueWorkItemDto>();
        var storageLocationNames = new Dictionary<long, string?>();
        foreach (var allocation in reserved.OrderBy(a => a.AllocatedUtc))
        {
            patients.TryGetValue(allocation.PatientId, out var patient);
            var typeRows = historyByPatient.GetValueOrDefault(allocation.PatientId) ?? [];
            var type = typeRows.Where(h => h.IsCurrent).OrderByDescending(h => h.Id).FirstOrDefault();
            antibodiesByPatient.TryGetValue(allocation.PatientId, out var history);
            var unit = await _inventory.GetUnitAsync(allocation.BloodProductId, ct);
            var cellular = unit?.ProductType?.ComponentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood;
            var needsSecondAbo = requireSecondAbo
                && cellular
                && !SecondAboDeterminationRule.HasSecondConcordant(
                    typeRows.Select(h => new SecondAboDeterminationRule.Determination(h.BloodType, h.IsCurrent)).ToList());
            var unitCrossmatches = xms
                .Where(x => x.PatientId == allocation.PatientId && x.BloodProductId == allocation.BloodProductId)
                .ToList();
            var latestXm = unitCrossmatches
                .OrderByDescending(x => x.PerformedUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
            var hasValidCrossmatch = unitCrossmatches.Any(x =>
                CrossmatchValidityRule.IsCurrentCompatible(x.Result, x.ExpiresUtc, now));
            var hasIncompatibleCrossmatch = unitCrossmatches.Any(x =>
                CrossmatchValidityRule.CountsAsIncompatible(x.Result));
            var requiresXm = unit?.ProductType is { } productType
                ? CrossmatchApplicability.RequiresCrossmatch(productType.ComponentClass, productType.RequiresCrossmatch)
                : true;
            var crossmatchCue = CrossmatchValidityRule.DescribeReadyToIssue(
                requiresXm, hasValidCrossmatch, hasIncompatibleCrossmatch);
            var discrepancyCue = IssueGate.DescribeAboRhDiscrepancy(
                IssueGate.HasUnresolvedAboRhDiscrepancy(typeRows.Select(h => (h.BloodType, h.IsCurrent))),
                requiresXm);
            var recordedXm = latestXm is { Result: CrossmatchResult.Compatible or CrossmatchResult.Incompatible }
                ? latestXm.Result
                : (CrossmatchResult?)null;
            var display = ProductAllocationDisplayStatusRule.Evaluate(
                requiresXm,
                latestXm?.Result,
                latestXm?.Result == CrossmatchResult.Incompatible);
            var specimen = SelectCurrentSpecimen(specimens.Where(s => s.PatientId == allocation.PatientId));
            var expires = specimen?.ExpiresUtc;
            var specimenExpired = expires is DateTime exp && now >= exp;
            var (specimenBlocksIssue, specimenBlockReason) = DescribeSpecimenBlock(specimen, specimenExpired);
            var unitOutdated = unit is not null && BloodUnitExpirationRule.IsExpired(unit.ExpiresUtc, now);
            var postThaw = unit is not null && await IsPostThawDatingAsync(unit.Id, unit.ExpiresUtc, ct);
            var expiresSoon = unit is not null && BloodUnitExpirationRule.IsNearExpiry(
                unit.ExpiresUtc,
                now,
                BloodUnitExpirationRule.NearExpiryWindow(postThaw, nearExpiryWindow));
            var openWorkup = openWorkupPatientIds.Contains(allocation.PatientId);
            string? specialRequirementNotice = null;
            string? antigenNotice = null;
            if (unit is not null)
            {
                var bloodAttrs = await _bloodAttributeCompat.LoadAsync(allocation.PatientId, unit.Id, ct);
                specialRequirementNotice = SpecialTransfusionRequirementRule.ReadyToIssueNotice(
                    requirementsByPatient.GetValueOrDefault(allocation.PatientId) ?? [],
                    attributesByProduct.GetValueOrDefault(unit.ProductTypeId)
                        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    bloodAttrs.UnitAntigens,
                    now);
                if (unit.ProductType is { } issuedProduct)
                {
                    antigenNotice = BloodAttributeCompatibilityRule.ReadyToIssueNotice(
                        issuedProduct.ComponentClass,
                        bloodAttrs.PatientSignificantAntibodies,
                        bloodAttrs.PatientAntigens,
                        bloodAttrs.UnitSignificantAntibodies,
                        bloodAttrs.UnitAntigens);
                }
            }
            items.Add(new ReadyToIssueWorkItemDto(
                allocation.Id,
                allocation.PatientId,
                patient?.MedicalRecordNumber,
                patient is null ? null : $"{patient.LastName}, {patient.FirstName}",
                patient?.DateOfBirth.ToString("yyyy-MM-dd"),
                allocation.BloodProductId,
                unit?.UnitNumber,
                type?.BloodType.ToString(),
                history is { Count: > 0 },
                FormatReadyToIssueAntibodies(history),
                expires,
                specimenExpired,
                allocation.AllocatedUtc,
                display,
                recordedXm,
                needsSecondAbo,
                specimenBlocksIssue,
                specimenBlockReason,
                unitOutdated,
                unitOutdated ? "This unit is outdated and cannot be issued." : null,
                openWorkup,
                AntibodyIdentificationHistoryPostRule.ReadyToIssueNotice(openWorkup),
                expiresSoon,
                electronicCrossmatchNotices.GetValueOrDefault(allocation.PatientId),
                crossmatchCue.BlocksRoutineIssue,
                crossmatchCue.Notice,
                specialRequirementNotice,
                antigenNotice,
                discrepancyCue.BlocksIssue,
                discrepancyCue.Notice,
                ReactionHistoryBanner.Notice(reactionPatientIds.Contains(allocation.PatientId)),
                unit is null
                    ? null
                    : ProductSelectionOrder.RestrictedUnitCue(
                        unit.DonationRestriction, unit.ReservedPatientId, allocation.PatientId),
                unit?.ProductType?.Name,
                unit?.ExpiresUtc,
                unit is null ? null : unit.BloodType.ToString(),
                CrossmatchValidityRule.CurrentCompatibleExpiresUtc(
                    unitCrossmatches.Select(x => (x.Result, x.ExpiresUtc)),
                    now),
                await StorageLocationNameAsync(unit?.CurrentLocationId, storageLocationNames, ct),
                specimen?.AccessionNumber));
        }

        return items;
    }

    private async Task<string?> StorageLocationNameAsync(
        long? locationId,
        Dictionary<long, string?> cache,
        CancellationToken ct)
    {
        if (locationId is not long id)
        {
            return null;
        }

        if (cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var location = await _locations.GetByIdAsync(id, ct);
        var name = ReadyToIssueFormCue.StorageLocationLabel(id, location?.Name);
        cache[id] = name;
        return name;
    }

    private async Task<Dictionary<long, string?>> ElectronicCrossmatchNoticesAsync(
        IReadOnlyList<long> patientIds, CancellationToken ct)
    {
        var notices = new Dictionary<long, string?>();
        if (_electronicCrossmatch is null)
        {
            return notices;
        }

        foreach (var patientId in patientIds.Distinct())
        {
            var assessment = await _electronicCrossmatch.AssessAsync(patientId, ct);
            notices[patientId] = assessment is null
                ? null
                : ElectronicCrossmatchBanner.Notice(assessment.Eligible, assessment.BlockingReason);
        }

        return notices;
    }

    private async Task<HashSet<long>> OpenAntibodyWorkupPatientIdsAsync(
        IReadOnlyList<long> patientIds, CancellationToken ct)
    {
        if (_workups is null || patientIds.Count == 0)
        {
            return [];
        }

        var rows = await _workups.ListAsync(w => patientIds.Contains(w.PatientId), ct);
        return rows
            .Where(w => AntibodyIdentificationHistoryPostRule.IsOpen(w.Status))
            .Select(w => w.PatientId)
            .ToHashSet();
    }

    private static string? FormatReadyToIssueAntibodies(IReadOnlyList<AntibodyHistory>? history)
    {
        if (history is not { Count: > 0 })
        {
            return null;
        }

        return string.Join(", ", history.Select(a =>
            a.IsActive
                ? a.AntibodySpecificity
                : $"{a.AntibodySpecificity} (historical / currently undetectable)"));
    }

    /// <summary>
    /// Nursing-unit custody acknowledgment after the unit leaves the blood bank.
    /// Required before transfusion when <see cref="FacilityPolicyKeys.RequireWardReceipt"/> is true.
    /// </summary>
    public async Task<EvaluationResult<Issue>> RecordWardReceiptAsync(
        long issueId, WardReceiptRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wardAuth = IssueAuthorizationRule.EvaluateWardReceipt(
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.TransfusionDocument, ct));
        if (wardAuth.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Issue>.Blocked(new RuleEvaluation([wardAuth]));
        }

        if (string.IsNullOrWhiteSpace(request.ReceivedBy))
        {
            return EvaluationResult<Issue>.Fail("The person who received the unit is required.");
        }

        var appearance = WardAppearanceRule.Evaluate(request.Appearance);
        if (appearance.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Issue>.Blocked(new RuleEvaluation([appearance]));
        }

        if (!request.VisualInspectionAcceptable)
        {
            return EvaluationResult<Issue>.Fail(
                "Do not acknowledge receipt of a unit that failed visual inspection. Return it to the blood bank.");
        }

        var issue = await _issues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return EvaluationResult<Issue>.Fail("Issue not found.");
        }

        if (issue.Status != IssueStatus.Issued)
        {
            return EvaluationResult<Issue>.Fail($"An issue with status {issue.Status} cannot be received at the ward.");
        }

        if (issue.WardReceivedUtc is not null)
        {
            return EvaluationResult<Issue>.Fail("This unit was already acknowledged at the receiving location.");
        }

        var unit = await _inventory.GetUnitAsync(issue.BloodProductId, ct);
        if (unit is null)
        {
            return EvaluationResult<Issue>.Fail("Unit not found.");
        }

        var wardScan = EvaluateRequiredScan(unit, request.VerifiedScan, "at ward receipt");
        if (wardScan is not null)
        {
            return wardScan;
        }

        var now = _clock.UtcNow;
        var overdue = InTransitPendingRule.EvaluateOverdue(issue.InTransitDueUtc, now);
        issue.WardReceivedUtc = now;
        issue.WardReceivedBy = request.ReceivedBy.Trim();
        issue.WardVisualAcceptable = true;
        issue.WardAppearance = request.Appearance;
        issue.WardScanJson = request.VerifiedScan is null ? null : JsonSerializer.Serialize(request.VerifiedScan);
        _issues.Update(issue);

        _audit.Record(
            AuditEventType.Transfusion,
            nameof(Issue),
            issue.Id,
            oldValue: new { WardReceivedUtc = (DateTime?)null, unit.UnitNumber, unit.Din, BloodProductId = unit.Id },
            newValue: new
            {
                issue.WardReceivedBy,
                issue.WardReceivedUtc,
                Late = overdue.Severity == RuleSeverity.Warning,
                unit.UnitNumber,
                unit.Din,
                BloodProductId = unit.Id,
                IssueId = issue.Id
            },
            reason: overdue.Severity == RuleSeverity.Warning
                ? "Ward receipt of issued unit (late — past in-transit due time)."
                : "Ward receipt of issued unit.");

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Issue>.Ok(issue);
    }

    public async Task<EvaluationResult<Return>> ReturnUnitAsync(long issueId, ReturnUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var returnAuth = IssueAuthorizationRule.EvaluateReturn(
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueReturn, ct));
        if (returnAuth.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Return>.Blocked(new RuleEvaluation([returnAuth]));
        }

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
        var returnMinutes = await _policy.GetReturnToAvailableMinutesAsync(ct);
        var timeOk = ReturnReissueRule.IsWithinReturnToAvailableWindow(issue.IssuedUtc, now, returnMinutes);
        var reissueEval = ReturnReissueRule.Evaluate(
            request.TemperatureAcceptable,
            request.SealIntegrityAcceptable,
            request.VisualInspectionAcceptable,
            timeOk,
            unitUnexpired);

        if (request.Destination == UnitStatus.Available && !timeOk)
        {
            return EvaluationResult<Return>.Fail(
                $"Units issued for more than {returnMinutes} minutes can only be changed to quarantined or disposed.");
        }

        if (reissueEval.Severity == RuleSeverity.HardStop
            && request.Destination is null or UnitStatus.Available)
        {
            return EvaluationResult<Return>.Blocked(new RuleEvaluation([reissueEval]));
        }

        var reissueOk = reissueEval.Severity == RuleSeverity.Pass;
        UnitStatus destination;
        if (request.Destination is UnitStatus requested)
        {
            if (requested is not (UnitStatus.Available or UnitStatus.Quarantine or UnitStatus.Discarded))
            {
                return EvaluationResult<Return>.Fail(
                    "Issued units can only be changed to Available, Quarantine, or Disposed.");
            }

            if (requested == UnitStatus.Available && !reissueOk)
            {
                return EvaluationResult<Return>.Fail(
                    "Return inspection does not allow Available. Choose Quarantine or Disposed.");
            }

            destination = requested;
            if ((!request.VisualInspectionAcceptable || !request.SealIntegrityAcceptable)
                && destination == UnitStatus.Quarantine)
            {
                destination = UnitStatus.Discarded;
            }
        }
        else
        {
            destination = reissueOk ? UnitStatus.Available : UnitStatus.Quarantine;
            if (!request.VisualInspectionAcceptable || !request.SealIntegrityAcceptable)
            {
                destination = UnitStatus.Discarded;
            }
        }

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
            unit.QuarantineReasonCode = UnitQuarantineReason.FailedReturnInspection;
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

    public async Task<EvaluationResult<Return>> ReturnUnitForBloodUnitAsync(
        long unitId,
        ReturnUnitRequest request,
        CancellationToken ct = default)
    {
        var issue = (await _issues.ListAsync(
                i => i.BloodProductId == unitId && i.Status == IssueStatus.Issued, ct))
            .OrderByDescending(i => i.IssuedUtc)
            .FirstOrDefault();
        if (issue is null)
        {
            return EvaluationResult<Return>.Fail("No outstanding issue for this unit.");
        }

        return await ReturnUnitAsync(issue.Id, request, ct);
    }

    public async Task<EvaluationResult<TransfusionEvent>> DocumentTransfusionAsync(long issueId, DocumentTransfusionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var documentAuth = IssueAuthorizationRule.EvaluateDocumentTransfusion(
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.TransfusionDocument, ct));
        if (documentAuth.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([documentAuth]));
        }

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

        var patient = await _patients.GetByIdAsync(issue.PatientId, ct);
        if (patient is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Patient not found.");
        }

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
        if (identity.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([identity]));
        }

        // ISBT-normalized components require a fresh bedside unit scan.
        // Legacy units without ComponentIdentity keep the prior scan policy.
        var bedsideScanVerified = false;
        if (!string.IsNullOrEmpty(unit.ComponentIdentity))
        {
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

            bedsideScanVerified = true;
        }

        if (unit.ExpiresUtc <= _clock.UtcNow)
        {
            return EvaluationResult<TransfusionEvent>.Fail($"{IsbtErrorCodes.ComponentExpired}: Unit has expired.");
        }

        var requireReceipt = await _policy.GetRequireWardReceiptAsync(ct);
        var receipt = WardReceiptRule.Evaluate(requireReceipt, issue.WardReceivedUtc is not null);
        if (receipt.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([receipt]));
        }

        var requireSecond = await _policy.GetRequireSecondVerifierAsync(ct);
        var electronicId = identity.Severity == RuleSeverity.Pass && bedsideScanVerified;
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
            PatientIdentificationMethod = request.PatientIdentificationMethod
                ?? $"{request.PatientIdentifier1Type}+{request.PatientIdentifier2Type}",
            UnitIdentificationMethod = request.UnitIdentificationMethod
                ?? (request.BedsideScan is null ? null : "BedsideScan"),
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
            AuditEventType.Transfusion,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Issued },
            newValue: new { Status = terminal, request.FinalDisposition, request.ReactionSuspected },
            reason: "Transfusion documented.");

        await _unitOfWork.SaveChangesAsync(ct);

        if (request.ReactionSuspected)
        {
            await _reactions.OpenForTransfusionAsync(transfusion, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return EvaluationResult<TransfusionEvent>.Ok(transfusion);
    }

    /// <summary>
    /// One-click denotation that an issued unit was transfused. Uses known patient
    /// identifiers and records ward receipt when the facility requires it.
    /// </summary>
    public async Task<EvaluationResult<TransfusionEvent>> MarkTransfusedAsync(long issueId, CancellationToken ct = default)
    {
        var documentAuth = IssueAuthorizationRule.EvaluateDocumentTransfusion(
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.TransfusionDocument, ct));
        if (documentAuth.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([documentAuth]));
        }

        var issue = await _issues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Issue not found.");
        }

        if (issue.Status != IssueStatus.Issued)
        {
            return EvaluationResult<TransfusionEvent>.Fail($"An issue with status {issue.Status} cannot be marked transfused.");
        }

        var unit = await _inventory.GetUnitAsync(issue.BloodProductId, ct);
        if (unit is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Unit not found.");
        }

        if (unit.Status != UnitStatus.Issued && unit.Status != UnitStatus.TransfusionStarted)
        {
            return EvaluationResult<TransfusionEvent>.Fail(
                $"{IsbtErrorCodes.InvalidStatusTransition}: Unit status {unit.Status} cannot be marked transfused.");
        }

        var patient = await _patients.GetByIdAsync(issue.PatientId, ct);
        if (patient is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Patient not found.");
        }

        var now = _clock.UtcNow;
        if (issue.WardReceivedUtc is null)
        {
            issue.WardReceivedUtc = now;
            issue.WardReceivedBy = _currentUser.UserName;
        }

        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = issue.PatientId,
            StartUtc = now,
            StopUtc = now,
            Transfusionist = _currentUser.UserName,
            ReactionSuspected = false,
            FinalDisposition = TransfusionDisposition.Completed,
            DocumentedBy = _currentUser.UserName,
            Location = issue.IssuedToLocation,
            PatientIdentificationMethod = "KnownRecord",
            UnitIdentificationMethod = "IssuedUnit",
            WorkstationId = _currentUser.Workstation
        };
        await _transfusions.AddAsync(transfusion, ct);

        if (unit.Status == UnitStatus.Issued)
        {
            AppendStatus(unit, UnitStatus.TransfusionStarted, "Transfusion started", now, nameof(TransfusionEvent), issue.Id);
        }

        issue.Status = IssueStatus.Transfused;
        AppendStatus(unit, UnitStatus.Transfused, "Marked transfused", now, nameof(TransfusionEvent), issue.Id);

        _audit.Record(
            AuditEventType.Transfusion,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Issued },
            newValue: new { Status = UnitStatus.Transfused, FinalDisposition = TransfusionDisposition.Completed },
            reason: "Unit marked transfused from the product workflow.");

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransfusionEvent>.Ok(transfusion);
    }

    private async Task<Specimen?> GetCurrentSpecimenAsync(long patientId, CancellationToken ct)
    {
        var accepted = await _specimens.ListAsync(s => s.PatientId == patientId && s.Status == SpecimenStatus.Accepted, ct);
        return SelectCurrentSpecimen(accepted);
    }

    /// <summary>
    /// The specimen the issue gate will read: the latest accepted draw for the patient.
    /// </summary>
    private static Specimen? SelectCurrentSpecimen(IEnumerable<Specimen> specimens) =>
        specimens
            .Where(s => s.Status == SpecimenStatus.Accepted)
            .OrderByDescending(s => s.CollectedUtc)
            .FirstOrDefault();

    /// <summary>
    /// Ready to issue uses the same specimen facts as <see cref="IssueGate"/>. A missing,
    /// undated, or expired specimen is already a hard stop, including emergency release.
    /// </summary>
    private static (bool Blocks, string? Reason) DescribeSpecimenBlock(Specimen? specimen, bool specimenExpired)
    {
        if (specimen is null)
        {
            return (true, "No current specimen exists for this patient.");
        }

        if (specimen.ExpiresUtc is null)
        {
            return (true, "Specimen has no expiration date and cannot be validated.");
        }

        if (specimenExpired)
        {
            return (true, "Specimen has expired.");
        }

        return (false, null);
    }

    private async Task<bool> SpecialRequirementsSatisfiedAsync(
        long patientId,
        BloodUnit unit,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        DateTime nowUtc,
        IReadOnlyList<string>? acknowledgedCodes,
        PatientBloodTypeHistory? bloodType,
        CancellationToken ct)
    {
        var rows = await _specialRequirements.ListAsync(r => r.PatientId == patientId && r.IsActive, ct);
        var definitions = _requirementDefinitions is null
            ? []
            : await _requirementDefinitions.ListAsync(ct);
        var byId = definitions.ToDictionary(d => d.Id);
        var refs = rows.Select(r => ToRequirementRef(r, byId)).ToList();

        var assignments = await _productAttributeAssignments.ListAsync(
            a => a.ProductTypeId == unit.ProductTypeId && a.IsActive, ct);
        var attrIds = assignments.Select(a => a.ProductAttributeId).ToHashSet();
        var attributes = await _productAttributes.ListAsync(a => attrIds.Contains(a.Id) && a.IsActive, ct);
        var codes = attributes.Select(a => a.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var acks = (acknowledgedCodes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = SpecialTransfusionRequirementRule.Evaluate(
            refs,
            codes,
            unitAntigens,
            nowUtc,
            acks,
            bloodType?.Abo ?? AboGroup.Unknown,
            bloodType?.AboSubgroup ?? AboSubgroup.Unknown);
        return SpecialTransfusionRequirementRule.AllMet(results);
    }

    private async Task<Dictionary<long, IReadOnlyList<SpecialTransfusionRequirementRule.RequirementRef>>> ReadyToIssueRequirementsByPatientAsync(
        IReadOnlyList<long> patientIds, CancellationToken ct)
    {
        var rows = await _specialRequirements.ListAsync(
            r => patientIds.Contains(r.PatientId) && r.IsActive, ct);
        var definitions = _requirementDefinitions is null
            ? new Dictionary<long, SpecialRequirementDefinition>()
            : (await _requirementDefinitions.ListAsync(ct)).ToDictionary(d => d.Id);
        return rows
            .GroupBy(r => r.PatientId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SpecialTransfusionRequirementRule.RequirementRef>)g
                    .Select(r => ToRequirementRef(r, definitions))
                    .ToList());
    }

    private async Task<Dictionary<long, HashSet<string>>> ProductAttributeCodesByProductTypeAsync(CancellationToken ct)
    {
        var assignments = await _productAttributeAssignments.ListAsync(a => a.IsActive, ct);
        var attributes = (await _productAttributes.ListAsync(a => a.IsActive, ct)).ToDictionary(a => a.Id);
        var byProduct = new Dictionary<long, HashSet<string>>();
        foreach (var assignment in assignments)
        {
            if (!attributes.TryGetValue(assignment.ProductAttributeId, out var attribute))
            {
                continue;
            }

            if (!byProduct.TryGetValue(assignment.ProductTypeId, out var codes))
            {
                codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                byProduct[assignment.ProductTypeId] = codes;
            }

            codes.Add(attribute.Code);
        }

        return byProduct;
    }

    private static SpecialTransfusionRequirementRule.RequirementRef ToRequirementRef(
        SpecialTransfusionRequirement row,
        IReadOnlyDictionary<long, SpecialRequirementDefinition> definitions)
    {
        if (row.RequirementDefinitionId is long id && definitions.TryGetValue(id, out var definition))
        {
            return new SpecialTransfusionRequirementRule.RequirementRef(
                definition.Code,
                definition.EnforcementKind,
                definition.ProductAttributeCode,
                row.AntigenCode,
                row.EffectiveUtc,
                row.ExpiresUtc,
                row.IsActive);
        }

        return SpecialTransfusionRequirementRule.RequirementRef.FromLegacy(
            row.RequirementType, row.AntigenCode, row.EffectiveUtc, row.ExpiresUtc, row.IsActive);
    }

    private static string? JoinAcknowledgedCodes(IReadOnlyList<string>? codes)
    {
        var values = (codes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return values.Count == 0 ? null : string.Join(",", values);
    }

    /// <summary>
    /// True when a completed auto-thaw still owns this expiration. The 24-hour
    /// outdate is the product's shelf life, not a near-expiry exception.
    /// </summary>
    private async Task<bool> IsPostThawDatingAsync(long bloodUnitId, DateTime expiresUtc, CancellationToken ct)
    {
        if (_autoThawRequests is null)
        {
            return false;
        }

        return await _autoThawRequests.AnyAsync(
            r => r.BloodUnitId == bloodUnitId
                && r.Status == AutoThawRequestStatus.Completed
                && r.NewExpiresUtc == expiresUtc,
            ct);
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

    private async Task<bool> IsElectronicCrossmatchEligibleAsync(long patientId, CancellationToken ct)
    {
        var history = await _bloodTypes.ListAsync(h => h.PatientId == patientId, ct);
        var currentConfirmed = history.Any(h => h.IsCurrent && h.BloodType.IsKnown);
        var secondAbo = SecondAboDeterminationRule.HasSecondConcordant(
            history.Select(h => new SecondAboDeterminationRule.Determination(h.BloodType, h.IsCurrent)).ToList());
        var hasAntibodyHistory = await _antibodyScreenCompat.HasAntibodyHistoryAsync(patientId, ct);
        var screenNegative = !await _antibodyScreenCompat.HasPositiveAntibodyScreenAsync(patientId, ct);
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
        return ElectronicCrossmatchEligibilityRule.Evaluate(
            currentConfirmed,
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
            settings?.ElectronicXmMinimumTests ?? 0).Severity
            == RuleSeverity.Pass;
    }

    private static AuditEventType IssueAuditType(IssueType issueType) =>
        issueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion
            ? AuditEventType.EmergencyRelease
            : AuditEventType.Issue;

    private static IssueType ResolveIssueType(
        IssueType requested,
        InventoryLocation? location,
        bool electronicEligible,
        bool hasValidCrossmatch)
    {
        _ = electronicEligible;
        _ = hasValidCrossmatch;
        if (requested is not IssueType.Standard)
        {
            return requested;
        }

        // Satellite refrigerators always record the SafeTrace remote-issue pathway.
        if (location is { AllowsRemoteIssue: true, IsSatellite: true })
        {
            return IssueType.RemoteIssue;
        }

        return IssueType.Standard;
    }

    internal static int? AgeYears(DateOnly dateOfBirth, DateTime nowUtc)
    {
        if (dateOfBirth == default)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(nowUtc);
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age < 0 ? null : age;
    }

    private async Task<bool> HasUnresolvedAboRhDiscrepancyAsync(long patientId, CancellationToken ct)
    {
        var history = await _bloodTypes.ListAsync(h => h.PatientId == patientId, ct);
        return IssueGate.HasUnresolvedAboRhDiscrepancy(history.Select(h => (h.BloodType, h.IsCurrent)));
    }

    /// <summary>
    /// SoftBank remote-issue chain: issue and ward receipt require a fresh ISBT
    /// quadrant scan when the unit has a canonical component identity. Legacy units
    /// without identity are not blocked.
    /// </summary>
    private static EvaluationResult<Issue>? EvaluateRequiredScan(
        BloodUnit unit, ComponentScanVerificationRequest? scan, string when)
    {
        if (string.IsNullOrEmpty(unit.ComponentIdentity))
        {
            return null;
        }

        if (scan is null)
        {
            return EvaluationResult<Issue>.Fail(
                $"{IsbtErrorCodes.UnitScanMismatch}: Fresh component scan verification is required {when}.");
        }

        var scanEval = ComponentScanVerifier.Verify(
            unit,
            scan.Din,
            scan.ProductCodeData,
            scan.ExtendedDivisionCode,
            scan.AboRhdCode,
            scan.ExpirationEncoded);
        if (scanEval.IsHardStopped)
        {
            return EvaluationResult<Issue>.Blocked(scanEval);
        }

        return null;
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

    private async Task CancelPendingModificationsOnIssueAsync(
        BloodUnit unit, Allocation? allocation, CancellationToken ct)
    {
        if (_orderActions is null)
        {
            return;
        }

        var orderId = allocation?.OrderId;
        var pendingIds = (await _orderActions.ListAsync(
            a => a.Status == OrderActionStatus.Pending
                 && (a.SourceBloodUnitId == unit.Id
                     || (orderId != null
                         && a.OrderId == orderId
                         && a.SourceBloodUnitId == null)),
            ct)).Select(a => a.Id).ToList();
        foreach (var actionId in pendingIds)
        {
            var action = await _orderActions.GetByIdAsync(actionId, ct);
            if (action is null || action.Status != OrderActionStatus.Pending)
            {
                continue;
            }

            action.Status = OrderActionStatus.Cancelled;
            _audit.Record(
                AuditEventType.OrderChange,
                nameof(OrderAction),
                action.Id,
                oldValue: new { Status = OrderActionStatus.Pending },
                newValue: new { Status = OrderActionStatus.Cancelled },
                reason: "Canceled because unit was issued");
        }
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

    private async Task<(PtagIdentifier? Identifier, string? Error)> MatchPtagAsync(
        long unitId, long patientId, string? scanned, CancellationToken ct)
    {
        if (_labelLayouts is null || _ptagIdentifiers is null)
        {
            return (null, null);
        }

        var rows = await _labelLayouts.ListAsync(l => l.Kind == LabelLayoutKind.Ptag, ct);
        var json = rows.FirstOrDefault()?.DocumentJson;
        if (string.IsNullOrWhiteSpace(json))
        {
            return (null, null);
        }

        LabelLayoutDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<LabelLayoutDocument>(json, LabelFields.JsonOptions);
        }
        catch (JsonException)
        {
            return (null, null);
        }

        if (!LabelFields.RequiresPtagId(document))
        {
            return (null, null);
        }

        var code = scanned?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return (null, "Scan the P-tag ID printed on this unit's compatibility tag.");
        }

        var open = await _ptagIdentifiers.ListAsync(
            p => p.BloodProductId == unitId && p.PatientId == patientId && p.IssueId == null, ct);
        var match = open.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return (null, "The scanned P-tag ID does not match the tag printed for this unit and patient.");
        }

        return (await _ptagIdentifiers.GetByIdAsync(match.Id, ct), null);
    }

    private async Task<IReadOnlyList<StorageLocationLabel.CatalogLocation>?> LoadIssuePlacesAsync(
        IReadOnlyList<Issue> rows,
        CancellationToken ct)
    {
        if (!rows.Any(row => !string.IsNullOrWhiteSpace(row.IssuedToLocation)))
        {
            return null;
        }

        var locations = await _locations.ListAsync(ct);
        return locations
            .Select(location => new StorageLocationLabel.CatalogLocation(location.Id, location.Name, location.Code))
            .ToList();
    }

    private async Task<IssueWorklistContext> LoadIssueWorklistContextAsync(
        IReadOnlyList<Issue> rows, CancellationToken ct)
    {
        var patientIds = rows.Select(i => i.PatientId).Distinct().ToList();
        var patients = patientIds.Count == 0
            ? []
            : await _patients.ListAsync(p => patientIds.Contains(p.Id), ct);
        var types = patientIds.Count == 0
            ? []
            : await _bloodTypes.ListAsync(h => patientIds.Contains(h.PatientId) && h.IsCurrent, ct);
        var antibodies = _antibodies is null || patientIds.Count == 0
            ? []
            : await _antibodies.ListAsync(a => patientIds.Contains(a.PatientId), ct);
        var specimens = patientIds.Count == 0
            ? []
            : await _specimens.ListAsync(s => patientIds.Contains(s.PatientId), ct);
        var allocationIds = rows
            .Where(i => i.AllocationId is > 0)
            .Select(i => i.AllocationId!.Value)
            .Distinct()
            .ToList();
        var allocations = allocationIds.Count == 0
            ? []
            : await _allocations.ListAsync(a => allocationIds.Contains(a.Id), ct);

        var units = new Dictionary<long, BloodUnit>();
        foreach (var unitId in rows.Select(i => i.BloodProductId).Distinct())
        {
            var unit = await _inventory.GetUnitAsync(unitId, ct);
            if (unit is not null)
            {
                units[unitId] = unit;
            }
        }

        var crossmatches = patientIds.Count == 0
            ? []
            : await _crossmatches.ListAsync(x => patientIds.Contains(x.PatientId), ct);

        return new IssueWorklistContext(patients, types, antibodies, specimens, allocations, units, crossmatches);
    }

    private sealed class IssueWorklistContext(
        IReadOnlyList<Patient> patients,
        IReadOnlyList<PatientBloodTypeHistory> types,
        IReadOnlyList<AntibodyHistory> antibodies,
        IReadOnlyList<Specimen> specimens,
        IReadOnlyList<Allocation> allocations,
        IReadOnlyDictionary<long, BloodUnit> units,
        IReadOnlyList<Crossmatch> crossmatches)
    {
        private readonly Dictionary<long, Patient> _patients = patients.ToDictionary(p => p.Id);
        private readonly Dictionary<long, PatientBloodTypeHistory> _types = types
            .GroupBy(h => h.PatientId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Id).First());
        private readonly Dictionary<long, List<AntibodyHistory>> _antibodies = antibodies
            .GroupBy(a => a.PatientId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.IsActive).ThenBy(a => a.AntibodySpecificity).ToList());
        private readonly Dictionary<long, Allocation> _allocations = allocations.ToDictionary(a => a.Id);
        private readonly Dictionary<long, Specimen> _specimens = specimens.ToDictionary(s => s.Id);
        private readonly IReadOnlyDictionary<long, BloodUnit> _units = units;
        private readonly Dictionary<(long PatientId, long UnitId), List<Crossmatch>> _crossmatches = crossmatches
            .GroupBy(x => (x.PatientId, x.BloodProductId))
            .ToDictionary(g => g.Key, g => g.ToList());

        public (string? Mrn, string? DisplayName, string? DateOfBirth, string? UnitNumber, string? CurrentBloodType,
            bool HasAntibodyHistory, string? AntibodySummary, DateTime? SpecimenExpiresUtc, bool SpecimenExpired,
            long? SpecimenId, string? AccessionNumber, string? ProductName, DateTime? UnitExpiresUtc,
            string? UnitBloodType, DateTime? CrossmatchExpiresUtc, CrossmatchResult? LatestCrossmatchResult)
            For(Issue issue, DateTime clock)
        {
            _patients.TryGetValue(issue.PatientId, out var patient);
            _types.TryGetValue(issue.PatientId, out var type);
            _antibodies.TryGetValue(issue.PatientId, out var history);
            _units.TryGetValue(issue.BloodProductId, out var unit);
            var specimen = ResolveSpecimen(issue);
            var expires = specimen?.ExpiresUtc;
            return (
                patient?.MedicalRecordNumber,
                patient is null ? null : $"{patient.LastName}, {patient.FirstName}",
                patient?.DateOfBirth.ToString("yyyy-MM-dd"),
                unit?.UnitNumber,
                type?.BloodType.ToString(),
                history is { Count: > 0 },
                FormatAntibodySummary(history),
                expires,
                expires is DateTime exp && exp <= clock,
                specimen?.Id,
                specimen?.AccessionNumber,
                unit?.ProductType?.Name,
                unit?.ExpiresUtc,
                unit is null ? null : unit.BloodType.ToString(),
                CurrentCompatibleCrossmatchExpiresUtc(issue, clock),
                LatestRecordedCrossmatchResult(issue));
        }

        private CrossmatchResult? LatestRecordedCrossmatchResult(Issue issue)
        {
            if (!_crossmatches.TryGetValue((issue.PatientId, issue.BloodProductId), out var matches)
                || matches.Count == 0)
            {
                return null;
            }

            var latest = matches
                .OrderByDescending(x => x.PerformedUtc)
                .ThenByDescending(x => x.Id)
                .First();
            return latest.Result == CrossmatchResult.NotPerformed ? null : latest.Result;
        }

        private DateTime? CurrentCompatibleCrossmatchExpiresUtc(Issue issue, DateTime clock)
        {
            if (!_crossmatches.TryGetValue((issue.PatientId, issue.BloodProductId), out var matches))
            {
                return null;
            }

            return CrossmatchValidityRule.CurrentCompatibleExpiresUtc(
                matches.Select(x => (x.Result, x.ExpiresUtc)),
                clock);
        }

        private Specimen? ResolveSpecimen(Issue issue)
        {
            if (issue.AllocationId is long allocationId
                && _allocations.TryGetValue(allocationId, out var allocation)
                && allocation.SpecimenId is long specimenId
                && _specimens.TryGetValue(specimenId, out var linked))
            {
                return linked;
            }

            return _specimens.Values
                .Where(s => s.PatientId == issue.PatientId && s.Status == SpecimenStatus.Accepted)
                .OrderByDescending(s => s.CollectedUtc)
                .FirstOrDefault();
        }

        private static string? FormatAntibodySummary(IReadOnlyList<AntibodyHistory>? history)
        {
            if (history is not { Count: > 0 })
            {
                return null;
            }

            return string.Join(", ", history.Select(a =>
                a.IsActive
                    ? a.AntibodySpecificity
                    : $"{a.AntibodySpecificity} (historical / currently undetectable)"));
        }
    }
}
