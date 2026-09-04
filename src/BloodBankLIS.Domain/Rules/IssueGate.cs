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

    /// <summary>All active special transfusion requirements satisfied (irradiated/CMV-neg/...).</summary>
    public required bool SpecialRequirementsMet { get; init; }

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

    /// <summary>Facility requires two concordant ABO/Rh determinations for RBC/WB issue.</summary>
    public bool RequireSecondAboForCellularIssue { get; init; } = true;

    /// <summary>Current type plus a matching historical or second-sample determination.</summary>
    public bool HasSecondConcordantAboRh { get; init; }

    public Sex PatientSex { get; init; }

    public int? PatientAgeYears { get; init; }

    public bool UncrossmatchedMustBeGroupO { get; init; } = true;

    public bool UncrossmatchedONegForChildbearing { get; init; } = true;

    public int ChildbearingAgeYears { get; init; } = 50;

    /// <summary>Allogeneic, autologous, or directed. Autologous/directed must match <see cref="ReservedPatientId"/>.</summary>
    public DonationRestriction DonationRestriction { get; init; } = DonationRestriction.Allogeneic;

    public long? ReservedPatientId { get; init; }

    public long IssuePatientId { get; init; }

    /// <summary>Issue is tied to a product or test order.</summary>
    public bool OrderLinked { get; init; }

    /// <summary>Linked order is still open for fulfillment (not hold/cancel/complete).</summary>
    public bool OrderIsFulfillable { get; init; } = true;

    public required DateTime NowUtc { get; init; }
    public TimeSpan SpecimenNearExpiryWindow { get; init; } = TimeSpan.FromHours(12);
    public TimeSpan UnitNearExpiryWindow { get; init; } = TimeSpan.FromHours(24);
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

    private static readonly UnitStatus[] IssuableStatuses =
    {
        UnitStatus.Available, UnitStatus.Allocated, UnitStatus.Assigned, UnitStatus.Crossmatched, UnitStatus.Selected
    };

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

            IssuableStatuses.Contains(c.UnitStatus)
                ? RuleResult.Pass(UnitStatusCode)
                : RuleResult.HardStop(UnitStatusCode, $"Unit status {c.UnitStatus} is not issuable."),

            BloodUnitExpirationRule.Evaluate(c.UnitExpiresUtc, c.NowUtc, c.UnitNearExpiryWindow),

            c.AllocatedToThisPatient
                ? RuleResult.Pass(AllocationCode)
                : RuleResult.HardStop(AllocationCode, "Unit is not allocated/reserved to this patient."),

            CrossmatchValidityRule.Evaluate(
                RequiresCrossmatchEffective(c),
                c.HasValidCrossmatch,
                c.IsEmergencyRelease,
                c.ElectronicCrossmatchEligible && (c.IsElectronicIssue || c.IsRemoteIssue)),

            c.SpecialRequirementsMet
                ? RuleResult.Pass(SpecialReqCode)
                : RuleResult.HardStop(SpecialReqCode, "One or more active special transfusion requirements are not satisfied."),

            c.VisualInspectionAcceptable
                ? RuleResult.Pass(VisualInspectionCode)
                : RuleResult.HardStop(VisualInspectionCode, "Unit failed visual inspection and cannot be issued."),

            IssueAppearanceRule.Evaluate(c.Appearance),

            EvaluateDiscrepancy(c),

            AutologousDirectedRule.EvaluateIssue(c.DonationRestriction, c.ReservedPatientId, c.IssuePatientId),

            SecondAboDeterminationRule.EvaluateForCellularIssue(
                c.RequireSecondAboForCellularIssue,
                c.HasSecondConcordantAboRh,
                c.ComponentClass,
                c.IsEmergencyRelease),

            OrderControlRule.EvaluateIssue(c.OrderLinked, c.OrderIsFulfillable)
        };

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
            var abo = AboCompatibilityRule.Evaluate(c.PatientAboRh, c.UnitAboRh, c.ComponentClass);
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

    /// <summary>RBC and whole blood always require a compatible crossmatch result.</summary>
    internal static bool RequiresCrossmatchEffective(IssueGateContext c) =>
        c.RequiresCrossmatch
        || c.ComponentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood;

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

    private static RuleResult EvaluateDiscrepancy(IssueGateContext c)
    {
        if (!c.UnresolvedAboRhDiscrepancy)
        {
            return RuleResult.Pass(AboRhDiscrepancyCode);
        }

        // An unresolved ABO/Rh discrepancy is a HardStop on a crossmatch-required
        // product; otherwise an overridable Warning (see docs/safety-rules.md 1 & 6).
        return RequiresCrossmatchEffective(c)
            ? RuleResult.HardStop(AboRhDiscrepancyCode, "Unresolved ABO/Rh discrepancy on a crossmatch-required product.")
            : RuleResult.Warning(AboRhDiscrepancyCode, "Current ABO/Rh determination disagrees with the historical record.");
    }
}
