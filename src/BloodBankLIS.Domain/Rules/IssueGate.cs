using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// All facts the issue gate needs, assembled by the application layer from the
/// patient, specimen, unit, allocation, crossmatch, and antibody data. Keeping the
/// gate a pure function of this context makes the full safety check exhaustively
/// unit-testable without a database.
/// </summary>
public sealed record IssueGateContext
{
    public required bool IdentityConfirmed { get; init; }

    public required bool SpecimenExists { get; init; }
    public required bool SpecimenBelongsToPatient { get; init; }
    public DateTime? SpecimenExpiresUtc { get; init; }

    public required bool PatientBloodTypeKnown { get; init; }
    public AboRh PatientAboRh { get; init; }

    public required AboRh UnitAboRh { get; init; }
    public required ComponentClass ComponentClass { get; init; }

    public required UnitStatus UnitStatus { get; init; }
    public required DateTime UnitExpiresUtc { get; init; }

    public required bool AllocatedToThisPatient { get; init; }

    public required bool RequiresCrossmatch { get; init; }
    public required bool HasValidCrossmatch { get; init; }

    /// <summary>A completed incompatible crossmatch exists for this patient and unit.</summary>
    public bool HasIncompatibleCrossmatch { get; init; }

    public required bool IsEmergencyRelease { get; init; }

    public required bool ProductTypeMatchesOrder { get; init; }

    /// <summary>Clinically significant active patient antibodies (catalog-linked).</summary>
    public IReadOnlyList<BloodAttributeCompatibilityRule.AntibodyRef> PatientSignificantAntibodies { get; init; } = [];

    /// <summary>Patient antigen phenotype results.</summary>
    public IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> PatientAntigens { get; init; } = [];

    /// <summary>Clinically significant antibodies identified on the unit (plasma/platelet products).</summary>
    public IReadOnlyList<BloodAttributeCompatibilityRule.AntibodyRef> UnitSignificantAntibodies { get; init; } = [];

    /// <summary>Unit antigen phenotype results.</summary>
    public IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> UnitAntigens { get; init; } = [];

    /// <summary>
    /// Clinically active special requirements are satisfied: the issue procedures (acknowledgments, A1/A2 subgroup),
    /// and, for callers that do not give <see cref="UnmetUnitRequirements"/>, the unit facts too.
    /// </summary>
    public required bool SpecialRequirementsMet { get; init; }

    /// <summary>
    /// What each unmet unit typing or modification requires (e.g. "Patient requires Irradiated – RBC").
    /// A hard stop, except on emergency or MTP release, where it needs an authorized override.
    /// </summary>
    public IReadOnlyList<string> UnmetUnitRequirements { get; init; } = [];

    /// <summary>A current vs historical ABO/Rh discrepancy that has not been resolved.</summary>
    public required bool UnresolvedAboRhDiscrepancy { get; init; }

    /// <summary>Operator attested that the unit passed visual inspection at issue.</summary>
    public bool VisualInspectionAcceptable { get; init; } = true;

    /// <summary>Coded SoftBank/SafeTrace appearance catalog at issue.</summary>
    public UnitAppearance Appearance { get; init; } = UnitAppearance.Acceptable;

    /// <summary>Unit has a configured current inventory location.</summary>
    public bool LocationKnown { get; init; }

    public bool LocationActive { get; init; } = true;

    public bool LocationAllowsIssue { get; init; } = true;

    public bool LocationAllowsRemoteIssue { get; init; }

    public bool LocationAllowsElectronicIssue { get; init; } = true;

    public bool LocationRequiresSecondVerifier { get; init; }

    public bool LocationAllowsComponent { get; init; } = true;

    public bool HasSecondVerifier { get; init; }

    public bool IsRemoteIssue { get; init; }

    public bool IsElectronicIssue { get; init; }

    /// <summary>AABB 5.16 computer-crossmatch preconditions hold for this patient.</summary>
    public bool ElectronicCrossmatchEligible { get; init; }

    /// <summary>OCD-074 part three: an infant under the neonatal rules needs no crossmatch for this unit.</summary>
    public bool NeonatalNoCrossmatch { get; init; }

    /// <summary>Facility requires two concordant ABO/Rh determinations for RBC/WB issue.</summary>
    public bool RequireSecondAboForCellularIssue { get; init; } = true;

    /// <summary>Current type plus a matching historical or second-sample determination.</summary>
    public bool HasSecondConcordantAboRh { get; init; }

    public Sex PatientSex { get; init; }

    /// <summary>
    /// Product ABO/Rh table. When null or empty, the built-in component-class rule is used.
    /// </summary>
    public ProductAboRhPolicy? AboRhPolicy { get; init; }

    public int? PatientAgeYears { get; init; }

    public bool UncrossmatchedMustBeGroupO { get; init; } = true;

    public bool UncrossmatchedONegForChildbearing { get; init; } = true;

    public int ChildbearingAgeYears { get; init; } = 50;

    /// <summary>Allogeneic, autologous, or directed. Autologous/directed must match <see cref="ReservedPatientId"/>.</summary>
    public DonationRestriction DonationRestriction { get; init; } = DonationRestriction.Allogeneic;

    public long? ReservedPatientId { get; init; }

    public long IssuePatientId { get; init; }

    /// <summary>Merged records cannot be issued against; Active/Inactive remain usable.</summary>
    public PatientStatus PatientStatus { get; init; } = PatientStatus.Active;

    /// <summary>Issue is tied to a product or test order.</summary>
    public bool OrderLinked { get; init; }

    /// <summary>Linked order is still open for fulfillment (not hold/cancel/complete).</summary>
    public bool OrderIsFulfillable { get; init; } = true;

    public required DateTime NowUtc { get; init; }
    public TimeSpan SpecimenNearExpiryWindow { get; init; } = TimeSpan.FromHours(12);
    public TimeSpan UnitNearExpiryWindow { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// False when the product requires ABO/Rh retype and no verified matching retype is on file.
    /// Products that do not require retype are confirmed.
    /// </summary>
    public bool RetypeConfirmed { get; init; } = true;

    /// <summary>The latest verified product retype disagrees with the label: a known mislabeled unit.</summary>
    public bool UnitRetypeMismatch { get; init; }
}

/// <summary>
/// The issue gate (docs/safety-rules.md section 1, workflows section 5). Runs the
/// full check set before a unit may leave inventory. Any HardStop blocks the issue
/// and cannot be overridden; Warnings are overridable with reason + authorization.
/// </summary>
public static class IssueGate
{
    public const string IdentityCode = "ISS-IDENTITY";
    public const string SpecExistsCode = "ISS-SPEC-EXISTS";
    public const string SpecPatientCode = "ISS-SPEC-PATIENT";
    public const string PatientAboRhCode = "ISS-PT-ABORH";
    public const string UnitAboRhCode = "ISS-UNIT-ABORH";
    public const string ProductTypeCode = "ISS-PRODUCT-TYPE";
    public const string UnitStatusCode = "ISS-UNIT-STATUS";
    public const string AllocationCode = "ISS-ALLOCATION";
    public const string AntigenNegCode = "ISS-ANTIGEN-NEG";
    public const string SpecialReqCode = "ISS-SPECIAL-REQ";
    public const string AboRhDiscrepancyCode = "ISS-ABORH-DISCREPANCY";
    public const string VisualInspectionCode = "ISS-VISUAL";
    public const string UnitRetypeMismatchCode = "ISS-RETYPE-MISMATCH";

    private static readonly UnitStatus[] IssuableStatuses =
    {
        UnitStatus.Available, UnitStatus.Allocated, UnitStatus.Assigned, UnitStatus.Crossmatched, UnitStatus.Selected
    };

    /// <summary>
    /// Unmet issue procedures always stop the issue. An unmet unit typing or modification stops it too, except on
    /// emergency or MTP release, where it becomes a warning that needs the authorized emergency override.
    /// </summary>
    public static IReadOnlyList<RuleResult> EvaluateSpecialRequirements(IssueGateContext c)
    {
        var results = new List<RuleResult>();
        if (!c.SpecialRequirementsMet)
        {
            results.Add(RuleResult.HardStop(SpecialReqCode, "One or more active special transfusion requirements are not satisfied."));
        }

        foreach (var unmet in c.UnmetUnitRequirements)
        {
            results.Add(c.IsEmergencyRelease
                ? RuleResult.Warning(SpecialReqCode, $"{unmet} Emergency or MTP release requires an authorized override.")
                : RuleResult.HardStop(SpecialReqCode, unmet));
        }

        return results.Count == 0 ? [RuleResult.Pass(SpecialReqCode)] : results;
    }

    public static RuleEvaluation Evaluate(IssueGateContext c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var results = new List<RuleResult>
        {
            c.IdentityConfirmed
                ? RuleResult.Pass(IdentityCode)
                : RuleResult.HardStop(IdentityCode, "Patient identity has not been confirmed against the specimen and unit tag."),

            c.SpecimenExists
                ? RuleResult.Pass(SpecExistsCode)
                : RuleResult.HardStop(SpecExistsCode, "No current specimen exists for the patient."),

            c.SpecimenBelongsToPatient
                ? RuleResult.Pass(SpecPatientCode)
                : RuleResult.HardStop(SpecPatientCode, "Specimen does not belong to this patient."),

            SpecimenExpirationRule.Evaluate(c.SpecimenExpiresUtc, c.NowUtc, c.SpecimenNearExpiryWindow),

            EvaluatePatientTypeKnown(c),

            c.UnitAboRh.IsKnown
                ? RuleResult.Pass(UnitAboRhCode)
                : RuleResult.HardStop(UnitAboRhCode, "Unit ABO/Rh is not present."),

            c.ProductTypeMatchesOrder
                ? RuleResult.Pass(ProductTypeCode)
                : RuleResult.HardStop(ProductTypeCode, "Unit product type does not match the order/clinical requirement."),

            UnitStatusAllowsIssue(c)
                ? RuleResult.Pass(UnitStatusCode)
                : RuleResult.HardStop(UnitStatusCode, NotIssuableMessage(c.UnitStatus)),

            BloodUnitExpirationRule.Evaluate(c.UnitExpiresUtc, c.NowUtc, c.UnitNearExpiryWindow),

            c.AllocatedToThisPatient || EmergencyMayReserveUnretyped(c)
                ? RuleResult.Pass(AllocationCode)
                : RuleResult.HardStop(AllocationCode, "Unit is not allocated/reserved to this patient."),

            ProductRetypeAllocationRule.EvaluateForIssue(c.RetypeConfirmed, c.IsEmergencyRelease),

            // Never converted for emergency: "not yet retyped" may be overridden, "retyped and wrong" may not.
            c.UnitRetypeMismatch
                ? RuleResult.HardStop(UnitRetypeMismatchCode,
                    "This unit's verified ABO/Rh retype does not match its label. It cannot be issued, even for emergency release, until the discrepancy is resolved.")
                : RuleResult.Pass(UnitRetypeMismatchCode),

            CrossmatchValidityRule.Evaluate(
                RequiresCrossmatchEffective(c) && !c.NeonatalNoCrossmatch,
                c.HasValidCrossmatch,
                c.IsEmergencyRelease,
                c.ElectronicCrossmatchEligible && (c.IsElectronicIssue || c.IsRemoteIssue),
                c.HasIncompatibleCrossmatch),

            c.VisualInspectionAcceptable
                ? RuleResult.Pass(VisualInspectionCode)
                : RuleResult.HardStop(VisualInspectionCode, "Unit failed visual inspection and cannot be issued."),

            IssueAppearanceRule.Evaluate(c.Appearance),

            EvaluateDiscrepancy(c),

            AutologousDirectedRule.EvaluateIssue(c.DonationRestriction, c.ReservedPatientId, c.IssuePatientId),

            PatientMergeRule.EvaluateClinicalUse(c.PatientStatus),

            SecondAboDeterminationRule.EvaluateForCellularIssue(
                c.RequireSecondAboForCellularIssue,
                c.HasSecondConcordantAboRh,
                c.ComponentClass,
                c.IsEmergencyRelease),

            OrderControlRule.EvaluateIssue(c.OrderLinked, c.OrderIsFulfillable)
        };

        results.AddRange(EvaluateSpecialRequirements(c));

        // OCD-053: an emergency with no usable specimen replaces the specimen checks with the pre-specimen rule.
        // A specimen that belongs to another patient is a wrong-patient signal and stays a hard stop.
        if (c.SpecimenExists == c.SpecimenBelongsToPatient
            && EmergencyPreSpecimenRule.Applies(c.IsEmergencyRelease, c.SpecimenExists, c.SpecimenExpiresUtc, c.NowUtc))
        {
            results.RemoveAll(r => r.Code is SpecExistsCode or SpecPatientCode or SpecimenExpirationRule.ExpiredCode or SpecimenExpirationRule.NearExpiryCode);
            results.Add(EmergencyPreSpecimenRule.Evaluate(c.ComponentClass, c.UnitAboRh));
        }

        results.AddRange(EmergencyUncrossmatchedAboRule.Evaluate(
            c.IsEmergencyRelease,
            c.ComponentClass,
            c.UnitAboRh,
            c.PatientAboRh,
            c.PatientSex,
            c.PatientAgeYears,
            c.UncrossmatchedMustBeGroupO,
            c.UncrossmatchedONegForChildbearing,
            c.ChildbearingAgeYears));

        // Not covered by the emergency warning conversion below: an established ABO conflict stays a hard stop.
        results.Add(EmergencyUncrossmatchedAboRule.EvaluateEstablishedConflict(
            c.IsEmergencyRelease,
            c.ComponentClass,
            c.UnitAboRh,
            c.PatientAboRh,
            c.PatientBloodTypeKnown));

        results.AddRange(InventoryLocationPolicyRule.EvaluateIssue(
            c.LocationKnown,
            c.LocationActive,
            c.LocationAllowsComponent,
            c.LocationAllowsIssue,
            c.LocationAllowsRemoteIssue,
            c.LocationAllowsElectronicIssue,
            c.LocationRequiresSecondVerifier,
            c.HasSecondVerifier,
            c.IsRemoteIssue,
            c.IsElectronicIssue,
            c.IsEmergencyRelease,
            c.ElectronicCrossmatchEligible));

        // Ordered checks: (1) ABORH Ag/Ab, (2) non-ABORH antigen-neg for RBC/WB.
        // Complex XM (3) is enforced at allocation/order time; compatible XM (4) above.
        if (c.PatientBloodTypeKnown && c.UnitAboRh.IsKnown)
        {
            var abo = AboCompatibilityRule.Evaluate(
                c.PatientAboRh, c.UnitAboRh, c.ComponentClass, c.PatientSex, c.AboRhPolicy);
            results.AddRange(c.IsEmergencyRelease
                ? abo.Select(r => r.Severity == RuleSeverity.HardStop
                    ? RuleResult.Warning(r.Code, r.Message)
                    : r)
                : abo);
        }

        results.AddRange(BloodAttributeCompatibilityRule.Evaluate(
            c.ComponentClass,
            c.PatientSignificantAntibodies,
            c.PatientAntigens,
            c.UnitSignificantAntibodies,
            c.UnitAntigens));

        return new RuleEvaluation(results);
    }

    /// <summary>
    /// Emergency or MTP issue may release a unit that is still Received because retype
    /// has not been confirmed. Every other issue stays on the normal issuable statuses.
    /// </summary>
    /// <summary>
    /// Cycle 697: the refusal for a status that cannot be issued says why and what to do next, after the status itself
    /// (the wording "Unit status X is not issuable" is kept at the start).
    /// </summary>
    public static string NotIssuableMessage(UnitStatus status) =>
        $"Unit status {status} is not issuable" + status switch
        {
            UnitStatus.Expected => ": the unit has not been received yet. Receive it first.",
            UnitStatus.Received => ": the unit is received but not released to stock. Release it (and retype it if required) first.",
            UnitStatus.Discarded => ": the unit was discarded. Choose another unit.",
            UnitStatus.Modified => ": the unit was modified into other units. Issue one of those instead.",
            UnitStatus.Expired => ": the unit has expired. Discard it and choose another unit.",
            UnitStatus.Recalled => ": the unit was recalled. Choose another unit.",
            UnitStatus.Quarantine => ": the unit is in quarantine. Choose another unit, or release it from quarantine first.",
            UnitStatus.OnHold => ": the unit is on operational hold. Release the hold first, or choose another unit.",
            UnitStatus.Missing or UnitStatus.Damaged => ": resolve it on the Discrepancy board, or choose another unit.",
            UnitStatus.Issued or UnitStatus.TransfusionStarted or UnitStatus.Transfused or UnitStatus.TransfusionStopped
                => ": the unit is already issued or transfused.",
            UnitStatus.ReturnedToSupplier or UnitStatus.ShippedOut => ": the unit has left this facility.",
            _ => "."
        };

    private static bool UnitStatusAllowsIssue(IssueGateContext c) =>
        IssuableStatuses.Contains(c.UnitStatus)
        || (EmergencyMayReserveUnretyped(c) && c.UnitStatus == UnitStatus.Received);

    /// <summary>
    /// An unretyped unit cannot be reserved in advance. Emergency issue is allowed to
    /// name that unit without a prior allocation.
    /// </summary>
    private static bool EmergencyMayReserveUnretyped(IssueGateContext c) =>
        c.IsEmergencyRelease && !c.RetypeConfirmed;

    /// <summary>
    /// RBC and whole blood always require a compatible crossmatch. Plasma, platelets,
    /// cryoprecipitate, and other non-cellular products never do.
    /// </summary>
    internal static bool RequiresCrossmatchEffective(IssueGateContext c) =>
        CrossmatchApplicability.RequiresCrossmatch(c.ComponentClass, c.RequiresCrossmatch);

    private static RuleResult EvaluatePatientTypeKnown(IssueGateContext c)
    {
        if (c.PatientBloodTypeKnown)
        {
            return RuleResult.Pass(PatientAboRhCode);
        }

        return c.IsEmergencyRelease
            ? RuleResult.Warning(
                PatientAboRhCode,
                "Patient ABO/Rh is not established; uncrossmatched emergency or MTP issue requires an authorized override.")
            : RuleResult.HardStop(PatientAboRhCode, "Patient ABO/Rh is not established.");
    }

    private static RuleResult EvaluateDiscrepancy(IssueGateContext c) =>
        EvaluateAboRhDiscrepancy(c.UnresolvedAboRhDiscrepancy, RequiresCrossmatchEffective(c));

    /// <summary>
    /// Same discrepancy result issue uses. A crossmatch-required product is a hard
    /// stop. Any other product is an overridable warning.
    /// </summary>
    public static RuleResult EvaluateAboRhDiscrepancy(bool unresolved, bool requiresCrossmatch)
    {
        if (!unresolved)
        {
            return RuleResult.Pass(AboRhDiscrepancyCode);
        }

        return requiresCrossmatch
            ? RuleResult.HardStop(AboRhDiscrepancyCode, "Unresolved ABO/Rh discrepancy on a crossmatch-required product.")
            : RuleResult.Warning(AboRhDiscrepancyCode, "Current ABO/Rh determination disagrees with the historical record.");
    }

    public readonly record struct AboRhDiscrepancyCue(bool BlocksIssue, string? Notice);

    /// <summary>
    /// Ready-to-issue cue. Emergency release does not clear a discrepancy hard stop.
    /// </summary>
    public static AboRhDiscrepancyCue DescribeAboRhDiscrepancy(bool unresolved, bool requiresCrossmatch)
    {
        var result = EvaluateAboRhDiscrepancy(unresolved, requiresCrossmatch);
        return result.Severity == RuleSeverity.Pass
            ? new AboRhDiscrepancyCue(false, null)
            : new AboRhDiscrepancyCue(result.Severity == RuleSeverity.HardStop, result.Message);
    }

    /// <summary>
    /// The discrepancy input from stored history: types whose source result was invalidated are left out.
    /// The chart builds the same input from its rows (<c>BloodTypeDto.DiscrepancyInput</c>).
    /// </summary>
    public static IReadOnlyList<(AboRh Type, bool IsCurrent)> DiscrepancyInput(IEnumerable<Entities.PatientBloodTypeHistory> rows) =>
        rows.Where(h => h.InvalidatedUtc is null).Select(h => (h.BloodType, h.IsCurrent)).ToList();

    /// <summary>
    /// True when the first current known type disagrees with any other known,
    /// non-current type. This is the issue-gate predicate.
    /// </summary>
    public static bool HasUnresolvedAboRhDiscrepancy(IEnumerable<(AboRh Type, bool IsCurrent)> history)
    {
        (AboRh Type, bool IsCurrent)? current = null;
        foreach (var row in history)
        {
            if (row.IsCurrent)
            {
                current = row;
                break;
            }
        }

        if (current is null || !current.Value.Type.IsKnown)
        {
            return false;
        }

        var currentType = current.Value.Type;
        return history.Any(row => !row.IsCurrent && row.Type.IsKnown && row.Type != currentType);
    }

    /// <summary>
    /// Patient-banner sentence when the current type disagrees with history.
    /// The product-specific hard stop stays on issue.
    /// </summary>
    public static string? PatientBannerNotice(IEnumerable<(AboRh Type, bool IsCurrent)> history) =>
        HasUnresolvedAboRhDiscrepancy(history)
            ? EvaluateAboRhDiscrepancy(true, requiresCrossmatch: false).Message
            : null;
}
