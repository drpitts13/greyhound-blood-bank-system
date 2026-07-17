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

    private static readonly UnitStatus[] IssuableStatuses = { UnitStatus.Available, UnitStatus.Allocated };

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

            c.PatientBloodTypeKnown
                ? RuleResult.Pass(PatientAboRhCode)
                : RuleResult.HardStop(PatientAboRhCode, "Patient ABO/Rh is not established."),

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

            CrossmatchValidityRule.Evaluate(c.RequiresCrossmatch, c.HasValidCrossmatch, c.IsEmergencyRelease),

            c.SpecialRequirementsMet
                ? RuleResult.Pass(SpecialReqCode)
                : RuleResult.HardStop(SpecialReqCode, "One or more active special transfusion requirements are not satisfied."),

            EvaluateDiscrepancy(c)
        };

        // ABO/Rh compatibility only contributes meaningfully when both types are known;
        // the unknown cases are already hard-stopped above.
        if (c.PatientBloodTypeKnown && c.UnitAboRh.IsKnown)
        {
            results.AddRange(AboCompatibilityRule.Evaluate(c.PatientAboRh, c.UnitAboRh, c.ComponentClass));
        }

        results.AddRange(BloodAttributeCompatibilityRule.Evaluate(
            c.ComponentClass,
            c.PatientSignificantAntibodies,
            c.PatientAntigens,
            c.UnitSignificantAntibodies,
            c.UnitAntigens));

        return new RuleEvaluation(results);
    }

    private static RuleResult EvaluateDiscrepancy(IssueGateContext c)
    {
        if (!c.UnresolvedAboRhDiscrepancy)
        {
            return RuleResult.Pass(AboRhDiscrepancyCode);
        }

        // An unresolved ABO/Rh discrepancy is a HardStop on a crossmatch-required
        // product; otherwise an overridable Warning (see docs/safety-rules.md 1 & 6).
        return c.RequiresCrossmatch
            ? RuleResult.HardStop(AboRhDiscrepancyCode, "Unresolved ABO/Rh discrepancy on a crossmatch-required product.")
            : RuleResult.Warning(AboRhDiscrepancyCode, "Current ABO/Rh determination disagrees with the historical record.");
    }
}
