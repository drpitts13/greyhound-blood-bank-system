namespace BloodBankLIS.Domain.Rules;

public enum FacilityPolicyValueKind
{
    Integer = 0,
    Boolean = 1
}

public sealed record FacilityPolicyDefinition(
    string Key,
    string Category,
    string DisplayName,
    string Description,
    FacilityPolicyValueKind Kind,
    string DefaultValue,
    string Citation,
    int? MinInclusive = null,
    int? MaxInclusive = null);

/// <summary>
/// SoftBank / SafeTrace facility-parameter catalog. Values live in <c>SystemSettings</c>;
/// this table is the regulated dictionary of allowed keys, bounds, and citations.
/// </summary>
public static class FacilityPolicyCatalog
{
    public static IReadOnlyList<FacilityPolicyDefinition> All { get; } =
    [
        new(FacilityPolicyKeys.SpecimenAlloimmunizationHours, "Specimen", "Alloimmunization specimen hours",
            "Validity window when the recipient was transfused or pregnant in the lookback period.",
            FacilityPolicyValueKind.Integer, "72", "AABB 5.14.5 / CAP TRM.30550", 24, 72),
        new(FacilityPolicyKeys.SpecimenStandardHours, "Specimen", "Standard specimen hours",
            "Validity window when alloimmunization risk is not present.",
            FacilityPolicyValueKind.Integer, "168", "AABB 5.14.5", 24, 336),
        new(FacilityPolicyKeys.SpecimenLookbackDays, "Specimen", "Alloimmunization lookback days",
            "Days to search for recent transfusion or pregnancy.",
            FacilityPolicyValueKind.Integer, "90", "AABB 5.14.5", 90, 365),
        new(FacilityPolicyKeys.RequireSecondVerifier, "Transfusion", "Require second verifier at issue",
            "Require a distinct second verifier or validated electronic identification.",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.11 / 5.28"),
        new(FacilityPolicyKeys.RequireWardReceipt, "Transfusion", "Require ward receipt",
            "Receiving location must acknowledge the unit before transfusion documentation.",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.28 / SoftBank remote issue"),
        new(FacilityPolicyKeys.RetrospectiveCrossmatchDueHours, "Issue", "Retrospective XM due hours",
            "Hours after uncrossmatched emergency or MTP issue when retrospective testing is due.",
            FacilityPolicyValueKind.Integer, "24", "21 CFR 606.151(b)", 1, 72),
        new(FacilityPolicyKeys.InTransitDueHours, "Issue", "In-transit receipt due hours",
            "Hours after issue when ward receipt of a cooler is due.",
            FacilityPolicyValueKind.Integer, "4", "AABB 5.1.8 / 21 CFR 606.160", 1, 24),
        new(FacilityPolicyKeys.AllowElectronicCrossmatch, "Issue", "Allow electronic crossmatch",
            "Permit computer XM and electronic issue after two concordant ABO/Rh determinations and a negative antibody screen. Disable until the facility validates AABB 5.16.",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.16 / CAP TRM.40650"),
        new(FacilityPolicyKeys.RequireSecondAboForCellularIssue, "Issue", "Require second ABO/Rh for RBC issue",
            "Require a current ABO/Rh plus a concordant historical or second-sample type before non-emergency red cell or whole-blood issue. Emergency and MTP remain overridable.",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.14 / CAP TRM.40650"),
        new(FacilityPolicyKeys.UncrossmatchedCellularMustBeGroupO, "Issue", "Uncrossmatched RBC must be group O",
            "Warn when emergency or MTP red cells or whole blood are not group O (SafeTrace / SoftBank uncrossmatched default).",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.27 / CAP TRM.40770"),
        new(FacilityPolicyKeys.UncrossmatchedONegForChildbearing, "Issue", "Uncrossmatched O-neg for childbearing potential",
            "Warn when uncrossmatched red cells are RhD-positive for a recipient who is not known Rh-positive and is of childbearing potential.",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.27 / CAP TRM.40770"),
        new(FacilityPolicyKeys.ChildbearingAgeYears, "Issue", "Childbearing potential age (years)",
            "Inclusive upper age used with sex when applying the uncrossmatched RhD-negative preference.",
            FacilityPolicyValueKind.Integer, "50", "AABB 5.27", 12, 60),
        new(FacilityPolicyKeys.RequireQuarantineReleaseVerifier, "Inventory", "Quarantine-release verifier",
            "Require a distinct directory user to release a unit from quality quarantine.",
            FacilityPolicyValueKind.Boolean, "true", "21 CFR 606.100 / AABB 5.1.5"),
        new(FacilityPolicyKeys.RequireReceiveVisualInspection, "Inventory", "Receive visual inspection",
            "Require an acceptable visual inspection before a unit can be received.",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.6.2"),
        new(FacilityPolicyKeys.RequireReceiveVerifier, "Inventory", "Receive second verifier",
            "Require a distinct directory user when receiving a unit.",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.6 / 21 CFR 606.160"),
        new(FacilityPolicyKeys.RequireReceiveTemperature, "Inventory", "Receive temperature",
            "Require a shipping-container temperature before receiving a unit.",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.1.8"),
        new(FacilityPolicyKeys.RequireDiscardVerifier, "Inventory", "Discard second verifier",
            "Require a distinct directory user when discarding a unit.",
            FacilityPolicyValueKind.Boolean, "true", "21 CFR 606.160 / AABB 5.1.5"),
        new(FacilityPolicyKeys.RequireDirectedConversionVerifier, "Inventory", "Directed-conversion verifier",
            "Require a distinct directory user to convert a directed unit to allogeneic inventory.",
            FacilityPolicyValueKind.Boolean, "true", "AABB 5.4 / 21 CFR 606.160"),
        new(FacilityPolicyKeys.ExpectedArrivalDueHours, "Inventory", "Expected arrival due hours",
            "Hours after packing-list expect when inbound arrival is due.",
            FacilityPolicyValueKind.Integer, "24", "AABB 5.6", 1, 168),
        new(FacilityPolicyKeys.NearExpiryWarningHours, "Inventory", "Near-expiry warning hours",
            "Hours before expiration when the near-expiry worklist includes the unit.",
            FacilityPolicyValueKind.Integer, "24", "AABB 5.1.8", 1, 168),
        new(FacilityPolicyKeys.BlockSelfVerify, "Result", "Block self-verify",
            "When true, the entering user cannot verify the same result (CLIA independent review).",
            FacilityPolicyValueKind.Boolean, "false", "CLIA 42 CFR 493.1251 / CAP COM.10000"),
        new(FacilityPolicyKeys.BlockAboSelfVerify, "Result", "Block ABO/Rh self-verify",
            "When true, the user who entered a patient ABO/Rh cannot verify it. Default on. MarkComplete does not auto-verify ABO/Rh.",
            FacilityPolicyValueKind.Boolean, "true", "Facility SOP / OCD-013"),
        new(FacilityPolicyKeys.BlockRetypeSelfVerify, "Inventory", "Block retype self-verify",
            "When true, the user who entered a unit ABO/Rh retype cannot verify it. Default on.",
            FacilityPolicyValueKind.Boolean, "true", "Facility SOP / OCD-011"),
        new(FacilityPolicyKeys.RetentionYears, "Records", "Record retention years",
            "Minimum product record retention. Automated purge is not performed.",
            FacilityPolicyValueKind.Integer, "10", "21 CFR 606.160(d) / CAP TRM.42120", 5, 30),
        new(FacilityPolicyKeys.SignatureValidityMinutes, "Signatures", "Signature validity minutes",
            "Electronic signature reuse window before consumption.",
            FacilityPolicyValueKind.Integer, "15", "21 CFR 11.70", 1, 60),
        new(FacilityPolicyKeys.AntibodyIdDosageAware, "AntibodyId", "Dosage-aware antibody ID",
            "When true, heterozygous (single-dose) cells do not rule out antibodies in the dosage-sensitive set. Assistance only; technologist judgment is still required.",
            FacilityPolicyValueKind.Boolean, "true", "Facility SOP / OCD-020"),
        new(FacilityPolicyKeys.AntibodyIdMinHomozygousExclusions, "AntibodyId", "Minimum homozygous exclusions",
            "Homozygous (double-dose) negative cells required to exclude a dosage-sensitive antibody during assistance.",
            FacilityPolicyValueKind.Integer, "1", "Facility SOP / OCD-020", 1, 5),
        new(FacilityPolicyKeys.AntibodyIdMinHeterozygousExclusions, "AntibodyId", "Minimum heterozygous exclusions",
            "Antigen-positive negative cells required to exclude when dosage-aware evaluation is off, or for antigens that are not dosage-sensitive.",
            FacilityPolicyValueKind.Integer, "2", "Facility SOP / OCD-020", 1, 5),
        new(FacilityPolicyKeys.AntibodyIdRequireSupervisorReview, "AntibodyId", "Require supervisor review of antibody ID",
            "When true, a distinct supervisor must accept the technologist interpretation before the workup can complete and post history.",
            FacilityPolicyValueKind.Boolean, "true", "Facility SOP / OCD-021"),
        new(FacilityPolicyKeys.AntibodyIdBlockSelfReview, "AntibodyId", "Block antibody ID self-review",
            "When true, the technologist who interpreted the workup cannot also perform supervisor review.",
            FacilityPolicyValueKind.Boolean, "true", "Facility SOP / OCD-021")
    ];

    public static FacilityPolicyDefinition? Find(string key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));
}
