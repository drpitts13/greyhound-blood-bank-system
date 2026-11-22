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

    /// <summary>Send a verified first result to the EHR without anyone asking (OCD-086; end-user testing 2026-11-12).</summary>
    public const string SendFinalResultsAutomatically = "Results.SendFinalAutomatically";

    /// <summary>Minutes after issue when a unit may still return to Available inventory.</summary>
    public const string ReturnToAvailableMinutes = "Issue.ReturnToAvailableMinutes";

    /// <summary>Hours after a bedside start when an uncompleted transfusion is raised as a reminder (0 = off).</summary>
    public const string TransfusionCompletionReminderHours = "Transfusion.CompletionReminderHours";

    /// <summary>Require the bedside to confirm consent is on the chart before a transfusion starts (pass 115). Off by default.</summary>
    public const string RequireTransfusionConsent = "Transfusion.RequireConsentConfirmation";
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
    public const string TemperatureReadingIntervalHours = "Inventory.TemperatureReadingIntervalHours";

    /// <summary>Minutes a STAT or urgent test may wait from order to verification before it is marked (pass 106). 0 = not set.</summary>
    public const string RushTestTurnaroundMinutes = "Testing.RushTurnaroundTargetMinutes";

    /// <summary>The facility's crossmatch-to-transfusion ratio target, in tenths (20 = 2.0); 0 = not set (pass 342).</summary>
    public const string UtilizationCtRatioTargetTenths = "Utilization.CtRatioTargetTenths";

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

    /// <summary>When true (the default), a modification requires a typed reason.</summary>
    public const string RequireModificationReason = "Modification.RequireReason";
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

    /// <summary>Hours after the event within which RhIG is to be given (OCD-087; 0 = not set).</summary>
    public const string RhigAdministrationWindowHours = "Rhig.AdministrationWindowHours";

    /// <summary>Days after a lookback notification is opened within which the recipient's physician is to be notified (OCD-089; 0 = not set).</summary>
    public const string LookbackNotifyWithinDays = "Lookback.NotifyWithinDays";

    /// <summary>OCD-074: turns on the neonatal pretransfusion testing rules. Default off.</summary>
    public const string NeonatalRulesEnabled = "Neonatal.RulesEnabled";

    /// <summary>OCD-074: the age (days) under which the neonatal rules apply.</summary>
    public const string NeonatalAgeLimitDays = "Neonatal.AgeLimitDays";

    // Medical director decisions recorded as facility settings (2026-11-20: different medical directors use this
    // application). Each default is the decision previously built in, so nothing changes until the facility changes it.

    /// <summary>OCD-090: which deviations need their FDA reportability determined before they close (0 none, 1 all, 2 major and critical, 3 critical).</summary>
    public const string DeviationFdaReportabilityToClose = "Deviation.FdaReportabilityToClose";

    /// <summary>OCD-046: an incompatible retrospective crossmatch after emergency release opens a critical deviation.</summary>
    public const string RetrospectiveIncompatibleOpensDeviation = "Issue.RetrospectiveIncompatibleOpensDeviation";

    /// <summary>OCD-050: the retrospective crossmatch may be recorded on the expired pre-transfusion specimen as an exception.</summary>
    public const string RetrospectiveCrossmatchOnExpiredSpecimen = "Compatibility.RetrospectiveOnExpiredSpecimen";

    /// <summary>OCD-053: emergency or massive-transfusion release before a specimen is drawn, with an override.</summary>
    public const string EmergencyReleaseBeforeSpecimen = "Issue.EmergencyReleaseBeforeSpecimen";

    /// <summary>OCD-058: a reaction workup may use an expired pre-transfusion specimen.</summary>
    public const string ReactionWorkupOnExpiredSpecimen = "Reaction.WorkupOnExpiredSpecimen";

    /// <summary>OCD-057: closing a reaction investigation needs the concluded reaction and the treatment for future transfusions.</summary>
    public const string ReactionCloseRequiresConclusion = "Reaction.CloseRequiresConclusion";

    /// <summary>OCD-069 (D variants): 1 weak D and partial D treated as Rh negative for RhIG; 2 partial D only.</summary>
    public const string RhigDVariantHandling = "Rhig.DVariantHandling";

    /// <summary>OCD-078: another unit while a reaction is under investigation (0 no check, 1 warning with override, 2 refused).</summary>
    public const string ReactionUnderInvestigationIssue = "Issue.ReactionUnderInvestigation";

    /// <summary>OCD-081: a back-entry paper time outside every recorded outage (0 allowed, 1 reason required, 2 refused).</summary>
    public const string BackEntryOutsideOutage = "Downtime.BackEntryOutsideOutage";

    /// <summary>OCD-093: a result entered while its specimen was in date may be verified after the specimen expires.</summary>
    public const string VerifyAfterSpecimenExpiry = "Result.VerifyAfterSpecimenExpiry";

    /// <summary>OCD-051: a correction of a verified result may be verified after the specimen expires.</summary>
    public const string CorrectAfterSpecimenExpiry = "Result.CorrectAfterSpecimenExpiry";

    /// <summary>OCD-028: identifying a specificity assistance would exclude needs a written rationale.</summary>
    public const string AntibodyIdRequireExclusionRationale = "AntibodyId.RequireExclusionRationale";

    /// <summary>OCD-019: inbound OBX-11 statuses that may post a pending result (comma-separated letters).</summary>
    public const string InboundResultStatuses = "Interface.InboundResultStatuses";

    /// <summary>OCD-020: antigen codes that show dosage for antibody identification assistance.</summary>
    public const string AntibodyIdDosageSensitiveAntigens = "AntibodyId.DosageSensitiveAntigens";

    /// <summary>OCD-024: words that mark an antigen typing method as a predicted genotype.</summary>
    public const string AntibodyIdGenotypeMethodWords = "AntibodyId.GenotypeMethodWords";

    /// <summary>OCD-061: which of ABORH, ABSC and DAT may be verified in a batch.</summary>
    public const string BatchVerifyTests = "Result.BatchVerifyTests";

    /// <summary>OCD-065: the score each reaction grade adds to a scored titer (grade=score pairs).</summary>
    public const string TiterGradeScores = "Titer.GradeScores";

    /// <summary>OCD-005: the receipt temperature range for shipped units, whole degrees Celsius.</summary>
    public const string ReceiveTemperatureMinC = "Inventory.ReceiveTemperatureMinC";

    public const string ReceiveTemperatureMaxC = "Inventory.ReceiveTemperatureMaxC";

    /// <summary>OCD-025: completing or accepting antibody identification on an expired specimen, with an acknowledgment.</summary>
    public const string AntibodyIdCompleteOnExpiredSpecimen = "AntibodyId.CompleteOnExpiredSpecimen";

    /// <summary>OCD-027: completing or accepting antibody identification after an attached panel lot expired, with an acknowledgment.</summary>
    public const string AntibodyIdCompleteWithExpiredPanelLot = "AntibodyId.CompleteWithExpiredPanelLot";

    /// <summary>OCD-009: an Inactive (not merged) patient record is refused for ordering, specimens, results, crossmatch and issue.</summary>
    public const string BlockInactivePatientClinicalUse = "Patient.BlockInactiveClinicalUse";

    /// <summary>OCD-026: antibody identification on a received, not yet accepted, specimen (with a warning) or refused.</summary>
    public const string AntibodyIdOnReceivedSpecimen = "AntibodyId.OnReceivedSpecimen";

    /// <summary>OCD-083: an EHR transfusion report for a unit no longer issued (1 record and flag for review, 2 hold unrecorded).</summary>
    public const string TransfusionReportedForUnitNotIssued = "Transfusion.ReportedForUnitNotIssued";

    /// <summary>OCD-091: a patient test read on paper, entered on a specimen expired since (0 refused, 1 tested before expiry with a reason, 2 also in a recorded outage).</summary>
    public const string PatientTestOnExpiredSpecimen = "Result.PaperTestOnExpiredSpecimen";

    /// <summary>OCD-091: a crossmatch read on paper, entered on a specimen expired since (same choices).</summary>
    public const string CrossmatchOnExpiredSpecimen = "Result.PaperCrossmatchOnExpiredSpecimen";
}
