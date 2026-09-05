namespace BloodBankLIS.Domain.Rules;

/// <summary>Canonical facility policy keys stored in <c>SystemSettings</c>.</summary>
public static class FacilityPolicyKeys
{
    public const string SpecimenAlloimmunizationHours = "Specimen.ValidityHours.AlloimmunizationRisk";
    public const string SpecimenStandardHours = "Specimen.ValidityHours.Standard";
    public const string SpecimenLookbackDays = "Specimen.AlloimmunizationLookbackDays";
    public const string RequireSecondVerifier = "Transfusion.RequireSecondVerifier";
    public const string RequireWardReceipt = "Transfusion.RequireWardReceipt";
    public const string RetrospectiveCrossmatchDueHours = "Issue.RetrospectiveCrossmatchDueHours";
    public const string InTransitDueHours = "Issue.InTransitDueHours";
    public const string RequireQuarantineReleaseVerifier = "Inventory.RequireQuarantineReleaseVerifier";
    public const string RequireReceiveVisualInspection = "Inventory.RequireReceiveVisualInspection";
    public const string RequireReceiveVerifier = "Inventory.RequireReceiveVerifier";
    public const string RequireReceiveTemperature = "Inventory.RequireReceiveTemperature";
    public const string RequireDiscardVerifier = "Inventory.RequireDiscardVerifier";
    public const string RequireDirectedConversionVerifier = "Inventory.RequireDirectedConversionVerifier";
    public const string ExpectedArrivalDueHours = "Inventory.ExpectedArrivalDueHours";
    public const string NearExpiryWarningHours = "Inventory.NearExpiryWarningHours";
    public const string BlockSelfVerify = "Result.BlockSelfVerify";

    /// <summary>The user who entered a patient ABO/Rh result may not verify it (establishes current type).</summary>
    public const string BlockAboSelfVerify = "Result.BlockAboSelfVerify";

    /// <summary>The user who entered a unit ABO/Rh retype may not verify it.</summary>
    public const string BlockRetypeSelfVerify = "Inventory.BlockRetypeSelfVerify";
    public const string RetentionYears = "Record.RetentionYears";
    public const string SignatureValidityMinutes = "Signature.ValidityMinutes";

    /// <summary>Facility must enable computer XM after AABB 5.16 validation (SoftBank/SafeTrace switch).</summary>
    public const string AllowElectronicCrossmatch = "Compatibility.AllowElectronicCrossmatch";

    /// <summary>Require two concordant ABO/Rh determinations before non-emergency RBC/WB issue.</summary>
    public const string RequireSecondAboForCellularIssue = "Compatibility.RequireSecondAboForCellularIssue";

    /// <summary>Uncrossmatched emergency/MTP RBC and whole blood should be group O.</summary>
    public const string UncrossmatchedCellularMustBeGroupO = "Compatibility.UncrossmatchedCellularMustBeGroupO";

    /// <summary>Uncrossmatched RBC for childbearing-potential recipients should be RhD-negative.</summary>
    public const string UncrossmatchedONegForChildbearing = "Compatibility.UncrossmatchedONegForChildbearing";

    /// <summary>Upper age (inclusive) treated as childbearing potential when sex is not male.</summary>
    public const string ChildbearingAgeYears = "Compatibility.ChildbearingAgeYears";

    /// <summary>When true, heterozygous (single-dose) cells do not rule out dosage-sensitive antibodies.</summary>
    public const string AntibodyIdDosageAware = "AntibodyId.DosageAware";

    /// <summary>Minimum homozygous (double-dose) negative cells required to exclude a dosage-sensitive antibody.</summary>
    public const string AntibodyIdMinHomozygousExclusions = "AntibodyId.MinHomozygousExclusions";

    /// <summary>Minimum antigen-positive negative cells required to exclude when dosage-aware evaluation is off.</summary>
    public const string AntibodyIdMinHeterozygousExclusions = "AntibodyId.MinHeterozygousExclusions";

    /// <summary>When true, supervisor review is required before an identification workup can complete.</summary>
    public const string AntibodyIdRequireSupervisorReview = "AntibodyId.RequireSupervisorReview";

    /// <summary>When true, the interpreting technologist cannot also perform supervisor review.</summary>
    public const string AntibodyIdBlockSelfReview = "AntibodyId.BlockSelfReview";
}
