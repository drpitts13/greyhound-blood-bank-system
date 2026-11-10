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

    /// <summary>Minutes after issue when a unit may still return to Available inventory.</summary>
    public const string ReturnToAvailableMinutes = "Issue.ReturnToAvailableMinutes";

    /// <summary>Hours after a bedside start when an uncompleted transfusion is raised as a reminder (0 = off).</summary>
    public const string TransfusionCompletionReminderHours = "Transfusion.CompletionReminderHours";
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

    /// <summary>A second person may verify eligible routine results in a batch (OCD-061). Default off.</summary>
    public const string AllowBatchVerify = "Result.AllowBatchVerify";

    /// <summary>A tube is accepted for testing only when who collected it is recorded (OCD-064). Default off.</summary>
    public const string RequireSpecimenCollector = "Specimen.RequireCollector";

    /// <summary>Retired. Unit retype is the supplier confirmation and no longer requires a second reviewer.</summary>
    public const string BlockRetypeSelfVerify = "Inventory.BlockRetypeSelfVerify";
    public const string RetentionYears = "Record.RetentionYears";

    /// <summary>Days a patient tube is kept after its expiration before it is due for discard (OCD-070). 0 = not set.</summary>
    public const string SpecimenRetentionDays = "Specimen.RetentionDays";

    /// <summary>Minutes before a blood order's needed-by time that it is marked as needed soon (OCD-075). 0 = off.</summary>
    public const string BloodOrderNeededBySoonMinutes = "BloodOrder.NeededBySoonMinutes";
    public const string MtpIdleReminderMinutes = "Mtp.IdleReminderMinutes";
    /// <summary>Hours an unidentified patient chart stays open before the home card asks to identify it (OCD-077). 0 = off.</summary>
    public const string UnidentifiedReminderHours = "Patient.UnidentifiedReminderHours";
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

    /// <summary>When true, product charges fire on transfusion; when false, they fire on issue.</summary>
    public const string ProductChargeOnTransfuse = "Billing.ProductChargeOnTransfuse";

    /// <summary>When true, patient result entry is blocked while reagent QC work is open. Default off.</summary>
    public const string BlockResultsWhenReagentQcOpen = "Result.BlockWhenReagentQcOpen";

    /// <summary>Hours after issue before an open unit is listed on the final-disposition board. Operational only.</summary>
    public const string FinalDispositionDueHours = "Issue.FinalDispositionDueHours";

    /// <summary>When true, a modification requires a supply lot. Default off.</summary>
    public const string RequireModificationSupplyLot = "Modification.RequireSupplyLot";
    public const string RequireIrradiationRecord = "Modification.RequireIrradiationRecord";
    public const string RequirePrivilegedMfa = "Security.RequirePrivilegedMfa";
    public const string MfaEnabled = "Security.MfaEnabled";
    public const string RequireAllUsersMfa = "Security.RequireAllUsersMfa";
    public const string ExcursionQuarantineMinutes = "Inventory.ExcursionQuarantineMinutes";
    public const string PbmEvaluateOrders = "Order.PbmEvaluate";
    public const string PbmRequireJustification = "Order.PbmRequireJustification";

    /// <summary>When true, issue and bedside forms start with the patient's MRN and DOB filled in. Default off (OCD-039).</summary>
    public const string PrefillPatientIdentifiers = "Issue.PrefillPatientIdentifiers";

    /// <summary>When true, a specimen can be received only by scanning the tube, not from a list row. Default off (OCD-056).</summary>
    public const string RequireSpecimenReceiptByScan = "Specimen.RequireReceiptByScan";

    /// <summary>When true, unit inspections (return, appearance at issue and receipt) start as acceptable. Default off (OCD-040).</summary>
    public const string InspectionDefaultAcceptable = "Inspection.DefaultAcceptable";

    /// <summary>OCD-069: mL of fetal whole blood one RhIG vial covers; 0 = not set (no dose is calculated).</summary>
    public const string RhigMlPerVial = "Rhig.MlFetalBloodPerVial";

    /// <summary>OCD-069: the maternal blood volume (mL) the fetal bleed is calculated against; 0 = not set.</summary>
    public const string RhigMaternalBloodVolumeMl = "Rhig.MaternalBloodVolumeMl";

    /// <summary>OCD-069: how the calculated vials are rounded (1 up, 2 nearest plus one, 3 up plus one); 0 = not set.</summary>
    public const string RhigDoseRounding = "Rhig.DoseRounding";

    /// <summary>OCD-074: turns on the neonatal pretransfusion testing rules. Default off.</summary>
    public const string NeonatalRulesEnabled = "Neonatal.RulesEnabled";

    /// <summary>OCD-074: the age (days) under which the neonatal rules apply.</summary>
    public const string NeonatalAgeLimitDays = "Neonatal.AgeLimitDays";
}
