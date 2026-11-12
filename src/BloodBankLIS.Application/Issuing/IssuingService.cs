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
    private readonly IRepository<OrderLine>? _orderLines;
    private readonly IRepository<DowntimeEpisode>? _outages;
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
    private readonly IRepository<MtpActivation>? _mtpActivations;
    private readonly IRepository<CompatibilityRuleVersion>? _compatibilityVersions;
    private readonly IRepository<CompatibilityRule>? _compatibilityRules;
    private readonly IRepository<BloodComponentSpecialTest>? _unitSpecialTests;
    private readonly IProductDispensePublisher? _dispensePublisher;
    private readonly IRepository<AutoThawRequest>? _autoThawRequests;
    private readonly ElectronicCrossmatchEligibilityService? _electronicCrossmatch;
    private readonly IRepository<ProductAboCompatibility>? _aboCompatibilities;
    private readonly IRepository<ProductRhCompatibility>? _rhCompatibilities;
    private readonly IRepository<Encounter>? _encounters;

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
        IRepository<ProductRhCompatibility>? rhCompatibilities = null,
        IRepository<MtpActivation>? mtpActivations = null,
        IRepository<CompatibilityRuleVersion>? compatibilityVersions = null,
        IRepository<CompatibilityRule>? compatibilityRules = null,
        IRepository<BloodComponentSpecialTest>? unitSpecialTests = null,
        IProductDispensePublisher? dispensePublisher = null,
        IRepository<Encounter>? encounters = null,
        IRepository<OrderLine>? orderLines = null,
        IRepository<DowntimeEpisode>? outages = null)
    {
        _orderLines = orderLines;
        _outages = outages;
        _encounters = encounters;
        _dispensePublisher = dispensePublisher;
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
        _mtpActivations = mtpActivations;
        _compatibilityVersions = compatibilityVersions;
        _compatibilityRules = compatibilityRules;
        _unitSpecialTests = unitSpecialTests;
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

        // The stated issue time: never in the future; earlier than now is a late entry that needs a reason.
        var requestedIssuedUtc = request.IssuedUtc is DateTime requested
            ? requested.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(requested, DateTimeKind.Utc),
                DateTimeKind.Local => requested.ToUniversalTime(),
                _ => requested
            }
            : _clock.UtcNow;
        var issueTime = IssueTimeRule.Evaluate(requestedIssuedUtc, _clock.UtcNow, request.LateEntryReason);
        if (issueTime.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Issue>.Blocked(new RuleEvaluation([issueTime]));
        }

        var lateEntryReason = IssueTimeRule.IsLateEntry(requestedIssuedUtc, _clock.UtcNow)
            ? request.LateEntryReason!.Trim()
            : null;

        // OCD-054: a downtime back-entry is judged as of the paper record's issue time, by a role that allows it.
        var backEntry = request.DowntimeBackEntry;
        var backEntryCheck = DowntimeBackEntryRule.Evaluate(
            backEntry,
            backEntry && await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueDowntimeBackEntry, ct),
            requestedIssuedUtc,
            _clock.UtcNow);
        if (backEntryCheck.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Issue>.Blocked(new RuleEvaluation([backEntryCheck]));
        }

        // OCD-081: a paper time outside every recorded outage is explained, and the reason kept on the issue.
        string? outsideOutageReason = null;
        if (backEntry && _outages is not null)
        {
            var outages = await _outages.ListAsync(ct);
            var paperTime = DowntimeEpisodeRule.CheckPaperTime(outages, requestedIssuedUtc, request.OutsideOutageReason);
            if (paperTime.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<Issue>.Blocked(new RuleEvaluation([paperTime]));
            }

            outsideOutageReason = DowntimeEpisodeRule.IsOutside(outages, requestedIssuedUtc) ? request.OutsideOutageReason!.Trim() : null;
        }

        var emergencyAuth = IssueAuthorizationRule.EvaluateEmergency(
            request.IssueType,
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueEmergencyRelease, ct));
        if (emergencyAuth.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Issue>.Blocked(new RuleEvaluation([emergencyAuth]));
        }

        // A massive-transfusion issue belongs to the patient's active protocol activation.
        long? mtpActivationId = null;
        if (_mtpActivations is not null && request.IssueType == IssueType.MassiveTransfusion)
        {
            mtpActivationId = (await _mtpActivations.ListAsync(
                    a => a.PatientId == request.PatientId && a.Status == MtpActivationStatus.Active, ct))
                .OrderByDescending(a => a.ActivatedUtc)
                .Select(a => (long?)a.Id)
                .FirstOrDefault();
            var mtp = MtpActivationRule.EvaluateIssue(request.IssueType, mtpActivationId is not null);
            if (mtp.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<Issue>.Blocked(new RuleEvaluation([mtp]));
            }
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
        if (unit.Status == UnitStatus.ShippedOut)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentShippedOut}: Unit was shipped to {unit.ShippedToFacility ?? "another facility"}.");
        if (unit.Status is UnitStatus.Issued or UnitStatus.Transfused or UnitStatus.TransfusionStarted)
            return EvaluationResult<Issue>.Fail($"{IsbtErrorCodes.ComponentAlreadyIssued}: Unit already issued or transfused.");

        var patient = await _patients.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
        {
            return EvaluationResult<Issue>.Fail("Patient not found.");
        }

        // Fresh scan verification required when the unit has a canonical ISBT identity. A downtime back-entry has no
        // bag to scan, so the DIN typed from the paper record must match the unit instead.
        if (backEntry)
        {
            var din = DowntimeBackEntryRule.ConfirmDin(request.BackEntryDin, unit.UnitNumber, unit.Din);
            if (din.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<Issue>.Blocked(new RuleEvaluation([din]));
            }
        }
        else
        {
            var issueScan = EvaluateRequiredScan(unit, request.VerifiedScan, "at issue");
            if (issueScan is not null)
            {
                return issueScan;
            }
        }

        // OCD-040: appearance is what the issuer saw on the bag, never assumed.
        var issueAppearance = AppearanceAttestationRule.Resolve(
            request.Appearance, await _policy.GetInspectionDefaultAcceptableAsync(ct), "issuing the unit");
        if (issueAppearance.Result.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Issue>.Blocked(new RuleEvaluation([issueAppearance.Result]));
        }

        var now = _clock.UtcNow;
        var gateTime = DowntimeBackEntryRule.GateTime(backEntry, requestedIssuedUtc, now);
        var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == request.PatientId && h.IsCurrent, ct);
        var specimen = backEntry
            ? SelectCurrentSpecimen(await _specimens.ListAsync(
                s => s.PatientId == request.PatientId && s.Status == SpecimenStatus.Accepted && s.CollectedUtc <= gateTime, ct))
            : await GetCurrentSpecimenAsync(request.PatientId, ct);
        var allocationId = (await _allocations.ListAsync(
                a => a.BloodProductId == unit.Id && a.PatientId == request.PatientId
                    && (a.Status == AllocationStatus.Reserved || a.Status == AllocationStatus.Expired), ct))
            .Where(a => DowntimeBackEntryRule.ReservationCounts(backEntry, a.Status, a.AllocatedUtc, gateTime))
            .OrderBy(a => a.Status == AllocationStatus.Reserved ? 0 : 1)
            .Select(a => (long?)a.Id)
            .FirstOrDefault();
        // Loaded by id so the issue can mark it consumed.
        var allocation = allocationId is long chosen ? await _allocations.GetByIdAsync(chosen, ct) : null;

        var unitCrossmatches = await _crossmatches.ListAsync(
            x => x.BloodProductId == unit.Id && x.PatientId == request.PatientId, ct);
        var hasValidCrossmatch = unitCrossmatches.Any(x =>
            (!backEntry || x.PerformedUtc <= gateTime)
            && CrossmatchValidityRule.IsCurrentCompatible(x.Result, x.ExpiresUtc, gateTime));
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

        var (specialMet, unmetUnitRequirements) = await SpecialRequirementsSatisfiedAsync(
            request.PatientId, unit, productType, bloodAttrs.UnitAntigens, gateTime, request.AcknowledgedSpecialRequirementCodes, bloodType, ct);
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
        // OCD-074: an infant's type from this admission's specimen stands for routine issue without a second determination.
        var neonatalTypeStands = await NeonatalAdmissionTypeStandsAsync(typeHistory, ct);
        var secondAbo = SecondAboDeterminationRule.HasSecondConcordant(
                SecondAboDeterminationRule.FromHistory(typeHistory))
            || neonatalTypeStands;
        // OCD-074 part three: group O red cells for an infant with a negative screen need no crossmatch (routine issue).
        var neonatalNoCrossmatch = neonatalTypeStands
            && !hasValidCrossmatch
            && request.IssueType is not (IssueType.EmergencyRelease or IssueType.MassiveTransfusion)
            && await NeonatalNoCrossmatchAsync(request.PatientId, productType?.ComponentClass, unit.Abo, ct);
        var exmEligible = facilityAllowsExm && await IsElectronicCrossmatchEligibleAsync(request.PatientId, ct);
        var issueType = ResolveIssueType(request.IssueType, location, exmEligible, hasValidCrossmatch);
        var retypeConfirmed = await ProductRetypeConfirmation.IsConfirmedAsync(_retypes, productType, unit.Id, ct);
        var retypeMismatch = await ProductRetypeConfirmation.HasVerifiedMismatchAsync(_retypes, unit.Id, ct);
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
            UnitStatus = DowntimeBackEntryRule.StatusForGate(backEntry, unit.Status, unit.ExpiresUtc, gateTime),
            UnitExpiresUtc = unit.ExpiresUtc,
            UnitNearExpiryWindow = BloodUnitExpirationRule.NearExpiryWindow(
                postThawDating, TimeSpan.FromHours(nearExpiryHours)),
            AllocatedToThisPatient = allocation is not null || emergencyUnretyped,
            RetypeConfirmed = retypeConfirmed,
            UnitRetypeMismatch = retypeMismatch,
            RequiresCrossmatch = productType?.RequiresCrossmatch ?? false,
            HasValidCrossmatch = hasValidCrossmatch,
            HasIncompatibleCrossmatch = hasIncompatibleCrossmatch,
            NeonatalNoCrossmatch = neonatalNoCrossmatch,
            IsEmergencyRelease = issueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion,
            ProductTypeMatchesOrder = productMatches,
            PatientSignificantAntibodies = bloodAttrs.PatientSignificantAntibodies,
            PatientAntigens = bloodAttrs.PatientAntigens,
            UnitSignificantAntibodies = bloodAttrs.UnitSignificantAntibodies,
            UnitAntigens = bloodAttrs.UnitAntigens,
            SpecialRequirementsMet = specialMet,
            UnmetUnitRequirements = unmetUnitRequirements,
            UnresolvedAboRhDiscrepancy = unresolvedDelta,
            VisualInspectionAcceptable = request.VisualInspectionAcceptable
                && IssueAppearanceRule.IsAcceptable(issueAppearance.Appearance),
            Appearance = issueAppearance.Appearance,
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
            PatientAgeYears = AgeYears(patient.DateOfBirth, gateTime),
            UncrossmatchedMustBeGroupO = uncrossmatchedGroupO,
            UncrossmatchedONegForChildbearing = uncrossmatchedONeg,
            ChildbearingAgeYears = childbearingAge,
            DonationRestriction = unit.DonationRestriction,
            ReservedPatientId = unit.ReservedPatientId,
            IssuePatientId = request.PatientId,
            PatientStatus = patient.Status,
            OrderLinked = linkedOrder is not null,
            OrderIsFulfillable = linkedOrder is null || OrderControlRule.IsFulfillable(linkedOrder.Status),
            NowUtc = gateTime
        };

        // OCD-052: the active compatibility table can make a gate rule stricter for this component class, never weaker.
        var evaluation = CompatibilityTablePolicy.Apply(
            IssueGate.Evaluate(context), context.ComponentClass, await LoadActiveCompatibilityTableAsync(ct));
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

        // OCD-078: another unit while a reaction is under investigation is the physician's decision, overridden and reviewed.
        var openReactionAtGate = (await _reactions.OpenByPatientAsync([request.PatientId], ct))
            .TryGetValue(request.PatientId, out var reactionAtGate) ? reactionAtGate : ((long Id, DateTime ReportedUtc)?)null;
        var reactionOpen = ReactionUnderInvestigationRule.Evaluate(openReactionAtGate?.Id, openReactionAtGate?.ReportedUtc, requestedIssuedUtc);
        if (reactionOpen.Severity != RuleSeverity.Pass)
        {
            evaluation = new RuleEvaluation(evaluation.Results.Append(reactionOpen).ToList());
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
                return EvaluationResult<Issue>.Blocked(OverrideFieldsRule.WithMissing(evaluation, request.OverrideReason, request.AuthorizedBy));
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
                Action = OverrideActionRule.ForIssue(issueType),
                ContextType = nameof(Issue),
                ContextId = unit.Id,
                RuleCode = string.Join(",", evaluation.Warnings.Select(r => r.Code)),
                Reason = request.OverrideReason.Trim(),
                AuthorizedBy = request.AuthorizedBy.Trim(),
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

        // A reaction still under investigation for the patient is recorded with the issue (Cycle 634): it warns, it does
        // not block (OCD-078), and a later review can find the units given meanwhile.
        var openReaction = openReactionAtGate?.Id;
        var issue = new Issue
        {
            ReactionInvestigationOpenAtIssueId = openReaction,
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
                && IssueAppearanceRule.IsAcceptable(issueAppearance.Appearance),
            IssueAppearance = issueAppearance.Appearance,
            SecondVerifier = request.SecondVerifier,
            PatientIdentifier1 = request.PatientIdentifier1Value,
            PatientIdentifier2 = request.PatientIdentifier2Value,
            MtpActivationId = mtpActivationId,
            LateEntryReason = lateEntryReason,
            DowntimeBackEntry = backEntry,
            OutsideOutageReason = outsideOutageReason,
            NeonatalNoCrossmatch = neonatalNoCrossmatch,
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
            Reason = backEntry ? $"Downtime back-entry: issued {issuedUtc:yyyy-MM-dd HH:mm} UTC on paper" : issueType switch
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
                BloodProductId = unit.Id,
                IssueAppearance = issueAppearance.Appearance,
                AppearanceFromFacilityDefault = issueAppearance.UsedDefault,
                MtpActivationId = mtpActivationId,
                IssuedUtc = issuedUtc,
                EnteredUtc = now,
                LateEntryReason = lateEntryReason,
                DowntimeBackEntry = backEntry,
                OutsideOutageReason = outsideOutageReason,
                NeonatalNoCrossmatch = neonatalNoCrossmatch,
                BackEntryDin = backEntry ? request.BackEntryDin?.Trim() : null
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

        if (await TellEhrOfDispenseAsync(issue.Id, ProductDispenseEvent.Dispensed, unit.UnitNumber, ct) is { } notSent)
        {
            evaluation = new RuleEvaluation(evaluation.Results.Append(notSent).ToList());
        }

        return EvaluationResult<Issue>.Ok(issue, evaluation);
    }

    public Task<Issue?> GetAsync(long id, CancellationToken ct = default) =>
        _issues.GetByIdAsync(id, ct);

    /// <summary>
    /// The most recent issue of the unit with this unit number (exact, ignoring case and spaces), so staff can
    /// work from the number on the bag instead of an internal issue id. Null when the unit or an issue is not found.
    /// </summary>
    public async Task<Issue?> GetLatestForUnitNumberAsync(string unitNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(unitNumber))
        {
            return null;
        }

        var wanted = unitNumber.Trim();
        var candidates = await _inventory.SearchAsync(new InventorySearchCriteria(UnitNumber: wanted), ct);
        var unit = candidates.FirstOrDefault(u => string.Equals(u.UnitNumber, wanted, StringComparison.OrdinalIgnoreCase));
        if (unit is null)
        {
            return null;
        }

        var issues = await _issues.ListAsync(i => i.BloodProductId == unit.Id, ct);
        return issues.OrderByDescending(i => i.IssuedUtc).ThenByDescending(i => i.Id).FirstOrDefault();
    }

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
        // An incompatible retrospective crossmatch is the most urgent row on the board.
        return rows
            .OrderByDescending(i => i.CrossmatchStatus == CrossmatchClinicalStatus.Incompatible)
            .ThenBy(i => i.RetrospectiveCrossmatchDueUtc ?? i.IssuedUtc)
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
        var recalledOut = await RecalledOutAsync(rows, ct);
        return rows
            .OrderBy(i => i.InTransitDueUtc ?? i.IssuedUtc)
            .Select(i =>
            {
                var row = context.For(i, now);
                authorizers.TryGetValue(i.OverrideId ?? 0, out var authorizer);
                return InTransitWorkItemDto.From(
                    i, now, row.Mrn, row.DisplayName, row.UnitNumber, row.CurrentBloodType,
                    row.HasAntibodyHistory, row.AntibodySummary, row.SpecimenExpiresUtc, row.SpecimenExpired,
                    row.ProductName, row.UnitExpiresUtc, row.UnitBloodType, row.AccessionNumber, row.CrossmatchExpiresUtc, row.LatestCrossmatchResult, authorizer, issuePlaces) with
                {
                    UnitRecalled = recalledOut.ContainsKey(i.BloodProductId),
                    RecallReason = recalledOut.GetValueOrDefault(i.BloodProductId)
                };
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
        var aboOrders = await OpenAboRhOrdersAsync(patientIds, ct);

        var items = new List<SecondAboWorkItemDto>();
        foreach (var patient in patients.OrderBy(p => p.LastName).ThenBy(p => p.FirstName))
        {
            var rows = historyByPatient.GetValueOrDefault(patient.Id) ?? [];
            var current = rows.FirstOrDefault(h => h.IsCurrent);
            var second = SecondAboDeterminationRule.HasSecondConcordant(
                    SecondAboDeterminationRule.FromHistory(rows))
                || await NeonatalAdmissionTypeStandsAsync(rows, ct);
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
                "Second concordant ABO/Rh is required before non-emergency red-cell issue.",
                aboOrders.GetValueOrDefault(patient.Id)));
        }

        return items;
    }

    /// <summary>
    /// The oldest open ABO/Rh order per patient: an active ABO/Rh line not yet verified (or retracted and awaiting a
    /// new result) on an order that is not completed, cancelled, or discontinued.
    /// </summary>
    private async Task<Dictionary<long, string>> OpenAboRhOrdersAsync(List<long> patientIds, CancellationToken ct)
    {
        if (_orderLines is null)
        {
            return [];
        }

        var orders = await _orders.ListAsync(o => patientIds.Contains(o.PatientId)
            && o.Status != OrderStatus.Completed
            && o.Status != OrderStatus.Cancelled
            && o.Status != OrderStatus.Discontinued, ct);
        if (orders.Count == 0)
        {
            return [];
        }

        var orderIds = orders.Select(o => o.Id).ToList();
        var lines = await _orderLines.ListAsync(l => orderIds.Contains(l.OrderId)
            && l.IsActive
            && l.OrderType == OrderType.AboRh
            && l.ResultStatus != ResultStatus.Verified
            && l.ResultStatus != ResultStatus.Corrected, ct);
        var withLine = lines.Select(l => l.OrderId).ToHashSet();
        return orders
            .Where(o => withLine.Contains(o.Id))
            .GroupBy(o => o.PatientId)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.OrderedUtc).First().OrderNumber);
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
        var returnMinutes = await _policy.GetReturnToAvailableMinutesAsync(ct);
        var reminderHours = await _policy.GetTransfusionCompletionReminderHoursAsync(ct);
        var openReactions = await _reactions.OpenByPatientAsync(rows.Select(i => i.PatientId).Distinct().ToList(), ct);
        // An issue still Issued with a transfusion record was started at the bedside and is not yet completed.
        var outstandingIds = rows.Select(i => i.Id).ToList();
        var startedByIssue = outstandingIds.Count == 0
            ? new Dictionary<long, DateTime?>()
            : (await _transfusions.ListAsync(tx => outstandingIds.Contains(tx.IssueId) && tx.StopUtc == null, ct))
                .GroupBy(tx => tx.IssueId)
                .ToDictionary(g => g.Key, g => g.Max(tx => tx.StartUtc));
        var locationIds = rows.Select(i => context.StorageLocationId(i)).OfType<long>().Distinct().ToList();
        var locations = locationIds.Count == 0
            ? new Dictionary<long, InventoryLocation>()
            : (await _locations.ListAsync(l => locationIds.Contains(l.Id), ct)).ToDictionary(l => l.Id);
        var recalledOut = await RecalledOutAsync(rows, ct);
        return rows
            .OrderBy(i => i.IssuedUtc)
            .Select(i =>
            {
                var row = context.For(i, now);
                authorizers.TryGetValue(i.OverrideId ?? 0, out var authorizer);
                var governed = true;
                if (context.StorageLocationId(i) is long locationId && locations.TryGetValue(locationId, out var location))
                {
                    governed = location.TimeOutGoverned;
                }

                var acceptable = ReturnReissueRule.TimeOutAcceptable(governed, i.IssuedUtc, now, returnMinutes);
                return OutstandingIssueWorkItemDto.From(
                    i, now, row.Mrn, row.DisplayName, row.DateOfBirth, row.UnitNumber, row.CurrentBloodType,
                    row.HasAntibodyHistory, row.AntibodySummary, row.SpecimenExpiresUtc, row.SpecimenExpired,
                    row.ProductName, row.UnitExpiresUtc, row.UnitBloodType, row.AccessionNumber, row.CrossmatchExpiresUtc, row.LatestCrossmatchResult, authorizer, issuePlaces,
                    governed, acceptable) with
                {
                    TransfusionStartedUtc = startedByIssue.GetValueOrDefault(i.Id),
                    TransfusionCompletionOverdue = TransfusionCompletionRule.IsOverdue(startedByIssue.GetValueOrDefault(i.Id), now, reminderHours),
                    OpenReactionInvestigationId = openReactions.TryGetValue(i.PatientId, out var reaction) ? reaction.Id : null,
                    OpenReactionReportedUtc = openReactions.TryGetValue(i.PatientId, out var reported) ? reported.ReportedUtc : null,
                    UnitRecalled = recalledOut.ContainsKey(i.BloodProductId),
                    RecallReason = recalledOut.GetValueOrDefault(i.BloodProductId)
                };
            })
            // Transfusions whose completion is overdue lead; the rest stay in issue order.
            .OrderByDescending(r => r.TransfusionCompletionOverdue)
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
        // The open orders the units are reserved for: when each is needed, so the desk issues the most pressing first.
        var reservedOrderIds = reserved.Select(a => a.OrderId).OfType<long>().Distinct().ToList();
        var reservedOrders = reservedOrderIds.Count == 0
            ? new Dictionary<long, Order>()
            : (await _orders.ListAsync(o => reservedOrderIds.Contains(o.Id)
                    && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Discontinued, ct))
                .ToDictionary(o => o.Id);
        var neededSoonMinutes = await _policy.GetBloodOrderNeededBySoonMinutesAsync(ct);
        var openReactions = await _reactions.OpenByPatientAsync(reserved.Select(a => a.PatientId).Distinct().ToList(), ct);
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
        var requirementsByPatient = await SpecialRequirementLoader.ByPatientAsync(
            _specialRequirements, _requirementDefinitions, patientIds, ct);
        var attributesByProduct = await SpecialRequirementLoader.ProductAttributeCodesByProductTypeAsync(
            _productAttributeAssignments, _productAttributes, ct);
        var negativeTestsByUnit = await SpecialRequirementLoader.NegativeTestCodesByUnitAsync(
            _unitSpecialTests, reserved.Select(a => a.BloodProductId).Distinct().ToList(), ct);
        var encountersByPatient = _encounters is null
            ? new Dictionary<long, List<Encounter>>()
            : (await _encounters.ListAsync(e => patientIds.Contains(e.PatientId), ct))
                .GroupBy(e => e.PatientId).ToDictionary(g => g.Key, g => g.ToList());

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
            var readyTypeStands = await NeonatalAdmissionTypeStandsAsync(typeRows, ct);
            var needsSecondAbo = requireSecondAbo
                && cellular
                && !SecondAboDeterminationRule.HasSecondConcordant(
                    SecondAboDeterminationRule.FromHistory(typeRows))
                && !readyTypeStands;
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
            // OCD-074 part three: the same waiver issue applies, so the row does not ask for a crossmatch it will not need.
            if (requiresXm && readyTypeStands && unit is not null
                && await NeonatalNoCrossmatchAsync(allocation.PatientId, unit.ProductType?.ComponentClass, unit.Abo, ct))
            {
                requiresXm = false;
            }
            var crossmatchCue = CrossmatchValidityRule.DescribeReadyToIssue(
                requiresXm, hasValidCrossmatch, hasIncompatibleCrossmatch);
            var discrepancyCue = IssueGate.DescribeAboRhDiscrepancy(
                IssueGate.HasUnresolvedAboRhDiscrepancy(IssueGate.DiscrepancyInput(typeRows)),
                requiresXm);
            var recordedXm = latestXm is { Result: CrossmatchResult.Compatible or CrossmatchResult.Incompatible }
                ? latestXm.Result
                : (CrossmatchResult?)null;
            var display = ProductAllocationDisplayStatusRule.Evaluate(
                requiresXm,
                latestXm?.Result,
                latestXm?.Result == CrossmatchResult.Incompatible,
                hasValidCrossmatch);
            var specimen = SelectCurrentSpecimen(specimens.Where(s => s.PatientId == allocation.PatientId));
            var forOrder = allocation.OrderId is long reservedFor ? reservedOrders.GetValueOrDefault(reservedFor) : null;
            var expires = specimen?.ExpiresUtc;
            var specimenExpired = expires is DateTime exp && now >= exp;
            var (specimenBlocksIssue, specimenBlockReason) = DescribeSpecimenBlock(specimen, specimenExpired);
            var preSpecimenEmergency = false;
            if (specimenBlocksIssue
                && unit?.ProductType is { } preSpecimenProduct
                && EmergencyPreSpecimenRule.Applies(true, specimen is not null, specimen?.ExpiresUtc, now))
            {
                preSpecimenEmergency = EmergencyPreSpecimenRule.Evaluate(
                    preSpecimenProduct.ComponentClass, new AboRh(unit.Abo, unit.RhD)).Severity == RuleSeverity.Warning;
                specimenBlockReason += preSpecimenEmergency
                    ? " Emergency release of this unit is possible with an authorized override (OCD-053)."
                    : " Before a specimen is drawn only group O red cells or group AB or A plasma can be released.";
            }
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
                    now,
                    unit.ProductType is { } typedProduct
                        ? SpecialRequirementLoader.Typing(unit, typedProduct.ComponentClass, negativeTestsByUnit)
                        : null);
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
                specimen?.AccessionNumber,
                preSpecimenEmergency,
                allocation.ExpiresUtc,
                allocation.IssueRequestedUtc,
                allocation.IssueRequestedBy,
                allocation.IssueRequestedTo,
                PatientVisitLocation.For(allocation.EncounterId, encountersByPatient.GetValueOrDefault(allocation.PatientId) ?? []))
            {
                OrderNumber = forOrder?.OrderNumber,
                NeededByUtc = forOrder?.NeededByUtc,
                NeededByPassed = OrderNeededByRule.Passed(forOrder?.NeededByUtc, now),
                NeededBySoon = OrderNeededByRule.DueSoon(forOrder?.NeededByUtc, now, neededSoonMinutes),
                NeededInMinutes = OrderNeededByRule.DueSoon(forOrder?.NeededByUtc, now, neededSoonMinutes)
                    ? OrderNeededByRule.MinutesUntil(forOrder?.NeededByUtc, now)
                    : null,
                ExpiresBeforeNeeded = OrderNeededByRule.ExpiresBeforeNeeded(
                    OrderNeededByRule.EarliestEnd(
                        CrossmatchValidityRule.CurrentCompatibleExpiresUtc(unitCrossmatches.Select(x => (x.Result, x.ExpiresUtc)), now),
                        expires),
                    forOrder?.NeededByUtc, now),
                OpenReactionInvestigationId = openReactions.TryGetValue(allocation.PatientId, out var reaction) ? reaction.Id : null,
                OpenReactionReportedUtc = openReactions.TryGetValue(allocation.PatientId, out var reported) ? reported.ReportedUtc : null
            });
        }

        return ReadyToIssueAttentionRule.IssuableFirst(items);
    }

    /// <summary>The rows of the active compatibility table version; empty when none is active or configured.</summary>
    private async Task<IReadOnlyList<CompatibilityTableRow>> LoadActiveCompatibilityTableAsync(CancellationToken ct)
    {
        if (_compatibilityVersions is null || _compatibilityRules is null)
        {
            return [];
        }

        var active = await _compatibilityVersions.FirstOrDefaultAsync(v => v.IsActive, ct);
        if (active is null)
        {
            return [];
        }

        return (await _compatibilityRules.ListAsync(r => r.CompatibilityRuleVersionId == active.Id && r.IsActive, ct))
            .Select(r => new CompatibilityTableRow(r.RuleCode, r.ComponentClass, r.Severity, r.IsActive))
            .ToList();
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

        // One wording with the chart header: an antibody known only from another facility's report says so.
        return string.Join(", ", history.Select(a =>
            OutsideAntibodyReportRule.HistoryEntry(a.AntibodySpecificity, a.IsActive, a.ReportingFacility, a.IdentifiedOn, a.ConfirmedUtc is not null)));
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

        var wardAppearance = AppearanceAttestationRule.Resolve(
            request.Appearance, await _policy.GetInspectionDefaultAcceptableAsync(ct), "acknowledging receipt");
        if (wardAppearance.Result.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Issue>.Blocked(new RuleEvaluation([wardAppearance.Result]));
        }

        var appearance = WardAppearanceRule.Evaluate(wardAppearance.Appearance);
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

        // Cycle 692: a unit recalled on its way is not accepted on the ward; it goes back as it came.
        if (unit.Status == UnitStatus.Recalled)
        {
            return EvaluationResult<Issue>.Fail(
                $"{IsbtErrorCodes.ComponentRecalled}: {unit.UnitNumber} was recalled after it was issued"
                + (string.IsNullOrWhiteSpace(unit.RecallReason) ? "" : $" ({unit.RecallReason.Trim()})")
                + ". Do not accept it on the ward; return it to the blood bank.");
        }

        var wardPaperDin = !string.IsNullOrWhiteSpace(request.BackEntryDin);
        if (wardPaperDin)
        {
            var paper = await EvaluatePaperDinAsync(issue, unit, request.BackEntryDin, ct);
            if (paper.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<Issue>.Blocked(new RuleEvaluation([paper]));
            }

            var paperTime = DowntimeBackEntryRule.EvaluateReceiptTime(ToUtc(request.ReceivedUtc), issue.IssuedUtc, _clock.UtcNow);
            if (paperTime.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<Issue>.Blocked(new RuleEvaluation([paperTime]));
            }
        }
        else
        {
            var wardScan = EvaluateRequiredScan(unit, request.VerifiedScan, "at ward receipt");
            if (wardScan is not null)
            {
                return wardScan;
            }
        }

        var now = _clock.UtcNow;
        // A receipt back-entered from paper is judged, and stamped, at the time on the paper record.
        var receivedUtc = wardPaperDin ? ToUtc(request.ReceivedUtc)!.Value : now;
        var overdue = InTransitPendingRule.EvaluateOverdue(issue.InTransitDueUtc, receivedUtc);
        issue.WardReceivedUtc = receivedUtc;
        issue.WardReceivedBy = request.ReceivedBy.Trim();
        issue.WardVisualAcceptable = true;
        issue.WardAppearance = wardAppearance.Appearance;
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
                issue.WardAppearance,
                AppearanceFromFacilityDefault = wardAppearance.UsedDefault,
                Late = overdue.Severity == RuleSeverity.Warning,
                unit.UnitNumber,
                unit.Din,
                BloodProductId = unit.Id,
                IssueId = issue.Id,
                EnteredUtc = now,
                FromPaperRecord = wardPaperDin
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

        // Cycle 691: a unit recalled while it was out comes back to quarantine for the recall, whatever the inspection;
        // returning it to stock would undo the recall.
        if (unit.Status == UnitStatus.Recalled)
        {
            if (request.Destination == UnitStatus.Available)
            {
                return EvaluationResult<Return>.Fail(
                    $"{IsbtErrorCodes.ComponentRecalled}: {unit.UnitNumber} was recalled while issued; it returns to quarantine, not to stock.");
            }

            return await ReturnRecalledAsync(issue, unit, request.Reason, ct);
        }

        // Cycle 693: a unit already quarantined while it was out (for example pulled for a recall) stays in quarantine
        // when its issue is returned; the inspection cannot send it to stock.
        if (unit.Status == UnitStatus.Quarantine)
        {
            if (request.Destination == UnitStatus.Available)
            {
                return EvaluationResult<Return>.Fail(
                    $"{IsbtErrorCodes.ComponentQuarantined}: {unit.UnitNumber} is in quarantine ({unit.QuarantineReason ?? unit.QuarantineReasonCode.ToString()}); it stays there, not in stock.");
            }

            return await ReturnQuarantinedAsync(issue, unit, request.Reason, ct);
        }

        // Cycle 694: a unit reported damaged or missing while it was out comes back to be dealt with as that, never to stock.
        if (unit.Status is UnitStatus.Damaged or UnitStatus.Missing)
        {
            if (request.Destination == UnitStatus.Available)
            {
                return EvaluationResult<Return>.Fail(
                    $"{unit.UnitNumber} was reported {unit.Status.ToString().ToLowerInvariant()} while issued; it does not return to stock.");
            }

            return await ReturnReportedAsync(issue, unit, request.Reason, ct);
        }

        if (unit.Status is not (UnitStatus.Issued or UnitStatus.ReturnPending))
        {
            return EvaluationResult<Return>.Fail(
                $"{IsbtErrorCodes.InvalidStatusTransition}: {unit.UnitNumber} is {unit.Status}; it cannot be returned.");
        }

        var now = _clock.UtcNow;
        var unitUnexpired = unit.ExpiresUtc > now;
        var returnMinutes = await _policy.GetReturnToAvailableMinutesAsync(ct);
        var governed = await StorageTimeOutGovernedAsync(unit.CurrentLocationId, ct);
        var timeOk = ReturnReissueRule.TimeOutAcceptable(governed, issue.IssuedUtc, now, returnMinutes);
        var inspection = ReturnInspectionRule.Resolve(
            request.TemperatureAcceptable,
            request.SealIntegrityAcceptable,
            request.VisualInspectionAcceptable,
            await _policy.GetInspectionDefaultAcceptableAsync(ct),
            mayReturnToAvailable: request.Destination is null or UnitStatus.Available);
        if (inspection.Result.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<Return>.Blocked(new RuleEvaluation([inspection.Result]));
        }

        var reissueEval = ReturnReissueRule.Evaluate(
            inspection.TemperatureAcceptable,
            inspection.SealIntegrityAcceptable,
            inspection.VisualInspectionAcceptable,
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
        // A return never disposes of a unit: disposal is the discard step (permission, coded reason, second verifier).
        var inspectionFailed = !inspection.VisualInspectionAcceptable || !inspection.SealIntegrityAcceptable;
        var disposalRequested = request.Destination == UnitStatus.Discarded;
        UnitStatus destination;
        if (request.Destination is UnitStatus requested)
        {
            if (requested is not (UnitStatus.Available or UnitStatus.Quarantine or UnitStatus.Discarded))
            {
                return EvaluationResult<Return>.Fail(
                    "Issued units can only be returned to Available or Quarantine.");
            }

            if (requested == UnitStatus.Available && !reissueOk)
            {
                return EvaluationResult<Return>.Fail(
                    "Return inspection does not allow Available. Choose Quarantine.");
            }

            destination = requested == UnitStatus.Available ? UnitStatus.Available : UnitStatus.Quarantine;
        }
        else
        {
            destination = reissueOk ? UnitStatus.Available : UnitStatus.Quarantine;
        }

        // An emergency release can issue a unit before its retype. Back in stock it goes to Received, not Available,
        // so the retype worklist lists it again and the retype can still be recorded.
        var retypePending = false;
        if (destination == UnitStatus.Available)
        {
            var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
            var retypeConfirmed = await ProductRetypeConfirmation.IsConfirmedAsync(_retypes, productType, unit.Id, ct);
            destination = RetypeStockStatus.ForStock(productType?.RequiresRetype == true, retypeConfirmed);
            retypePending = destination == UnitStatus.Received;
        }

        var returnRecord = new Return
        {
            IssueId = issue.Id,
            Issue = issue,
            BloodProductId = unit.Id,
            ReturnedUtc = now,
            ReturnedBy = _currentUser.UserName,
            Reason = request.Reason,
            ReissueEligible = reissueOk,
            ReissueEvaluationJson = JsonSerializer.Serialize(new
            {
                inspection.TemperatureAcceptable,
                inspection.SealIntegrityAcceptable,
                inspection.VisualInspectionAcceptable,
                InspectionUnanswered = inspection.AnyUnanswered,
                InspectionFromFacilityDefault = inspection.UsedFacilityDefault,
                TimeOutOfStorageAcceptable = timeOk,
                TimeOutGoverned = governed,
                CallerTimeOutOfStorageAcceptable = request.TimeOutOfStorageAcceptable,
                RetypePending = retypePending,
                RequestedDestination = request.Destination,
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
            unit.QuarantineReasonCode = ReturnDisposalRule.QuarantineReason(disposalRequested, inspectionFailed);
        }

        AppendStatus(unit, destination, retypePending ? $"{request.Reason} ({RetypeStockStatus.PendingReason})" : request.Reason,
            now, nameof(Return), issue.Id);

        _audit.Record(
            AuditEventType.Return,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Issued },
            newValue: new { Status = destination, ReissueEligible = reissueOk, RetypePending = retypePending, RequestedDestination = request.Destination },
            reason: request.Reason);

        await _unitOfWork.SaveChangesAsync(ct);
        var notices = new List<RuleResult>();
        if (disposalRequested || inspectionFailed)
        {
            notices.Add(ReturnDisposalRule.Notice(unit.UnitNumber, disposalRequested, inspectionFailed));
        }

        if (await TellEhrOfDispenseAsync(issue.Id, ProductDispenseEvent.Returned, unit.UnitNumber, ct) is { } notSent)
        {
            notices.Add(notSent);
        }

        return EvaluationResult<Return>.Ok(returnRecord, notices.Count == 0 ? null : new RuleEvaluation(notices));
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

    /// <summary>The refusal when a transfusion is finished without saying whether a reaction is suspected.</summary>
    public const string ReactionAnswerRequired = "Say whether a transfusion reaction is suspected (yes or no) before finishing the transfusion.";

    /// <summary>The refusal when a transfusion is finished without saying how it ended.</summary>
    public const string DispositionRequired = "Choose how the transfusion ended (completed, stopped, or wasted) before finishing it.";

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

        if (request.ReactionSuspected is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail(ReactionAnswerRequired);
        }

        if (request.FinalDisposition is not TransfusionDisposition disposition)
        {
            return EvaluationResult<TransfusionEvent>.Fail(DispositionRequired);
        }

        if (VitalsProblem(request.PreVitals, request.PostVitals) is { } documentVitals)
        {
            return EvaluationResult<TransfusionEvent>.Fail(documentVitals);
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

        if (await FindStartedTransfusionAsync(issue, ct) is TransfusionEvent started)
        {
            return EvaluationResult<TransfusionEvent>.Fail(
                $"{TransfusionDocumentationRule.Code}: This transfusion was started at {started.StartUtc:yyyy-MM-dd HH:mm} UTC. Complete it instead of documenting it again.");
        }

        var documentation = TransfusionDocumentationRule.Evaluate(
            request.StartUtc, request.StopUtc, request.VolumeTransfused, unit.Volume, _clock.UtcNow);
        if (documentation.IsHardStopped)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(documentation);
        }

        var bedside = await EvaluateBedsideAsync(issue, unit, request.StartUtc ?? _clock.UtcNow, new BedsideIdentification(
            request.PatientIdentifier1Type, request.PatientIdentifier1Value,
            request.PatientIdentifier2Type, request.PatientIdentifier2Value,
            request.BedsideScan, request.SecondVerifier, request.BackEntryDin), ct);
        if (bedside.Blocked is not null)
        {
            return bedside.Blocked;
        }

        var reactionCheck = await OverrideReactionAtBedsideAsync(issue, unit, request.StartUtc ?? _clock.UtcNow,
            request.OverrideReason, request.AuthorizedBy, ct);
        if (reactionCheck is not null)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(reactionCheck);
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
            ReactionSuspected = request.ReactionSuspected.Value,
            FinalDisposition = disposition,
            DocumentedBy = _currentUser.UserName,
            SecondVerifier = request.SecondVerifier,
            Location = request.Location,
            PatientIdentificationMethod = request.PatientIdentificationMethod
                ?? $"{request.PatientIdentifier1Type}+{request.PatientIdentifier2Type}",
            UnitIdentificationMethod = request.UnitIdentificationMethod
                ?? (!string.IsNullOrWhiteSpace(request.BackEntryDin) ? DowntimeBackEntryRule.PaperDinMethod
                    : request.BedsideScan is null ? null : "BedsideScan"),
            WorkstationId = _currentUser.Workstation,
            BedsideScanVerificationJson = request.BedsideScan is null ? null : JsonSerializer.Serialize(request.BedsideScan),
            PreTransfusionVitalsJson = request.PreVitals?.ToJson(),
            VitalsJson = request.PostVitals?.ToJson()
        };
        await _transfusions.AddAsync(transfusion, ct);

        if (unit.Status == UnitStatus.Issued)
            AppendStatus(unit, UnitStatus.TransfusionStarted, "Transfusion started", now, nameof(TransfusionEvent), issue.Id);

        var terminal = disposition == TransfusionDisposition.Stopped
            ? UnitStatus.TransfusionStopped
            : UnitStatus.Transfused;

        issue.Status = IssueStatus.Transfused;
        AppendStatus(unit, terminal, $"Transfusion {disposition}", now, nameof(TransfusionEvent), issue.Id);

        _audit.Record(
            AuditEventType.Transfusion,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Issued },
            newValue: new { Status = terminal, FinalDisposition = disposition, request.ReactionSuspected },
            reason: "Transfusion documented.");

        await _unitOfWork.SaveChangesAsync(ct);

        if (request.ReactionSuspected == true)
        {
            await _reactions.OpenForTransfusionAsync(transfusion, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return EvaluationResult<TransfusionEvent>.Ok(transfusion);
    }

    /// <summary>
    /// OCD-078: a unit started at the bedside after a reaction was reported for the patient, while it is still under
    /// investigation, needs the reason and who authorized it. Returns the evaluation to block with, or null to go on;
    /// an override given is added (recorded and audited with the transfusion's save) for supervisor review.
    /// </summary>
    private async Task<RuleEvaluation?> OverrideReactionAtBedsideAsync(
        Issue issue, BloodUnit unit, DateTime startUtc, string? reason, string? authorizedBy, CancellationToken ct)
    {
        var open = (await _reactions.OpenByPatientAsync([issue.PatientId], ct))
            .TryGetValue(issue.PatientId, out var found) ? found : ((long Id, DateTime ReportedUtc)?)null;
        var warning = ReactionUnderInvestigationRule.Evaluate(open?.Id, open?.ReportedUtc, startUtc);
        if (warning.Severity == RuleSeverity.Pass)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(authorizedBy))
        {
            return OverrideFieldsRule.WithMissing(new RuleEvaluation([warning]), reason, authorizedBy);
        }

        var recorded = new Override
        {
            Action = OverrideAction.WarningOverride,
            ContextType = nameof(Issue),
            ContextId = unit.Id,
            RuleCode = ReactionUnderInvestigationRule.Code,
            Reason = reason.Trim(),
            AuthorizedBy = authorizedBy.Trim(),
            OverriddenUtc = _clock.UtcNow
        };
        await _overrides.AddAsync(recorded, ct);
        _audit.Record(
            AuditEventType.Override,
            nameof(Issue),
            unit.Id,
            newValue: new { RuleCode = ReactionUnderInvestigationRule.Code, ReactionInvestigationId = open!.Value.Id, IssueId = issue.Id, AuthorizedBy = recorded.AuthorizedBy },
            reason: recorded.Reason);
        return null;
    }

    /// <summary>
    /// Records the start of a transfusion at the bedside, when the unit is spiked: the same identity, unit scan,
    /// ward receipt, second-verifier, and unit-life checks as one-step documentation. The unit shows as
    /// Transfusion started until <see cref="CompleteTransfusionAsync"/> records the stop and outcome.
    /// </summary>
    /// <summary>A mistyped vital sign, before or after, stops the save; vitals left blank are fine.</summary>
    private static string? VitalsProblem(BloodBankLIS.Domain.ValueObjects.TransfusionVitals? before, BloodBankLIS.Domain.ValueObjects.TransfusionVitals? after)
    {
        var problems = (before?.Problems("Before") ?? []).Concat(after?.Problems("After") ?? []).ToList();
        return problems.Count == 0 ? null : string.Join(" ", problems);
    }

    public async Task<EvaluationResult<TransfusionEvent>> StartTransfusionAsync(long issueId, StartTransfusionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var auth = IssueAuthorizationRule.EvaluateStartTransfusion(
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.TransfusionStart, ct)
            || await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.TransfusionDocument, ct));
        if (auth.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([auth]));
        }

        if (VitalsProblem(request.PreVitals, null) is { } startVitals)
        {
            return EvaluationResult<TransfusionEvent>.Fail(startVitals);
        }

        var issue = await _issues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Issue not found.");
        }

        if (issue.Status != IssueStatus.Issued)
        {
            return EvaluationResult<TransfusionEvent>.Fail($"An issue with status {issue.Status} cannot be started.");
        }

        var unit = await _inventory.GetUnitAsync(issue.BloodProductId, ct);
        if (unit is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Unit not found.");
        }

        // Cycle 691: a unit recalled while it was out must not be transfused; say so rather than a status mismatch.
        if (unit.Status == UnitStatus.Recalled)
        {
            return EvaluationResult<TransfusionEvent>.Fail(
                $"{IsbtErrorCodes.ComponentRecalled}: {unit.UnitNumber} was recalled after it was issued"
                + (string.IsNullOrWhiteSpace(unit.RecallReason) ? "" : $" ({unit.RecallReason.Trim()})")
                + ". Do not transfuse it; return it to the blood bank.");
        }

        if (unit.Status != UnitStatus.Issued || await _transfusions.AnyAsync(t => t.IssueId == issue.Id, ct))
        {
            return EvaluationResult<TransfusionEvent>.Fail(
                $"{IsbtErrorCodes.InvalidStatusTransition}: This issue already has a transfusion record, so it cannot be started again.");
        }

        var startUtc = request.StartUtc ?? _clock.UtcNow;
        var times = TransfusionDocumentationRule.Evaluate(startUtc, null, null, unit.Volume, _clock.UtcNow);
        if (times.IsHardStopped)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(times);
        }

        var bedside = await EvaluateBedsideAsync(issue, unit, startUtc, new BedsideIdentification(
            request.PatientIdentifier1Type, request.PatientIdentifier1Value,
            request.PatientIdentifier2Type, request.PatientIdentifier2Value,
            request.BedsideScan, request.SecondVerifier, request.BackEntryDin), ct);
        if (bedside.Blocked is not null)
        {
            return bedside.Blocked;
        }

        var reactionCheck = await OverrideReactionAtBedsideAsync(issue, unit, startUtc, request.OverrideReason, request.AuthorizedBy, ct);
        if (reactionCheck is not null)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(reactionCheck);
        }

        var now = _clock.UtcNow;
        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = issue.PatientId,
            StartUtc = startUtc,
            Transfusionist = request.Transfusionist,
            DocumentedBy = _currentUser.UserName,
            SecondVerifier = request.SecondVerifier,
            Location = request.Location,
            PatientIdentificationMethod = $"{request.PatientIdentifier1Type}+{request.PatientIdentifier2Type}",
            UnitIdentificationMethod = !string.IsNullOrWhiteSpace(request.BackEntryDin) ? DowntimeBackEntryRule.PaperDinMethod
                : request.BedsideScan is null ? null : "BedsideScan",
            WorkstationId = _currentUser.Workstation,
            BedsideScanVerificationJson = request.BedsideScan is null ? null : JsonSerializer.Serialize(request.BedsideScan),
            PreTransfusionVitalsJson = request.PreVitals?.ToJson()
        };
        await _transfusions.AddAsync(transfusion, ct);
        AppendStatus(unit, UnitStatus.TransfusionStarted, "Transfusion started at the bedside", now, nameof(TransfusionEvent), issue.Id);
        _audit.Record(
            AuditEventType.Transfusion,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Issued },
            newValue: new { Status = UnitStatus.TransfusionStarted, StartUtc = startUtc, request.SecondVerifier },
            reason: "Transfusion started.");
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransfusionEvent>.Ok(transfusion);
    }

    /// <summary>
    /// Completes a transfusion started at the bedside: stop time, volume, outcome, and a suspected reaction (which
    /// opens an investigation). The patient was identified at the start, so this does not ask again.
    /// </summary>
    public async Task<EvaluationResult<TransfusionEvent>> CompleteTransfusionAsync(long issueId, CompleteTransfusionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var auth = IssueAuthorizationRule.EvaluateCompleteTransfusion(
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.TransfusionComplete, ct)
            || await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.TransfusionDocument, ct));
        if (auth.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([auth]));
        }

        if (request.FinalDisposition == TransfusionDisposition.Returned)
        {
            return EvaluationResult<TransfusionEvent>.Fail("A started transfusion cannot be returned to inventory. Record it as stopped or wasted.");
        }

        if (request.ReactionSuspected is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail(ReactionAnswerRequired);
        }

        if (request.FinalDisposition is not TransfusionDisposition disposition)
        {
            return EvaluationResult<TransfusionEvent>.Fail(DispositionRequired);
        }

        if (VitalsProblem(null, request.PostVitals) is { } completeVitals)
        {
            return EvaluationResult<TransfusionEvent>.Fail(completeVitals);
        }

        var issue = await _issues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return EvaluationResult<TransfusionEvent>.Fail("Issue not found.");
        }

        var transfusion = issue.Status == IssueStatus.Issued ? await FindStartedTransfusionAsync(issue, ct) : null;
        var unit = await _inventory.GetUnitAsync(issue.BloodProductId, ct);
        if (transfusion is null || unit is null || unit.Status != UnitStatus.TransfusionStarted)
        {
            return EvaluationResult<TransfusionEvent>.Fail("No started transfusion to complete for this issue. Start it at the bedside, or document it in one step.");
        }

        var stopUtc = request.StopUtc ?? _clock.UtcNow;
        var times = TransfusionDocumentationRule.Evaluate(transfusion.StartUtc, stopUtc, request.VolumeTransfused, unit.Volume, _clock.UtcNow);
        if (times.IsHardStopped)
        {
            return EvaluationResult<TransfusionEvent>.Blocked(times);
        }

        var now = _clock.UtcNow;
        transfusion.StopUtc = stopUtc;
        transfusion.VolumeTransfused = request.VolumeTransfused;
        transfusion.FinalDisposition = disposition;
        transfusion.ReactionSuspected = request.ReactionSuspected.Value;
        if (request.PostVitals?.ToJson() is { } postVitals)
        {
            transfusion.VitalsJson = postVitals;
        }

        _transfusions.Update(transfusion);

        var terminal = disposition == TransfusionDisposition.Stopped
            ? UnitStatus.TransfusionStopped
            : UnitStatus.Transfused;
        issue.Status = IssueStatus.Transfused;
        AppendStatus(unit, terminal, $"Transfusion {disposition}", now, nameof(TransfusionEvent), issue.Id);
        _audit.Record(
            AuditEventType.Transfusion,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.TransfusionStarted },
            newValue: new { Status = terminal, FinalDisposition = disposition, request.ReactionSuspected, StopUtc = stopUtc, request.VolumeTransfused },
            reason: "Transfusion completed.");
        await _unitOfWork.SaveChangesAsync(ct);

        if (request.ReactionSuspected == true)
        {
            await _reactions.OpenForTransfusionAsync(transfusion, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return EvaluationResult<TransfusionEvent>.Ok(transfusion);
    }

    /// <summary>
    /// A transfusion started at the bedside and not completed: a record with no stop time on an issue that is still
    /// Issued. A record that already has a stop time is a finished transfusion, never an open one.
    /// </summary>
    private async Task<TransfusionEvent?> FindStartedTransfusionAsync(Issue issue, CancellationToken ct) =>
        issue.Status != IssueStatus.Issued
            ? null
            : (await _transfusions.ListAsync(t => t.IssueId == issue.Id && t.StopUtc == null, ct)).OrderByDescending(t => t.Id).FirstOrDefault();

    private sealed record BedsideIdentification(
        IdentityTokenType Identifier1Type,
        string? Identifier1Value,
        IdentityTokenType Identifier2Type,
        string? Identifier2Value,
        ComponentScanVerificationRequest? BedsideScan,
        string? SecondVerifier,
        string? BackEntryDin = null);

    private static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Unspecified } v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
        var v => v
    };

    /// <summary>
    /// OCD-054: a downtime back-entered issue may be identified by the DIN on its paper record instead of a scan, by a
    /// role holding issue.downtime-backentry. It is not electronic identification, so a required second verifier stays.
    /// </summary>
    private async Task<RuleResult> EvaluatePaperDinAsync(Issue issue, BloodUnit unit, string? typedDin, CancellationToken ct) =>
        DowntimeBackEntryRule.EvaluateFollowOn(
            issue.DowntimeBackEntry,
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueDowntimeBackEntry, ct),
            typedDin,
            unit.UnitNumber,
            unit.Din);

    /// <summary>
    /// The bedside checks shared by starting and one-step documentation: two patient identifiers match the issued
    /// patient, a fresh unit scan for ISBT units, the unit was in date and issued at the start time, ward receipt when
    /// the facility requires it, and the second verifier (or electronic identification) with a directory check.
    /// </summary>
    private async Task<(EvaluationResult<TransfusionEvent>? Blocked, bool ScanVerified)> EvaluateBedsideAsync(
        Issue issue, BloodUnit unit, DateTime startUtc, BedsideIdentification ids, CancellationToken ct)
    {
        var patient = await _patients.GetByIdAsync(issue.PatientId, ct);
        if (patient is null)
        {
            return (EvaluationResult<TransfusionEvent>.Fail("Patient not found."), false);
        }

        var identity = PatientIdentityMatchRule.Evaluate(
            patient.MedicalRecordNumber,
            patient.DateOfBirth,
            patient.LastName,
            patient.FirstName,
            string.IsNullOrWhiteSpace(ids.Identifier1Value)
                ? null
                : new PatientIdentityMatchRule.IdentityToken(ids.Identifier1Type, ids.Identifier1Value),
            string.IsNullOrWhiteSpace(ids.Identifier2Value)
                ? null
                : new PatientIdentityMatchRule.IdentityToken(ids.Identifier2Type, ids.Identifier2Value));
        if (identity.Severity == RuleSeverity.HardStop)
        {
            return (EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([identity])), false);
        }

        // ISBT-normalized components require a fresh bedside unit scan.
        // Legacy units without ComponentIdentity keep the prior scan policy.
        var bedsideScanVerified = false;
        if (!string.IsNullOrWhiteSpace(ids.BackEntryDin))
        {
            var paper = await EvaluatePaperDinAsync(issue, unit, ids.BackEntryDin, ct);
            if (paper.Severity == RuleSeverity.HardStop)
            {
                return (EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([paper])), false);
            }
        }
        else if (!string.IsNullOrEmpty(unit.ComponentIdentity))
        {
            if (ids.BedsideScan is null)
            {
                return (EvaluationResult<TransfusionEvent>.Fail(
                    $"{IsbtErrorCodes.UnitScanMismatch}: Fresh unit scan is required at bedside."), false);
            }

            var scan = ComponentScanVerifier.Verify(
                unit,
                ids.BedsideScan.Din,
                ids.BedsideScan.ProductCodeData,
                ids.BedsideScan.ExtendedDivisionCode,
                ids.BedsideScan.AboRhdCode,
                ids.BedsideScan.ExpirationEncoded);
            if (scan.IsHardStopped)
            {
                return (EvaluationResult<TransfusionEvent>.Blocked(scan), false);
            }

            bedsideScanVerified = true;
        }

        // Judge the unit at the moment it was started, not when the record is entered (documentation is often later).
        var unitLife = TransfusionDocumentationRule.EvaluateAgainstUnit(startUtc, issue.IssuedUtc, unit.ExpiresUtc);
        if (unitLife.IsHardStopped)
        {
            return (EvaluationResult<TransfusionEvent>.Blocked(unitLife), false);
        }

        var requireReceipt = await _policy.GetRequireWardReceiptAsync(ct);
        var receipt = WardReceiptRule.Evaluate(requireReceipt, issue.WardReceivedUtc is not null);
        if (receipt.Severity == RuleSeverity.HardStop)
        {
            return (EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([receipt])), false);
        }

        var requireSecond = await _policy.GetRequireSecondVerifierAsync(ct);
        var electronicId = identity.Severity == RuleSeverity.Pass && bedsideScanVerified;
        var dual = DualIdentificationRule.Evaluate(_currentUser.UserName, ids.SecondVerifier, electronicId, requireSecond);
        if (dual.Severity == RuleSeverity.HardStop)
        {
            return (EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([dual])), false);
        }

        var directory = await EvaluateSecondVerifierDirectoryAsync(ids.SecondVerifier, ct);
        if (directory.Severity == RuleSeverity.HardStop)
        {
            return (EvaluationResult<TransfusionEvent>.Blocked(new RuleEvaluation([directory])), false);
        }

        return (null, bedsideScanVerified);
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

    /// <summary>
    /// Whether the issue procedures are met, and what each unmet unit typing or modification requires for this unit.
    /// </summary>
    private async Task<(bool ProceduresMet, IReadOnlyList<string> UnmetUnitRequirements)> SpecialRequirementsSatisfiedAsync(
        long patientId,
        BloodUnit unit,
        ProductType? productType,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        DateTime nowUtc,
        IReadOnlyList<string>? acknowledgedCodes,
        PatientBloodTypeHistory? bloodType,
        CancellationToken ct)
    {
        var refs = await SpecialRequirementLoader.ForPatientAsync(_specialRequirements, _requirementDefinitions, patientId, ct);
        if (refs.Count == 0)
        {
            return (true, []);
        }

        var assignments = await _productAttributeAssignments.ListAsync(
            a => a.ProductTypeId == unit.ProductTypeId && a.IsActive, ct);
        var attrIds = assignments.Select(a => a.ProductAttributeId).ToHashSet();
        var attributes = await _productAttributes.ListAsync(a => attrIds.Contains(a.Id) && a.IsActive, ct);
        var codes = attributes.Select(a => a.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var acks = (acknowledgedCodes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var negativeTests = await SpecialRequirementLoader.NegativeTestCodesByUnitAsync(_unitSpecialTests, [unit.Id], ct);
        var typing = SpecialRequirementLoader.Typing(
            unit, productType?.ComponentClass ?? ComponentClass.Other, negativeTests);

        var proceduresMet = SpecialTransfusionRequirementRule.ProceduresMet(
            refs,
            nowUtc,
            acks,
            bloodType?.Abo ?? AboGroup.Unknown,
            bloodType?.AboSubgroup ?? AboSubgroup.Unknown);
        var unmet = SpecialTransfusionRequirementRule.UnmetUnitRequirements(refs, codes, unitAntigens, nowUtc, typing);
        return (proceduresMet, unmet);
    }

    /// <summary>
    /// Queues the dispense or return message to the EHR when a blood product interface is enabled. The issue or
    /// return already stands; a message that cannot be queued is a warning to resend from the HL7 page.
    /// </summary>
    private async Task<RuleResult?> TellEhrOfDispenseAsync(long issueId, ProductDispenseEvent dispenseEvent, string unitNumber, CancellationToken ct)
    {
        if (_dispensePublisher is null)
        {
            return null;
        }

        try
        {
            await _dispensePublisher.PublishDispenseAsync(issueId, dispenseEvent, ct);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RuleResult.Warning(
                "HL7-BPS-NOT-SENT",
                $"Unit {unitNumber} was {(dispenseEvent == ProductDispenseEvent.Dispensed ? "issued" : "returned")}, but the message telling the EHR could not be queued. Check the HL7 page.");
        }
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

    /// <summary>
    /// OCD-074 part two: the current ABO/Rh was tested on an infant's admission specimen that is still valid. The
    /// specimen says the neonatal rules applied when it was collected (<see cref="Specimen.NeonatalStandardExpiresUtc"/>).
    /// </summary>
    private async Task<bool> NeonatalAdmissionTypeStandsAsync(IReadOnlyCollection<PatientBloodTypeHistory> typeRows, CancellationToken ct)
    {
        var current = typeRows.FirstOrDefault(h => h.IsCurrent && h.InvalidatedUtc == null && h.SpecimenId != null);
        if (current?.SpecimenId is not long specimenId)
        {
            return false;
        }

        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        return NeonatalTestingRule.AdmissionTypeStands(
            specimen?.NeonatalStandardExpiresUtc is not null, specimen?.ExpiresUtc, _clock.UtcNow);
    }

    /// <summary>OCD-074 part three, beyond the admission type: a current negative screen, no antibody history, group O red cells.</summary>
    private async Task<bool> NeonatalNoCrossmatchAsync(long patientId, ComponentClass? componentClass, AboGroup unitAbo, CancellationToken ct)
    {
        var cellular = componentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood;
        if (!cellular || unitAbo != AboGroup.O)
        {
            return false;
        }

        var screenNegative = !await _antibodyScreenCompat.HasPositiveAntibodyScreenAsync(patientId, ct)
            && await _antibodyScreenCompat.HasCurrentVerifiedNegativeScreenAsync(patientId, _clock.UtcNow, ct);
        return NeonatalTestingRule.NoCrossmatchApplies(true, screenNegative,
            await _antibodyScreenCompat.HasAntibodyHistoryAsync(patientId, ct), cellular, unitAbo, emergency: false);
    }

    private async Task<bool> IsElectronicCrossmatchEligibleAsync(long patientId, CancellationToken ct)
    {
        var history = await _bloodTypes.ListAsync(h => h.PatientId == patientId, ct);
        var currentConfirmed = history.Any(h => h.IsCurrent && h.BloodType.IsKnown);
        var secondAbo = SecondAboDeterminationRule.HasSecondConcordant(
            SecondAboDeterminationRule.FromHistory(history));
        var hasAntibodyHistory = await _antibodyScreenCompat.HasAntibodyHistoryAsync(patientId, ct);
        var screenNegative = !await _antibodyScreenCompat.HasPositiveAntibodyScreenAsync(patientId, ct)
            && await _antibodyScreenCompat.HasCurrentVerifiedNegativeScreenAsync(patientId, _clock.UtcNow, ct);
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
        return IssueGate.HasUnresolvedAboRhDiscrepancy(IssueGate.DiscrepancyInput(history));
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

    /// <summary>
    /// Cycle 691: the return of a unit recalled while it was issued. The issue is closed as returned and the unit goes from
    /// Recalled straight to quarantine (coded Lookback recall, naming the recall), recorded and audited; no inspection can
    /// send it back to stock.
    /// </summary>
    private async Task<EvaluationResult<Return>> ReturnRecalledAsync(Issue issue, BloodUnit unit, string reason, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var why = reason.Trim();
        var returnRecord = new Return
        {
            IssueId = issue.Id,
            Issue = issue,
            BloodProductId = unit.Id,
            ReturnedUtc = now,
            ReturnedBy = _currentUser.UserName,
            Reason = why,
            ReissueEligible = false,
            ReissueEvaluationJson = JsonSerializer.Serialize(new { Recalled = true, unit.RecallReason, Destination = UnitStatus.Quarantine })
        };
        await _returns.AddAsync(returnRecord, ct);
        issue.Status = IssueStatus.Returned;

        var note = string.IsNullOrWhiteSpace(unit.RecallReason)
            ? $"Returned after recall: {why}"
            : $"Returned after recall ({unit.RecallReason.Trim()}): {why}";
        unit.QuarantineReasonCode = UnitQuarantineReason.LookbackRecall;
        unit.QuarantineReason = note;
        AppendStatus(unit, UnitStatus.Quarantine, note, now, nameof(Return), issue.Id);

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Recalled, IssueStatus = IssueStatus.Issued },
            newValue: new
            {
                unit.UnitNumber,
                Status = UnitStatus.Quarantine,
                Path = "ReturnRecalled",
                ReasonCode = UnitQuarantineReason.LookbackRecall,
                IssueId = issue.Id,
                unit.RecallReason
            },
            reason: note);
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Return>.Ok(returnRecord);
    }

    /// <summary>
    /// Cycle 693: the return of a unit quarantined while its issue was open. The issue is closed as returned and the unit
    /// stays in quarantine with the reason it already has; a status row records the return, and it is audited.
    /// </summary>
    private async Task<EvaluationResult<Return>> ReturnQuarantinedAsync(Issue issue, BloodUnit unit, string reason, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var why = reason.Trim();
        var returnRecord = new Return
        {
            IssueId = issue.Id,
            Issue = issue,
            BloodProductId = unit.Id,
            ReturnedUtc = now,
            ReturnedBy = _currentUser.UserName,
            Reason = why,
            ReissueEligible = false,
            ReissueEvaluationJson = JsonSerializer.Serialize(new { AlreadyQuarantined = true, unit.QuarantineReasonCode, Destination = UnitStatus.Quarantine })
        };
        await _returns.AddAsync(returnRecord, ct);
        issue.Status = IssueStatus.Returned;

        var held = unit.QuarantineReason ?? unit.QuarantineReasonCode.ToString();
        var note = $"Returned while in quarantine ({held}): {why}";
        AppendStatus(unit, UnitStatus.Quarantine, note, now, nameof(Return), issue.Id);
        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Quarantine, IssueStatus = IssueStatus.Issued },
            newValue: new { unit.UnitNumber, Status = UnitStatus.Quarantine, Path = "ReturnQuarantined", unit.QuarantineReasonCode, IssueId = issue.Id },
            reason: note);
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Return>.Ok(returnRecord);
    }

    /// <summary>
    /// Cycle 694: the return of a unit reported damaged or missing while its issue was open. The issue is closed as
    /// returned. A damaged unit stays Damaged for its damage inspection (Discrepancy board); a missing unit is back, so it
    /// is quarantined as located after missing, pending inspection, as Locate does. Recorded and audited.
    /// </summary>
    private async Task<EvaluationResult<Return>> ReturnReportedAsync(Issue issue, BloodUnit unit, string reason, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var why = reason.Trim();
        var reported = unit.Status;
        var returnRecord = new Return
        {
            IssueId = issue.Id,
            Issue = issue,
            BloodProductId = unit.Id,
            ReturnedUtc = now,
            ReturnedBy = _currentUser.UserName,
            Reason = why,
            ReissueEligible = false,
            ReissueEvaluationJson = JsonSerializer.Serialize(new { ReportedWhileIssued = reported })
        };
        await _returns.AddAsync(returnRecord, ct);
        issue.Status = IssueStatus.Returned;

        string note;
        if (reported == UnitStatus.Missing)
        {
            var prior = unit.MissingReason;
            unit.MissingReason = null;
            unit.QuarantineReasonCode = UnitQuarantineReason.LocatedAfterMissing;
            note = string.IsNullOrWhiteSpace(prior)
                ? $"Located after missing (returned): {why}; pending inspection"
                : $"Located after missing ({prior}) (returned): {why}; pending inspection";
            unit.QuarantineReason = note;
            AppendStatus(unit, UnitStatus.Quarantine, note, now, nameof(Return), issue.Id);
        }
        else
        {
            note = $"Returned while reported damaged ({unit.DamagedReason ?? "no reason recorded"}): {why}";
            AppendStatus(unit, UnitStatus.Damaged, note, now, nameof(Return), issue.Id);
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = reported, IssueStatus = IssueStatus.Issued },
            newValue: new { unit.UnitNumber, unit.Status, Path = "ReturnReported", IssueId = issue.Id },
            reason: note);
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<Return>.Ok(returnRecord);
    }

    /// <summary>Cycle 691: the units of these issues recalled while they are out, with the recall's reason.</summary>
    private async Task<Dictionary<long, string?>> RecalledOutAsync(IReadOnlyCollection<Issue> issues, CancellationToken ct)
    {
        if (issues.Count == 0)
        {
            return new Dictionary<long, string?>();
        }

        var unitIds = issues.Select(i => i.BloodProductId).ToHashSet();
        return (await _inventory.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Recalled), ct))
            .Where(u => unitIds.Contains(u.Id))
            .ToDictionary(u => u.Id, u => u.RecallReason);
    }

    private void AppendStatus(BloodUnit unit, UnitStatus toStatus, string reason, DateTime whenUtc, string relatedType, long relatedId)
    {
        var fromStatus = unit.Status;
        // Cycle 694: the issuing paths write their own status steps; one the status rules do not allow is a defect, so it
        // stops the action rather than leave the unit in a status it should not reach (Cycles 691-693 each found one).
        if (!InventoryStatusTransition.IsAllowed(fromStatus, toStatus))
        {
            throw new InvalidOperationException(
                $"Status change from {fromStatus} to {toStatus} is not allowed for {unit.UnitNumber} ({relatedType} {relatedId}).");
        }

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

    private async Task<bool> StorageTimeOutGovernedAsync(long? locationId, CancellationToken ct)
    {
        if (locationId is not long id)
        {
            return true;
        }

        var location = await _locations.GetByIdAsync(id, ct);
        return location?.TimeOutGoverned ?? true;
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

        public long? StorageLocationId(Issue issue) =>
            _units.TryGetValue(issue.BloodProductId, out var unit) ? unit.CurrentLocationId : null;

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

            // One wording with the chart header: an antibody known only from another facility's report says so.
            return string.Join(", ", history.Select(a =>
                OutsideAntibodyReportRule.HistoryEntry(a.AntibodySpecificity, a.IsActive, a.ReportingFacility, a.IdentifiedOn, a.ConfirmedUtc is not null)));
        }
    }
}
