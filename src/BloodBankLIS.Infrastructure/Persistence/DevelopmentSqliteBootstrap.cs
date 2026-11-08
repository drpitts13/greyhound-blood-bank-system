using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Keeps the local SQLite development database aligned with the EF model.
/// Older dev databases were created with <see cref="RelationalDatabaseFacadeExtensions.EnsureCreatedAsync"/>,
/// which does not apply schema updates when the model changes.
/// </summary>
public static class DevelopmentSqliteBootstrap
{
    /// <summary>
    /// Nullable columns added after EnsureCreated. Applied with ALTER TABLE when missing
    /// so local demo data is preserved when possible. SQL is fixed (not user input).
    /// </summary>
    private static readonly (string Table, string Column, string AlterSql)[] AdditiveColumns =
    [
        ("Issues", "Comment", """ALTER TABLE "Issues" ADD COLUMN "Comment" TEXT NULL"""),
        ("Issues", "MtpActivationId", """ALTER TABLE "Issues" ADD COLUMN "MtpActivationId" INTEGER NULL"""),
        ("MtpActivations", "PackDefinitionId", """ALTER TABLE "MtpActivations" ADD COLUMN "PackDefinitionId" INTEGER NULL"""),
        ("Issues", "MtpPackRecordId", """ALTER TABLE "Issues" ADD COLUMN "MtpPackRecordId" INTEGER NULL"""),
        ("Issues", "LateEntryReason", """ALTER TABLE "Issues" ADD COLUMN "LateEntryReason" TEXT NULL"""),
        ("Issues", "DowntimeBackEntry", """ALTER TABLE "Issues" ADD COLUMN "DowntimeBackEntry" INTEGER NOT NULL DEFAULT 0"""),
        ("InterfaceErrorQueue", "ResolutionNote", """ALTER TABLE "InterfaceErrorQueue" ADD COLUMN "ResolutionNote" TEXT NULL"""),
        ("InstrumentMessages", "ResolvedUtc", """ALTER TABLE "InstrumentMessages" ADD COLUMN "ResolvedUtc" TEXT NULL"""),
        ("InstrumentMessages", "ResolvedBy", """ALTER TABLE "InstrumentMessages" ADD COLUMN "ResolvedBy" TEXT NULL"""),
        ("InstrumentMessages", "ResolutionNote", """ALTER TABLE "InstrumentMessages" ADD COLUMN "ResolutionNote" TEXT NULL"""),
        ("Overrides", "ReviewOutcome", """ALTER TABLE "Overrides" ADD COLUMN "ReviewOutcome" INTEGER NULL"""),
        ("Overrides", "AuthorizerMatch", """ALTER TABLE "Overrides" ADD COLUMN "AuthorizerMatch" INTEGER NULL"""),
        ("Overrides", "AuthorizerReference", """ALTER TABLE "Overrides" ADD COLUMN "AuthorizerReference" TEXT NULL"""),
        ("Overrides", "ReviewedBy", """ALTER TABLE "Overrides" ADD COLUMN "ReviewedBy" TEXT NULL"""),
        ("Overrides", "ReviewedUtc", """ALTER TABLE "Overrides" ADD COLUMN "ReviewedUtc" TEXT NULL"""),
        ("Overrides", "ReviewNote", """ALTER TABLE "Overrides" ADD COLUMN "ReviewNote" TEXT NULL"""),
        ("PatientBloodTypeHistory", "InvalidatedUtc", """ALTER TABLE "PatientBloodTypeHistory" ADD COLUMN "InvalidatedUtc" TEXT NULL"""),
        ("PatientBloodTypeHistory", "SpecimenId", """ALTER TABLE "PatientBloodTypeHistory" ADD COLUMN "SpecimenId" INTEGER NULL"""),
        ("PatientBloodTypeHistory", "ManualDocumentation", """ALTER TABLE "PatientBloodTypeHistory" ADD COLUMN "ManualDocumentation" INTEGER NULL"""),
        ("PatientBloodTypeHistory", "ManualDocumentationReference", """ALTER TABLE "PatientBloodTypeHistory" ADD COLUMN "ManualDocumentationReference" TEXT NULL"""),
        ("Specimens", "RejectionReasonCode", """ALTER TABLE "Specimens" ADD COLUMN "RejectionReasonCode" TEXT NULL"""),
        ("Specimens", "RejectedUtc", """ALTER TABLE "Specimens" ADD COLUMN "RejectedUtc" TEXT NULL"""),
        ("Specimens", "RejectedBy", """ALTER TABLE "Specimens" ADD COLUMN "RejectedBy" TEXT NULL"""),
        ("ShiftHandoffs", "ActiveMtp", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "ActiveMtp" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "TransfusionsRunning", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "TransfusionsRunning" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "TransfusionsCompletionOverdue", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "TransfusionsCompletionOverdue" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "PendingTestsVerifyBeforeExpiry", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "PendingTestsVerifyBeforeExpiry" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "ResultsAwaitingVerification", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "ResultsAwaitingVerification" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "ReservationsLapsed", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "ReservationsLapsed" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "NearExpiryUnits", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "NearExpiryUnits" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "NearExpiryReserved", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "NearExpiryReserved" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "CoolersInTransit", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "CoolersInTransit" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "CoolersNotChecked", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "CoolersNotChecked" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "CoolersWithDifferences", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "CoolersWithDifferences" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "ExpiredOnShelf", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "ExpiredOnShelf" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "OpenDeviations", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "OpenDeviations" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "CriticalDeviations", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "CriticalDeviations" INTEGER NOT NULL DEFAULT 0"""),
        ("TestDefinitions", "ResultUnits", """ALTER TABLE "TestDefinitions" ADD COLUMN "ResultUnits" TEXT NULL"""),
        ("TestDefinitions", "DecimalPlaces", """ALTER TABLE "TestDefinitions" ADD COLUMN "DecimalPlaces" INTEGER NULL"""),
        ("TestDefinitions", "ReportableLow", """ALTER TABLE "TestDefinitions" ADD COLUMN "ReportableLow" TEXT NULL"""),
        ("TestDefinitions", "ReportableHigh", """ALTER TABLE "TestDefinitions" ADD COLUMN "ReportableHigh" TEXT NULL"""),
        ("TestDefinitions", "ReferenceLow", """ALTER TABLE "TestDefinitions" ADD COLUMN "ReferenceLow" TEXT NULL"""),
        ("TestDefinitions", "ReferenceHigh", """ALTER TABLE "TestDefinitions" ADD COLUMN "ReferenceHigh" TEXT NULL"""),
        ("TestDefinitions", "CriticalLow", """ALTER TABLE "TestDefinitions" ADD COLUMN "CriticalLow" TEXT NULL"""),
        ("TestDefinitions", "CriticalHigh", """ALTER TABLE "TestDefinitions" ADD COLUMN "CriticalHigh" TEXT NULL"""),
        ("TestDefinitions", "TiterStartDilution", """ALTER TABLE "TestDefinitions" ADD COLUMN "TiterStartDilution" INTEGER NULL"""),
        ("TestDefinitions", "TiterEndDilution", """ALTER TABLE "TestDefinitions" ADD COLUMN "TiterEndDilution" INTEGER NULL"""),
        ("TestDefinitions", "TiterPhaseCode", """ALTER TABLE "TestDefinitions" ADD COLUMN "TiterPhaseCode" TEXT NULL"""),
        ("TestDefinitions", "TiterEndpointMinGrade", """ALTER TABLE "TestDefinitions" ADD COLUMN "TiterEndpointMinGrade" TEXT NULL"""),
        ("TestDefinitions", "TiterScored", """ALTER TABLE "TestDefinitions" ADD COLUMN "TiterScored" INTEGER NOT NULL DEFAULT 0"""),
        ("Patients", "Race", """ALTER TABLE "Patients" ADD COLUMN "Race" TEXT NULL"""),
        ("Patients", "Unidentified", """ALTER TABLE "Patients" ADD COLUMN "Unidentified" INTEGER NOT NULL DEFAULT 0"""),
        ("Patients", "Ethnicity", """ALTER TABLE "Patients" ADD COLUMN "Ethnicity" TEXT NULL"""),
        ("OrderLines", "GrouperCode", """ALTER TABLE "OrderLines" ADD COLUMN "GrouperCode" TEXT NULL"""),
        ("InterfaceValueTranslations", "EndpointId", """ALTER TABLE "InterfaceValueTranslations" ADD COLUMN "EndpointId" INTEGER NULL"""),
        ("Reagents", "TestCodes", """ALTER TABLE "Reagents" ADD COLUMN "TestCodes" TEXT NULL"""),
        ("TestResultReagentLots", "ReviewedUtc", """ALTER TABLE "TestResultReagentLots" ADD COLUMN "ReviewedUtc" TEXT NULL"""),
        ("TestResultReagentLots", "ReviewedBy", """ALTER TABLE "TestResultReagentLots" ADD COLUMN "ReviewedBy" TEXT NULL"""),
        ("TestResultReagentLots", "ReviewNote", """ALTER TABLE "TestResultReagentLots" ADD COLUMN "ReviewNote" TEXT NULL"""),
        ("InstrumentValueTranslations", "Description", """ALTER TABLE "InstrumentValueTranslations" ADD COLUMN "Description" TEXT NULL"""),
        ("InterfaceUnmappedValues", "InstrumentInterfaceId", """ALTER TABLE "InterfaceUnmappedValues" ADD COLUMN "InstrumentInterfaceId" INTEGER NULL"""),
        ("TestDefinitions", "LoincCode", """ALTER TABLE "TestDefinitions" ADD COLUMN "LoincCode" TEXT NULL"""),
        ("TestResults", "AbnormalFlag", """ALTER TABLE "TestResults" ADD COLUMN "AbnormalFlag" TEXT NULL"""),
        ("ShiftHandoffs", "ProductsBelowMinimum", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "ProductsBelowMinimum" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "InstrumentFailuresOpen", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "InstrumentFailuresOpen" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "InstrumentResultsHeld", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "InstrumentResultsHeld" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "ReagentLotsFailedQc", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "ReagentLotsFailedQc" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "ReagentResultsToReview", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "ReagentResultsToReview" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "Hl7ErrorsOpen", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "Hl7ErrorsOpen" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "UnmappedInterfaceValues", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "UnmappedInterfaceValues" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "RetrospectiveIncompatible", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "RetrospectiveIncompatible" INTEGER NOT NULL DEFAULT 0"""),
        ("ReactionInvestigations", "ReopenedUtc", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "ReopenedUtc" TEXT NULL"""),
        ("ReactionInvestigations", "ReopenedBy", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "ReopenedBy" TEXT NULL"""),
        ("ReactionInvestigations", "ReopenReason", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "ReopenReason" TEXT NULL"""),
        ("ReactionInvestigations", "ReportedAfterTransfusion", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "ReportedAfterTransfusion" INTEGER NOT NULL DEFAULT 0"""),
        ("ReactionInvestigations", "OnsetUtc", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "OnsetUtc" TEXT NULL"""),
        ("ReactionInvestigations", "ConcludedReactionCode", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "ConcludedReactionCode" TEXT NULL"""),
        ("ReactionInvestigations", "ConcludedReactionText", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "ConcludedReactionText" TEXT NULL"""),
        ("ReactionInvestigations", "TreatmentCodes", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "TreatmentCodes" TEXT NULL"""),
        ("ReactionInvestigations", "TreatmentTexts", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "TreatmentTexts" TEXT NULL"""),
        ("ReactionInvestigations", "PreTransfusionSpecimenId", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "PreTransfusionSpecimenId" INTEGER NULL"""),
        ("ReactionInvestigations", "PreTransfusionOrderId", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "PreTransfusionOrderId" INTEGER NULL"""),
        ("ReactionInvestigations", "PostTransfusionSpecimenId", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "PostTransfusionSpecimenId" INTEGER NULL"""),
        ("ReactionInvestigations", "PostTransfusionOrderId", """ALTER TABLE "ReactionInvestigations" ADD COLUMN "PostTransfusionOrderId" INTEGER NULL"""),
        ("DatElutionWorkups", "VerifiedBy", """ALTER TABLE "DatElutionWorkups" ADD COLUMN "VerifiedBy" TEXT NULL"""),
        ("DatElutionWorkups", "VerifiedUtc", """ALTER TABLE "DatElutionWorkups" ADD COLUMN "VerifiedUtc" TEXT NULL"""),
        ("Issues", "VerifiedScanJson", """ALTER TABLE "Issues" ADD COLUMN "VerifiedScanJson" TEXT NULL"""),
        ("Issues", "CrossmatchStatus", """ALTER TABLE "Issues" ADD COLUMN "CrossmatchStatus" INTEGER NOT NULL DEFAULT 2"""),
        ("Issues", "EmergencyReleaseDetails", """ALTER TABLE "Issues" ADD COLUMN "EmergencyReleaseDetails" TEXT NULL"""),
        ("Issues", "ReceivedBy", """ALTER TABLE "Issues" ADD COLUMN "ReceivedBy" TEXT NULL"""),
        ("Issues", "UnitExpirationAtIssueUtc", """ALTER TABLE "Issues" ADD COLUMN "UnitExpirationAtIssueUtc" TEXT NULL"""),
        ("Allocations", "AssignmentType", """ALTER TABLE "Allocations" ADD COLUMN "AssignmentType" INTEGER NOT NULL DEFAULT 0"""),
        ("Allocations", "IssueRequestedUtc", """ALTER TABLE "Allocations" ADD COLUMN "IssueRequestedUtc" TEXT NULL"""),
        ("Allocations", "IssueRequestedBy", """ALTER TABLE "Allocations" ADD COLUMN "IssueRequestedBy" TEXT NULL"""),
        ("Allocations", "IssueRequestedTo", """ALTER TABLE "Allocations" ADD COLUMN "IssueRequestedTo" TEXT NULL"""),
        ("BloodProducts", "ComponentIdentity", """ALTER TABLE "BloodProducts" ADD COLUMN "ComponentIdentity" TEXT NULL"""),
        ("BloodProducts", "ComponentIdentityKey", """ALTER TABLE "BloodProducts" ADD COLUMN "ComponentIdentityKey" TEXT NULL"""),
        ("BloodProducts", "Din", """ALTER TABLE "BloodProducts" ADD COLUMN "Din" TEXT NULL"""),
        ("BloodProducts", "Fin", """ALTER TABLE "BloodProducts" ADD COLUMN "Fin" TEXT NULL"""),
        ("BloodProducts", "NominalYear", """ALTER TABLE "BloodProducts" ADD COLUMN "NominalYear" TEXT NULL"""),
        ("BloodProducts", "DonationSequence", """ALTER TABLE "BloodProducts" ADD COLUMN "DonationSequence" TEXT NULL"""),
        ("BloodProducts", "DinFlags", """ALTER TABLE "BloodProducts" ADD COLUMN "DinFlags" TEXT NULL"""),
        ("BloodProducts", "DinKeyboardCheck", """ALTER TABLE "BloodProducts" ADD COLUMN "DinKeyboardCheck" TEXT NULL"""),
        ("BloodProducts", "AboRhdCode", """ALTER TABLE "BloodProducts" ADD COLUMN "AboRhdCode" TEXT NULL"""),
        ("BloodProducts", "DonationCollectionCategory", """ALTER TABLE "BloodProducts" ADD COLUMN "DonationCollectionCategory" TEXT NULL"""),
        ("BloodProducts", "EncodedPhenotype", """ALTER TABLE "BloodProducts" ADD COLUMN "EncodedPhenotype" TEXT NULL"""),
        ("BloodProducts", "AboSpecialMessage", """ALTER TABLE "BloodProducts" ADD COLUMN "AboSpecialMessage" TEXT NULL"""),
        ("BloodProducts", "ProductCodeData", """ALTER TABLE "BloodProducts" ADD COLUMN "ProductCodeData" TEXT NULL"""),
        ("BloodProducts", "ProductDescriptionCode", """ALTER TABLE "BloodProducts" ADD COLUMN "ProductDescriptionCode" TEXT NULL"""),
        ("BloodProducts", "CollectionTypeCode", """ALTER TABLE "BloodProducts" ADD COLUMN "CollectionTypeCode" TEXT NULL"""),
        ("BloodProducts", "DivisionCode", """ALTER TABLE "BloodProducts" ADD COLUMN "DivisionCode" TEXT NULL"""),
        ("BloodProducts", "ExtendedDivisionCode", """ALTER TABLE "BloodProducts" ADD COLUMN "ExtendedDivisionCode" TEXT NULL"""),
        ("BloodProducts", "ExpirationEncoded", """ALTER TABLE "BloodProducts" ADD COLUMN "ExpirationEncoded" TEXT NULL"""),
        ("BloodProducts", "ExpirationLocal", """ALTER TABLE "BloodProducts" ADD COLUMN "ExpirationLocal" TEXT NULL"""),
        ("BloodProducts", "ExpirationTimezone", """ALTER TABLE "BloodProducts" ADD COLUMN "ExpirationTimezone" TEXT NULL"""),
        ("BloodProducts", "ExpirationHasExplicitTime", """ALTER TABLE "BloodProducts" ADD COLUMN "ExpirationHasExplicitTime" INTEGER NOT NULL DEFAULT 0"""),
        ("BloodProducts", "CollectionDateTime", """ALTER TABLE "BloodProducts" ADD COLUMN "CollectionDateTime" TEXT NULL"""),
        ("BloodProducts", "ProcessingFacilityCode", """ALTER TABLE "BloodProducts" ADD COLUMN "ProcessingFacilityCode" TEXT NULL"""),
        ("BloodProducts", "StandardVersion", """ALTER TABLE "BloodProducts" ADD COLUMN "StandardVersion" TEXT NOT NULL DEFAULT 'PLACEHOLDER-REQUIRES-ICCBBA'"""),
        ("BloodProducts", "Source", """ALTER TABLE "BloodProducts" ADD COLUMN "Source" INTEGER NOT NULL DEFAULT 1"""),
        ("BloodProducts", "ShipmentId", """ALTER TABLE "BloodProducts" ADD COLUMN "ShipmentId" TEXT NULL"""),
        ("BloodProducts", "RecallReason", """ALTER TABLE "BloodProducts" ADD COLUMN "RecallReason" TEXT NULL"""),
        ("BloodProducts", "ReceiveVisualAcceptable", """ALTER TABLE "BloodProducts" ADD COLUMN "ReceiveVisualAcceptable" INTEGER NOT NULL DEFAULT 1"""),
        ("BloodProducts", "ReceiveVisualNotes", """ALTER TABLE "BloodProducts" ADD COLUMN "ReceiveVisualNotes" TEXT NULL"""),
        ("BloodProducts", "ReceiveAppearance", """ALTER TABLE "BloodProducts" ADD COLUMN "ReceiveAppearance" INTEGER NOT NULL DEFAULT 0"""),
        ("BloodProducts", "ReceiveTemperatureCelsius", """ALTER TABLE "BloodProducts" ADD COLUMN "ReceiveTemperatureCelsius" TEXT NULL"""),
        ("BloodProducts", "MissingReason", """ALTER TABLE "BloodProducts" ADD COLUMN "MissingReason" TEXT NULL"""),
        ("BloodProducts", "DamagedReason", """ALTER TABLE "BloodProducts" ADD COLUMN "DamagedReason" TEXT NULL"""),
        ("BloodProducts", "SupplierReturnReason", """ALTER TABLE "BloodProducts" ADD COLUMN "SupplierReturnReason" TEXT NULL"""),
        ("BloodProducts", "ShippedToFacility", """ALTER TABLE "BloodProducts" ADD COLUMN "ShippedToFacility" TEXT NULL"""),
        ("BloodProducts", "ShippedUtc", """ALTER TABLE "BloodProducts" ADD COLUMN "ShippedUtc" TEXT NULL"""),
        ("BloodProducts", "OutboundShipmentReference", """ALTER TABLE "BloodProducts" ADD COLUMN "OutboundShipmentReference" TEXT NULL"""),
        ("BloodProducts", "DonationRestriction", """ALTER TABLE "BloodProducts" ADD COLUMN "DonationRestriction" INTEGER NOT NULL DEFAULT 0"""),
        ("BloodProducts", "ReservedPatientId", """ALTER TABLE "BloodProducts" ADD COLUMN "ReservedPatientId" INTEGER NULL"""),
        ("BloodProducts", "DirectedConversionReason", """ALTER TABLE "BloodProducts" ADD COLUMN "DirectedConversionReason" TEXT NULL"""),
        ("BloodProducts", "DirectedConvertedUtc", """ALTER TABLE "BloodProducts" ADD COLUMN "DirectedConvertedUtc" TEXT NULL"""),
        ("BloodProducts", "DirectedConvertedBy", """ALTER TABLE "BloodProducts" ADD COLUMN "DirectedConvertedBy" TEXT NULL"""),
        ("BloodProducts", "ExpectedArrivalDueUtc", """ALTER TABLE "BloodProducts" ADD COLUMN "ExpectedArrivalDueUtc" TEXT NULL"""),
        ("BloodProducts", "QuarantineReasonCode", """ALTER TABLE "BloodProducts" ADD COLUMN "QuarantineReasonCode" INTEGER NOT NULL DEFAULT 0"""),
        ("Crossmatches", "Phase", """ALTER TABLE "Crossmatches" ADD COLUMN "Phase" TEXT NULL"""),
        ("Crossmatches", "Interpretation", """ALTER TABLE "Crossmatches" ADD COLUMN "Interpretation" TEXT NULL"""),
        ("Crossmatches", "ObservedResultsJson", """ALTER TABLE "Crossmatches" ADD COLUMN "ObservedResultsJson" TEXT NULL"""),
        ("Crossmatches", "ClinicalStatus", """ALTER TABLE "Crossmatches" ADD COLUMN "ClinicalStatus" INTEGER NOT NULL DEFAULT 2"""),
        ("Crossmatches", "RulesVersion", """ALTER TABLE "Crossmatches" ADD COLUMN "RulesVersion" TEXT NULL"""),
        ("Crossmatches", "PolicyVersion", """ALTER TABLE "Crossmatches" ADD COLUMN "PolicyVersion" TEXT NULL"""),
        ("Crossmatches", "EncounterId", """ALTER TABLE "Crossmatches" ADD COLUMN "EncounterId" INTEGER NULL"""),
        ("Crossmatches", "OrderId", """ALTER TABLE "Crossmatches" ADD COLUMN "OrderId" INTEGER NULL"""),
        ("TransfusionEvents", "SecondVerifier", """ALTER TABLE "TransfusionEvents" ADD COLUMN "SecondVerifier" TEXT NULL"""),
        ("TransfusionEvents", "Location", """ALTER TABLE "TransfusionEvents" ADD COLUMN "Location" TEXT NULL"""),
        ("TransfusionEvents", "PreTransfusionVitalsJson", """ALTER TABLE "TransfusionEvents" ADD COLUMN "PreTransfusionVitalsJson" TEXT NULL"""),
        ("TransfusionEvents", "PostTransfusionObservations", """ALTER TABLE "TransfusionEvents" ADD COLUMN "PostTransfusionObservations" TEXT NULL"""),
        ("TransfusionEvents", "PatientIdentificationMethod", """ALTER TABLE "TransfusionEvents" ADD COLUMN "PatientIdentificationMethod" TEXT NULL"""),
        ("TransfusionEvents", "UnitIdentificationMethod", """ALTER TABLE "TransfusionEvents" ADD COLUMN "UnitIdentificationMethod" TEXT NULL"""),
        ("TransfusionEvents", "DeviceId", """ALTER TABLE "TransfusionEvents" ADD COLUMN "DeviceId" TEXT NULL"""),
        ("TransfusionEvents", "WorkstationId", """ALTER TABLE "TransfusionEvents" ADD COLUMN "WorkstationId" TEXT NULL"""),
        ("TransfusionEvents", "BedsideScanVerificationJson", """ALTER TABLE "TransfusionEvents" ADD COLUMN "BedsideScanVerificationJson" TEXT NULL"""),
        ("TransfusionEvents", "RemainderDisposition", """ALTER TABLE "TransfusionEvents" ADD COLUMN "RemainderDisposition" TEXT NULL"""),
        ("TransfusionEvents", "ReactionActions", """ALTER TABLE "TransfusionEvents" ADD COLUMN "ReactionActions" TEXT NULL"""),
        ("TransfusionEvents", "OverrideDataJson", """ALTER TABLE "TransfusionEvents" ADD COLUMN "OverrideDataJson" TEXT NULL"""),
        ("Patients", "RecentPregnancyUtc", """ALTER TABLE "Patients" ADD COLUMN "RecentPregnancyUtc" TEXT NULL"""),
        ("Specimens", "Identifier1Type", """ALTER TABLE "Specimens" ADD COLUMN "Identifier1Type" INTEGER NULL"""),
        ("Specimens", "Identifier1Value", """ALTER TABLE "Specimens" ADD COLUMN "Identifier1Value" TEXT NULL"""),
        ("Specimens", "Identifier2Type", """ALTER TABLE "Specimens" ADD COLUMN "Identifier2Type" INTEGER NULL"""),
        ("Specimens", "Identifier2Value", """ALTER TABLE "Specimens" ADD COLUMN "Identifier2Value" TEXT NULL"""),
        ("Issues", "TestsIncompleteAtIssue", """ALTER TABLE "Issues" ADD COLUMN "TestsIncompleteAtIssue" INTEGER NOT NULL DEFAULT 0"""),
        ("Issues", "VisualInspectionAcceptable", """ALTER TABLE "Issues" ADD COLUMN "VisualInspectionAcceptable" INTEGER NOT NULL DEFAULT 1"""),
        ("Issues", "WardReceivedUtc", """ALTER TABLE "Issues" ADD COLUMN "WardReceivedUtc" TEXT NULL"""),
        ("Issues", "WardReceivedBy", """ALTER TABLE "Issues" ADD COLUMN "WardReceivedBy" TEXT NULL"""),
        ("Issues", "WardVisualAcceptable", """ALTER TABLE "Issues" ADD COLUMN "WardVisualAcceptable" INTEGER NOT NULL DEFAULT 1"""),
        ("Issues", "RetrospectiveCrossmatchDueUtc", """ALTER TABLE "Issues" ADD COLUMN "RetrospectiveCrossmatchDueUtc" TEXT NULL"""),
        ("Issues", "RetrospectiveCrossmatchCompletedUtc", """ALTER TABLE "Issues" ADD COLUMN "RetrospectiveCrossmatchCompletedUtc" TEXT NULL"""),
        ("Issues", "RetrospectiveCrossmatchId", """ALTER TABLE "Issues" ADD COLUMN "RetrospectiveCrossmatchId" INTEGER NULL"""),
        ("Issues", "SecondVerifier", """ALTER TABLE "Issues" ADD COLUMN "SecondVerifier" TEXT NULL"""),
        ("Issues", "PatientIdentifier1", """ALTER TABLE "Issues" ADD COLUMN "PatientIdentifier1" TEXT NULL"""),
        ("Issues", "PatientIdentifier2", """ALTER TABLE "Issues" ADD COLUMN "PatientIdentifier2" TEXT NULL"""),
        ("Issues", "CoolerId", """ALTER TABLE "Issues" ADD COLUMN "CoolerId" TEXT NULL"""),
        ("Issues", "InTransitDueUtc", """ALTER TABLE "Issues" ADD COLUMN "InTransitDueUtc" TEXT NULL"""),
        ("Issues", "IssueAppearance", """ALTER TABLE "Issues" ADD COLUMN "IssueAppearance" INTEGER NOT NULL DEFAULT 0"""),
        ("Issues", "WardAppearance", """ALTER TABLE "Issues" ADD COLUMN "WardAppearance" INTEGER NOT NULL DEFAULT 0"""),
        ("Issues", "WardScanJson", """ALTER TABLE "Issues" ADD COLUMN "WardScanJson" TEXT NULL"""),
        ("Users", "FailedSignInCount", """ALTER TABLE "Users" ADD COLUMN "FailedSignInCount" INTEGER NOT NULL DEFAULT 0"""),
        ("Users", "PinHash", """ALTER TABLE "Users" ADD COLUMN "PinHash" TEXT NULL"""),
        ("Users", "TotpSecretProtected", """ALTER TABLE "Users" ADD COLUMN "TotpSecretProtected" TEXT NULL"""),
        ("Users", "TotpPendingSecretProtected", """ALTER TABLE "Users" ADD COLUMN "TotpPendingSecretProtected" TEXT NULL"""),
        ("Users", "TotpEnrolledUtc", """ALTER TABLE "Users" ADD COLUMN "TotpEnrolledUtc" TEXT NULL"""),
        ("Users", "TotpLastStep", """ALTER TABLE "Users" ADD COLUMN "TotpLastStep" INTEGER NULL"""),
        ("ElectronicSignatures", "AuthenticationMethod", """ALTER TABLE "ElectronicSignatures" ADD COLUMN "AuthenticationMethod" INTEGER NOT NULL DEFAULT 2"""),
        ("ElectronicSignatures", "SignatureHash", """ALTER TABLE "ElectronicSignatures" ADD COLUMN "SignatureHash" TEXT NULL"""),
        ("ElectronicSignatures", "ExpiresUtc", """ALTER TABLE "ElectronicSignatures" ADD COLUMN "ExpiresUtc" TEXT NULL"""),
        ("ElectronicSignatures", "ConsumedUtc", """ALTER TABLE "ElectronicSignatures" ADD COLUMN "ConsumedUtc" TEXT NULL"""),
        ("ModificationRules", "ExpirationModificationCodeId", """ALTER TABLE "ModificationRules" ADD COLUMN "ExpirationModificationCodeId" INTEGER NOT NULL DEFAULT 0"""),
        ("ModificationRules", "ModificationCode", """ALTER TABLE "ModificationRules" ADD COLUMN "ModificationCode" TEXT NOT NULL DEFAULT ''"""),
        ("ProductTypes", "RequiresRetype", """ALTER TABLE "ProductTypes" ADD COLUMN "RequiresRetype" INTEGER NOT NULL DEFAULT 0"""),
        ("ProductTypes", "RhPositiveRetypeTestId", """ALTER TABLE "ProductTypes" ADD COLUMN "RhPositiveRetypeTestId" INTEGER NULL"""),
        ("ProductTypes", "RhNegativeRetypeTestId", """ALTER TABLE "ProductTypes" ADD COLUMN "RhNegativeRetypeTestId" INTEGER NULL"""),
        ("InventoryLocations", "Department", """ALTER TABLE "InventoryLocations" ADD COLUMN "Department" TEXT NULL"""),
        ("InventoryLocations", "AllowsIssue", """ALTER TABLE "InventoryLocations" ADD COLUMN "AllowsIssue" INTEGER NOT NULL DEFAULT 1"""),
        ("InventoryLocations", "AllowsRemoteIssue", """ALTER TABLE "InventoryLocations" ADD COLUMN "AllowsRemoteIssue" INTEGER NOT NULL DEFAULT 0"""),
        ("InventoryLocations", "AllowsElectronicIssue", """ALTER TABLE "InventoryLocations" ADD COLUMN "AllowsElectronicIssue" INTEGER NOT NULL DEFAULT 1"""),
        ("InventoryLocations", "RequiresSecondVerifier", """ALTER TABLE "InventoryLocations" ADD COLUMN "RequiresSecondVerifier" INTEGER NOT NULL DEFAULT 0"""),
        ("InventoryLocations", "IsSatellite", """ALTER TABLE "InventoryLocations" ADD COLUMN "IsSatellite" INTEGER NOT NULL DEFAULT 0"""),
        ("InventoryLocations", "AllowsRbc", """ALTER TABLE "InventoryLocations" ADD COLUMN "AllowsRbc" INTEGER NOT NULL DEFAULT 1"""),
        ("InventoryLocations", "AllowsPlasma", """ALTER TABLE "InventoryLocations" ADD COLUMN "AllowsPlasma" INTEGER NOT NULL DEFAULT 1"""),
        ("InventoryLocations", "AllowsPlatelets", """ALTER TABLE "InventoryLocations" ADD COLUMN "AllowsPlatelets" INTEGER NOT NULL DEFAULT 1"""),
        ("InventoryLocations", "AllowsCryo", """ALTER TABLE "InventoryLocations" ADD COLUMN "AllowsCryo" INTEGER NOT NULL DEFAULT 1"""),
        ("InventoryLocations", "AllowsWholeBlood", """ALTER TABLE "InventoryLocations" ADD COLUMN "AllowsWholeBlood" INTEGER NOT NULL DEFAULT 1"""),
        ("InventoryLocations", "StorageTempMinC", """ALTER TABLE "InventoryLocations" ADD COLUMN "StorageTempMinC" TEXT NULL"""),
        ("InventoryLocations", "StorageTempMaxC", """ALTER TABLE "InventoryLocations" ADD COLUMN "StorageTempMaxC" TEXT NULL"""),
        ("InventoryLocations", "DefaultInTransitHours", """ALTER TABLE "InventoryLocations" ADD COLUMN "DefaultInTransitHours" INTEGER NULL"""),
        ("InventoryLocations", "Notes", """ALTER TABLE "InventoryLocations" ADD COLUMN "Notes" TEXT NULL"""),
        ("OrderLines", "PbmStatus", """ALTER TABLE "OrderLines" ADD COLUMN "PbmStatus" INTEGER NOT NULL DEFAULT 0"""),
        ("OrderLines", "PbmIndicationCode", """ALTER TABLE "OrderLines" ADD COLUMN "PbmIndicationCode" TEXT NULL"""),
        ("OrderLines", "PbmJustification", """ALTER TABLE "OrderLines" ADD COLUMN "PbmJustification" TEXT NULL"""),
        ("OrderLines", "PbmDetail", """ALTER TABLE "OrderLines" ADD COLUMN "PbmDetail" TEXT NULL"""),
        ("BloodProducts", "DiscardReasonCode", """ALTER TABLE "BloodProducts" ADD COLUMN "DiscardReasonCode" TEXT NULL"""),
        ("InventoryLocations", "TimeOutGoverned", """ALTER TABLE "InventoryLocations" ADD COLUMN "TimeOutGoverned" INTEGER NOT NULL DEFAULT 1"""),
        ("UnitModifications", "SupplyDescription", """ALTER TABLE "UnitModifications" ADD COLUMN "SupplyDescription" TEXT NULL"""),
        ("UnitModifications", "SupplyLot", """ALTER TABLE "UnitModifications" ADD COLUMN "SupplyLot" TEXT NULL"""),
        ("Specimens", "StorageLocation", """ALTER TABLE "Specimens" ADD COLUMN "StorageLocation" TEXT NULL"""),
        ("Specimens", "StoredUtc", """ALTER TABLE "Specimens" ADD COLUMN "StoredUtc" TEXT NULL"""),
        ("Specimens", "StoredBy", """ALTER TABLE "Specimens" ADD COLUMN "StoredBy" TEXT NULL"""),
        ("Specimens", "DiscardedUtc", """ALTER TABLE "Specimens" ADD COLUMN "DiscardedUtc" TEXT NULL"""),
        ("Specimens", "DiscardedBy", """ALTER TABLE "Specimens" ADD COLUMN "DiscardedBy" TEXT NULL"""),
        ("Specimens", "DiscardReason", """ALTER TABLE "Specimens" ADD COLUMN "DiscardReason" TEXT NULL"""),
        ("SpecimenStorageEvents", "Discarded", """ALTER TABLE "SpecimenStorageEvents" ADD COLUMN "Discarded" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "RhigOpen", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "RhigOpen" INTEGER NOT NULL DEFAULT 0"""),
        ("ShiftHandoffs", "RhigToGive", """ALTER TABLE "ShiftHandoffs" ADD COLUMN "RhigToGive" INTEGER NOT NULL DEFAULT 0"""),
        ("RhigEvaluations", "GivenLot", """ALTER TABLE "RhigEvaluations" ADD COLUMN "GivenLot" TEXT NULL"""),
        ("RhigEvaluations", "GivenVials", """ALTER TABLE "RhigEvaluations" ADD COLUMN "GivenVials" INTEGER NULL"""),
        ("RhigEvaluations", "GivenUtc", """ALTER TABLE "RhigEvaluations" ADD COLUMN "GivenUtc" TEXT NULL"""),
        ("RhigEvaluations", "GivenBy", """ALTER TABLE "RhigEvaluations" ADD COLUMN "GivenBy" TEXT NULL"""),
        ("RhigEvaluations", "GivenNote", """ALTER TABLE "RhigEvaluations" ADD COLUMN "GivenNote" TEXT NULL"""),
        ("OrderLines", "ReflexFromTestCode", """ALTER TABLE "OrderLines" ADD COLUMN "ReflexFromTestCode" TEXT NULL"""),
        ("OrderLines", "ReflexFromValue", """ALTER TABLE "OrderLines" ADD COLUMN "ReflexFromValue" TEXT NULL"""),
        ("OrderLines", "ReflexFromResultId", """ALTER TABLE "OrderLines" ADD COLUMN "ReflexFromResultId" INTEGER NULL"""),
        ("AutoThawProducts", "ThawedShelfLifeHours", """ALTER TABLE "AutoThawProducts" ADD COLUMN "ThawedShelfLifeHours" INTEGER NOT NULL DEFAULT 24"""),
        ("UnitModifications", "DeviceId", """ALTER TABLE "UnitModifications" ADD COLUMN "DeviceId" TEXT NULL"""),
        ("UnitModifications", "IndicatorConfirmed", """ALTER TABLE "UnitModifications" ADD COLUMN "IndicatorConfirmed" INTEGER NULL"""),
        ("ChargeCodes", "RevenueCode", """ALTER TABLE "ChargeCodes" ADD COLUMN "RevenueCode" TEXT NULL"""),
        ("ChargeCodes", "Modifier", """ALTER TABLE "ChargeCodes" ADD COLUMN "Modifier" TEXT NULL"""),
        ("BillingEvents", "ProcedureCode", """ALTER TABLE "BillingEvents" ADD COLUMN "ProcedureCode" TEXT NULL"""),
        ("BillingEvents", "RevenueCode", """ALTER TABLE "BillingEvents" ADD COLUMN "RevenueCode" TEXT NULL"""),
        ("BillingEvents", "Modifier", """ALTER TABLE "BillingEvents" ADD COLUMN "Modifier" TEXT NULL"""),
        ("BillingEvents", "Description", """ALTER TABLE "BillingEvents" ADD COLUMN "Description" TEXT NULL"""),
        ("BillingEvents", "PerformingLocationCode", """ALTER TABLE "BillingEvents" ADD COLUMN "PerformingLocationCode" TEXT NULL"""),
        ("TestResults", "Source", """ALTER TABLE "TestResults" ADD COLUMN "Source" INTEGER NOT NULL DEFAULT 0"""),
        ("TestResults", "SourceReference", """ALTER TABLE "TestResults" ADD COLUMN "SourceReference" TEXT NULL"""),
        ("TestResults", "InvalidatedBy", """ALTER TABLE "TestResults" ADD COLUMN "InvalidatedBy" TEXT NULL"""),
        ("TestResults", "InvalidatedUtc", """ALTER TABLE "TestResults" ADD COLUMN "InvalidatedUtc" TEXT NULL"""),
        ("TestResults", "InvalidationReason", """ALTER TABLE "TestResults" ADD COLUMN "InvalidationReason" TEXT NULL"""),
        ("SpecimenTypeDefinitions", "ExpirationCode", """ALTER TABLE "SpecimenTypeDefinitions" ADD COLUMN "ExpirationCode" TEXT NOT NULL DEFAULT '7D'"""),
        ("SpecimenTypeDefinitions", "ExpirationMode", """ALTER TABLE "SpecimenTypeDefinitions" ADD COLUMN "ExpirationMode" INTEGER NOT NULL DEFAULT 0"""),
        ("SpecialTransfusionRequirements", "RequirementDefinitionId", """ALTER TABLE "SpecialTransfusionRequirements" ADD COLUMN "RequirementDefinitionId" INTEGER NULL"""),
        ("SpecialTransfusionRequirements", "ReactionInvestigationId", """ALTER TABLE "SpecialTransfusionRequirements" ADD COLUMN "ReactionInvestigationId" INTEGER NULL"""),
        ("SpecialRequirementDefinitions", "Category", """ALTER TABLE "SpecialRequirementDefinitions" ADD COLUMN "Category" INTEGER NOT NULL DEFAULT -1"""),
        ("SpecialRequirementDefinitions", "ComponentClass", """ALTER TABLE "SpecialRequirementDefinitions" ADD COLUMN "ComponentClass" INTEGER NULL"""),
        ("SpecialRequirementDefinitions", "AllowedAboGroups", """ALTER TABLE "SpecialRequirementDefinitions" ADD COLUMN "AllowedAboGroups" TEXT NULL"""),
        ("SpecialRequirementDefinitions", "AntigenCode", """ALTER TABLE "SpecialRequirementDefinitions" ADD COLUMN "AntigenCode" TEXT NULL"""),
        ("SpecialRequirementDefinitions", "UnitTestCode", """ALTER TABLE "SpecialRequirementDefinitions" ADD COLUMN "UnitTestCode" TEXT NULL"""),
        ("PatientBloodTypeHistory", "AboSubgroup", """ALTER TABLE "PatientBloodTypeHistory" ADD COLUMN "AboSubgroup" INTEGER NOT NULL DEFAULT 0"""),
        ("Issues", "AcknowledgedSpecialRequirementCodes", """ALTER TABLE "Issues" ADD COLUMN "AcknowledgedSpecialRequirementCodes" TEXT NULL"""),
        ("InstrumentInterfaces", "InterpretationMatch", """ALTER TABLE "InstrumentInterfaces" ADD COLUMN "InterpretationMatch" INTEGER NOT NULL DEFAULT 0"""),
        ("InstrumentInterfaces", "InterpretationAnalyteCodes", """ALTER TABLE "InstrumentInterfaces" ADD COLUMN "InterpretationAnalyteCodes" TEXT NULL"""),
        ("UnitBloodAttributes", "TypingSource", """ALTER TABLE "UnitBloodAttributes" ADD COLUMN "TypingSource" INTEGER NOT NULL DEFAULT 1"""),
        ("Orders", "CancellationReasonCode", """ALTER TABLE "Orders" ADD COLUMN "CancellationReasonCode" TEXT NULL"""),
        ("OrderSpecimens", "UnlinkedUtc", """ALTER TABLE "OrderSpecimens" ADD COLUMN "UnlinkedUtc" TEXT NULL"""),
        ("OrderSpecimens", "UnlinkReason", """ALTER TABLE "OrderSpecimens" ADD COLUMN "UnlinkReason" TEXT NULL""")
    ];

    public static async Task InitializeAsync(
        BloodBankDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await EnsureOrderCancellationReasonsTableAsync(context, cancellationToken);
        await EnsureCompetitorGapTablesAsync(context, cancellationToken);
        await EnsureStorageTemperatureTableAsync(context, cancellationToken);
        await EnsureUnitDiscardReasonsTableAsync(context, cancellationToken);
        await EnsureSpecimenRejectionReasonsTableAsync(context, cancellationToken);
        await EnsureReactionCatalogEntriesTableAsync(context, cancellationToken);
        await EnsureReactionWorkupSettingsTableAsync(context, cancellationToken);
        await EnsureAlertsTableAsync(context, cancellationToken);
        await EnsureSupplierOrderTablesAsync(context, cancellationToken);
        await EnsureDatabaseBackupsTableAsync(context, cancellationToken);
        await EnsureTransfusionIndicationCriteriaTableAsync(context, cancellationToken);
        await EnsureShiftHandoffsTableAsync(context, cancellationToken);
        await EnsureDatElutionWorkupsTableAsync(context, cancellationToken);
        await EnsureEluateFindingsTableAsync(context, cancellationToken);
        await EnsureMtpPackDefinitionsTableAsync(context, cancellationToken);
        await EnsureMtpActivationsTableAsync(context, cancellationToken);
        await EnsureTransportContainersTableAsync(context, cancellationToken);
        await EnsureMtpPackRecordsTableAsync(context, cancellationToken);
        await EnsurePatientMergeTablesAsync(context, cancellationToken);
        await EnsureInterfaceUnmappedValuesTableAsync(context, cancellationToken);
        await EnsureInstrumentValueTranslationsTableAsync(context, cancellationToken);
        await EnsurePatientDuplicateDismissalsTableAsync(context, cancellationToken);
        await EnsureRhigEvaluationsTableAsync(context, cancellationToken);
        await EnsureSpecimenStorageEventsTableAsync(context, cancellationToken);
        var missing = await FindMissingTableAsync(context, cancellationToken)
            ?? await FindMissingRequiredColumnAsync(context, cancellationToken);
        var recreate = missing is not null;
        if (recreate)
        {
            logger.LogWarning(
                "SQLite development database is missing {Missing}, so its schema is out of date. " +
                "Recreating from the current EF model. Demo data will be re-seeded on startup.",
                missing);
            await context.Database.EnsureDeletedAsync(cancellationToken);
        }

        var created = await context.Database.EnsureCreatedAsync(cancellationToken);
        if (!created && !recreate)
        {
            await ApplyAdditiveColumnsAsync(context, logger, cancellationToken);
        }

        // A translation limited to one interface may repeat a global row: the unique index includes the interface.
        if (!created && !recreate)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                DROP INDEX IF EXISTS "IX_InterfaceValueTranslations_DataItemKey_InternalValue_ExternalValue_Direction";
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_InterfaceValueTranslations_EndpointId_DataItemKey_InternalValue_ExternalValue_Direction"
                    ON "InterfaceValueTranslations" ("EndpointId", "DataItemKey", "InternalValue", "ExternalValue", "Direction");
                """, cancellationToken);
        }

        // After the additive columns, so SpecimenId exists on upgraded databases (idempotent).
        await context.Database.ExecuteSqlRawAsync(BloodTypeSpecimenBackfill.Sql, cancellationToken);

        logger.LogInformation(
            recreate || created
                ? "SQLite development database created from EF model."
                : "SQLite development database is up to date.");
    }

    /// <summary>
    /// Creates the cancellation-reason catalog when an existing dev database predates it.
    /// Doing this before the missing-table check keeps demo data instead of recreating the file.
    /// </summary>
    private static async Task EnsureOrderCancellationReasonsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "OrderCancellationReasons", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "OrderCancellationReasons" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OrderCancellationReasons" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Text" TEXT NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """,
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX "IX_OrderCancellationReasons_Code" ON "OrderCancellationReasons" ("Code")""",
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_OrderCancellationReasons_IsActive_SortOrder" ON "OrderCancellationReasons" ("IsActive", "SortOrder")""",
            ct);
    }

    /// <summary>
    /// Creates the shift-handoff table when an existing dev database predates it.
    /// </summary>
    private static async Task EnsureShiftHandoffsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "ShiftHandoffs", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "ShiftHandoffs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ShiftHandoffs" PRIMARY KEY AUTOINCREMENT,
                "RecordedUtc" TEXT NOT NULL,
                "FromUser" TEXT NOT NULL,
                "ToUser" TEXT NULL,
                "Notes" TEXT NOT NULL,
                "RetrospectiveDue" INTEGER NOT NULL,
                "RetrospectiveOverdue" INTEGER NOT NULL,
                "OutstandingIssued" INTEGER NOT NULL,
                "InTransit" INTEGER NOT NULL,
                "InTransitOverdue" INTEGER NOT NULL,
                "PendingTests" INTEGER NOT NULL,
                "PendingSpecimens" INTEGER NOT NULL,
                "OpenReactions" INTEGER NOT NULL,
                "FatalityFollowUpDue" INTEGER NOT NULL,
                "AcknowledgedBy" TEXT NULL,
                "AcknowledgedUtc" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """,
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_ShiftHandoffs_RecordedUtc" ON "ShiftHandoffs" ("RecordedUtc")""",
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_ShiftHandoffs_AcknowledgedUtc" ON "ShiftHandoffs" ("AcknowledgedUtc")""",
            ct);
    }

    /// <summary>
    /// Creates the DAT/elution workup table when an existing dev database predates it.
    /// </summary>
    private static async Task EnsureTransportContainersTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "TransportContainers", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "TransportContainers" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TransportContainers" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "ContainerType" INTEGER NOT NULL,
                "Description" TEXT NULL,
                "MinTemperatureC" TEXT NULL,
                "MaxTemperatureC" TEXT NULL,
                "ValidatedHours" INTEGER NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX "IX_TransportContainers_Code" ON "TransportContainers" ("Code")""", ct);
    }

    private static async Task EnsureMtpPackRecordsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "MtpActivations", ct)
            || await TableExistsAsync(context, "MtpPackRecords", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "MtpPackRecords" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MtpPackRecords" PRIMARY KEY AUTOINCREMENT,
                "MtpActivationId" INTEGER NOT NULL,
                "PackNumber" INTEGER NOT NULL,
                "TransportContainerId" INTEGER NOT NULL,
                "ContainerLabel" TEXT NULL,
                "TemperatureC" TEXT NOT NULL,
                "TemperatureOutsideRange" INTEGER NOT NULL,
                "PackedUtc" TEXT NOT NULL,
                "PackedBy" TEXT NOT NULL,
                "Comment" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL,
                CONSTRAINT "FK_MtpPackRecords_MtpActivations_MtpActivationId" FOREIGN KEY ("MtpActivationId") REFERENCES "MtpActivations" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_MtpPackRecords_TransportContainers_TransportContainerId" FOREIGN KEY ("TransportContainerId") REFERENCES "TransportContainers" ("Id") ON DELETE RESTRICT
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX "IX_MtpPackRecords_MtpActivationId_PackNumber" ON "MtpPackRecords" ("MtpActivationId", "PackNumber")""", ct);
    }

    private static async Task EnsureMtpPackDefinitionsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "MtpPackDefinitions", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "MtpPackDefinitions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MtpPackDefinitions" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "RedCellUnits" INTEGER NOT NULL,
                "PlasmaUnits" INTEGER NOT NULL,
                "PlateletUnits" INTEGER NOT NULL,
                "CryoUnits" INTEGER NOT NULL,
                "WholeBloodUnits" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX "IX_MtpPackDefinitions_Code" ON "MtpPackDefinitions" ("Code")""", ct);
    }

    private static async Task EnsureMtpActivationsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "MtpActivations", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "MtpActivations" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MtpActivations" PRIMARY KEY AUTOINCREMENT,
                "PatientId" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "RequestedBy" TEXT NOT NULL,
                "Reason" TEXT NOT NULL,
                "Location" TEXT NULL,
                "ActivatedUtc" TEXT NOT NULL,
                "ActivatedBy" TEXT NOT NULL,
                "ClosedUtc" TEXT NULL,
                "ClosedBy" TEXT NULL,
                "CloseReason" TEXT NULL,
                "PackDefinitionId" INTEGER NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL,
                CONSTRAINT "FK_MtpActivations_Patients_PatientId" FOREIGN KEY ("PatientId") REFERENCES "Patients" ("Id") ON DELETE RESTRICT
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_MtpActivations_PatientId_Status" ON "MtpActivations" ("PatientId", "Status")""", ct);
    }

    private static async Task EnsureEluateFindingsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "DatElutionWorkups", ct)
            || await TableExistsAsync(context, "DatElutionEluateFindings", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "DatElutionEluateFindings" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DatElutionEluateFindings" PRIMARY KEY AUTOINCREMENT,
                "WorkupId" INTEGER NOT NULL,
                "Kind" INTEGER NOT NULL,
                "BloodAttributeDefinitionId" INTEGER NULL,
                "Specificity" TEXT NOT NULL,
                "PostedAntibodyHistoryId" INTEGER NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL,
                CONSTRAINT "FK_DatElutionEluateFindings_DatElutionWorkups_WorkupId" FOREIGN KEY ("WorkupId") REFERENCES "DatElutionWorkups" ("Id") ON DELETE RESTRICT
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_DatElutionEluateFindings_WorkupId" ON "DatElutionEluateFindings" ("WorkupId")""", ct);
    }

    private static async Task EnsureDatElutionWorkupsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "DatElutionWorkups", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "DatElutionWorkups" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DatElutionWorkups" PRIMARY KEY AUTOINCREMENT,
                "PatientId" INTEGER NOT NULL,
                "SpecimenId" INTEGER NULL,
                "Status" INTEGER NOT NULL,
                "DatResult" INTEGER NOT NULL,
                "DatMethod" TEXT NULL,
                "ElutionResult" TEXT NULL,
                "ElutionNotPerformedReason" TEXT NULL,
                "Comment" TEXT NULL,
                "CompletedBy" TEXT NULL,
                "CompletedUtc" TEXT NULL,
                "VoidedBy" TEXT NULL,
                "VoidedUtc" TEXT NULL,
                "VoidReason" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """,
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_DatElutionWorkups_PatientId" ON "DatElutionWorkups" ("PatientId")""",
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_DatElutionWorkups_Status" ON "DatElutionWorkups" ("Status")""",
            ct);
    }

    /// <summary>
    /// Creates the patient-merge ledger when an existing dev database predates it.
    /// </summary>
    private static async Task EnsurePatientMergeTablesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "PatientMerges", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "PatientMerges" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PatientMerges" PRIMARY KEY AUTOINCREMENT,
                "SurvivorPatientId" INTEGER NOT NULL,
                "DuplicatePatientId" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "Reason" TEXT NOT NULL,
                "MergedBy" TEXT NOT NULL,
                "MergedUtc" TEXT NOT NULL,
                "UnmergeReason" TEXT NULL,
                "UnmergedBy" TEXT NULL,
                "UnmergedUtc" TEXT NULL,
                "SurvivorDeceasedBefore" INTEGER NOT NULL,
                "SurvivorDeceasedUtcBefore" TEXT NULL,
                "SurvivorRecentPregnancyUtcBefore" TEXT NULL,
                "SurvivorDeceasedAfter" INTEGER NOT NULL,
                "SurvivorDeceasedUtcAfter" TEXT NULL,
                "SurvivorRecentPregnancyUtcAfter" TEXT NULL,
                "PriorMrnIdentifierId" INTEGER NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """,
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_PatientMerges_DuplicatePatientId" ON "PatientMerges" ("DuplicatePatientId")""",
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_PatientMerges_SurvivorPatientId" ON "PatientMerges" ("SurvivorPatientId")""",
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_PatientMerges_DuplicatePatientId_Status" ON "PatientMerges" ("DuplicatePatientId", "Status")""",
            ct);

        if (await TableExistsAsync(context, "PatientMergeMoves", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "PatientMergeMoves" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PatientMergeMoves" PRIMARY KEY AUTOINCREMENT,
                "PatientMergeId" INTEGER NOT NULL,
                "EntityType" TEXT NOT NULL,
                "EntityId" INTEGER NOT NULL,
                "Action" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL,
                CONSTRAINT "FK_PatientMergeMoves_PatientMerges_PatientMergeId"
                    FOREIGN KEY ("PatientMergeId") REFERENCES "PatientMerges" ("Id") ON DELETE CASCADE
            )
            """,
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_PatientMergeMoves_PatientMergeId" ON "PatientMergeMoves" ("PatientMergeId")""",
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_PatientMergeMoves_EntityType_EntityId" ON "PatientMergeMoves" ("EntityType", "EntityId")""",
            ct);
    }

    private static async Task ApplyAdditiveColumnsAsync(
        BloodBankDbContext context,
        ILogger logger,
        CancellationToken ct)
    {
        foreach (var (table, column, alterSql) in AdditiveColumns)
        {
            if (!await TableExistsAsync(context, table, ct))
            {
                continue;
            }

            if (await ColumnExistsAsync(context, table, column, ct))
            {
                continue;
            }

            logger.LogWarning(
                "SQLite development database is missing {Table}.{Column}. Adding column via ALTER TABLE.",
                table,
                column);

            await context.Database.ExecuteSqlRawAsync(alterSql, ct);
        }

        await BackfillUnitAttributeTypingSourceAsync(context, ct);
        await BackfillModificationCodesAsync(context, ct);
        await BackfillSpecialRequirementCategoriesAsync(context, ct);
        await ReplaceModificationRuleCodeIndexAsync(context, logger, ct);
        await BackfillInventoryLocationPolicyAsync(context, ct);
        await ApplyAdditiveIndexesAsync(context, logger, ct);
    }

    private static readonly (string Name, string Sql)[] AdditiveIndexes =
    [
        ("IX_Allocations_OneReservedPerUnit",
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Allocations_OneReservedPerUnit" ON "Allocations" ("BloodProductId") WHERE "Status" = 0"""),
        ("IX_Issues_OneOpenIssuePerUnit",
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Issues_OneOpenIssuePerUnit" ON "Issues" ("BloodProductId") WHERE "Status" = 0""")
    ];

    private static async Task ReplaceModificationRuleCodeIndexAsync(
        BloodBankDbContext context,
        ILogger logger,
        CancellationToken ct)
    {
        if (!await TableExistsAsync(context, "ModificationRules", ct))
        {
            return;
        }

        const string oldIndex = "IX_ModificationRules_ModificationCode";
        const string newIndex = "IX_ModificationRules_ModificationCode_SourceProductTypeId_TargetProductTypeId";

        if (await IndexExistsAsync(context, oldIndex, ct))
        {
            logger.LogWarning(
                "Replacing unique index {OldIndex} so modification codes can be reused across product paths.",
                oldIndex);
            await context.Database.ExecuteSqlRawAsync($"""DROP INDEX IF EXISTS "{oldIndex}" """, ct);
        }

        if (!await IndexExistsAsync(context, newIndex, ct))
        {
            logger.LogWarning("SQLite development database is missing index {Index}. Creating it.", newIndex);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ModificationRules_ModificationCode_SourceProductTypeId_TargetProductTypeId"
                ON "ModificationRules" ("ModificationCode", "SourceProductTypeId", "TargetProductTypeId")
                """,
                ct);
        }
    }

    private static async Task ApplyAdditiveIndexesAsync(
        BloodBankDbContext context,
        ILogger logger,
        CancellationToken ct)
    {
        foreach (var (name, sql) in AdditiveIndexes)
        {
            if (await IndexExistsAsync(context, name, ct))
            {
                continue;
            }

            logger.LogWarning("SQLite development database is missing index {Index}. Creating it.", name);
            await context.Database.ExecuteSqlRawAsync(sql, ct);
        }
    }

    private static async Task<bool> IndexExistsAsync(
        BloodBankDbContext context,
        string indexName,
        CancellationToken ct)
    {
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """SELECT 1 FROM sqlite_master WHERE type = 'index' AND name = $name LIMIT 1""";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = indexName;
            command.Parameters.Add(parameter);
            var result = await command.ExecuteScalarAsync(ct);
            return result is not null && result is not DBNull;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task BackfillUnitAttributeTypingSourceAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await TableExistsAsync(context, "UnitBloodAttributes", ct)
            || !await ColumnExistsAsync(context, "UnitBloodAttributes", "TypingSource", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            UPDATE UnitBloodAttributes
            SET TypingSource = 2
            WHERE SourceResultId IS NOT NULL AND TypingSource <> 2
            """,
            ct);
    }

    /// <summary>Same mapping as migration 20261112000000 for definitions made before categories.</summary>
    private static async Task BackfillSpecialRequirementCategoriesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await TableExistsAsync(context, "SpecialRequirementDefinitions", ct)
            || !await ColumnExistsAsync(context, "SpecialRequirementDefinitions", "Category", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(SpecialRequirementCategoryBackfill.Sql("Category = -1"), ct);
    }

    private static async Task BackfillModificationCodesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await TableExistsAsync(context, "ModificationRules", ct)
            || !await ColumnExistsAsync(context, "ModificationRules", "ModificationCode", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            UPDATE ModificationRules
            SET ModificationCode = CASE ModificationType
                WHEN 0 THEN 'DIV-'
                WHEN 1 THEN 'POOL-'
                WHEN 2 THEN 'IRR-'
                WHEN 3 THEN 'THAW-'
                WHEN 4 THEN 'VR-'
                WHEN 5 THEN 'LR-'
                WHEN 6 THEN 'WASH-'
                ELSE 'MOD-'
            END || COALESCE((SELECT ProductCode FROM ProductTypes WHERE Id = ModificationRules.SourceProductTypeId), CAST(Id AS TEXT))
            WHERE TRIM(ModificationCode) = ''
            """,
            ct);
    }

    private static async Task BackfillInventoryLocationPolicyAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await TableExistsAsync(context, "InventoryLocations", ct)
            || !await ColumnExistsAsync(context, "InventoryLocations", "AllowsRbc", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            UPDATE InventoryLocations
            SET AllowsPlasma = 0, AllowsPlatelets = 0, AllowsCryo = 0,
                StorageTempMinC = COALESCE(StorageTempMinC, '1'),
                StorageTempMaxC = COALESCE(StorageTempMaxC, '6')
            WHERE Code = 'FRIDGE-1'
            """,
            ct);
        await context.Database.ExecuteSqlRawAsync(
            """
            UPDATE InventoryLocations
            SET AllowsRbc = 0, AllowsWholeBlood = 0, AllowsPlatelets = 0,
                StorageTempMinC = COALESCE(StorageTempMinC, '-30'),
                StorageTempMaxC = COALESCE(StorageTempMaxC, '-18')
            WHERE Code = 'FREEZER-1'
            """,
            ct);
    }

    /// <summary>
    /// Returns the first table the EF model maps that is absent from the database, or null when
    /// the file is absent, brand new, or already complete. The expected set is read from the model
    /// rather than hand-maintained so a newly added entity cannot silently drift out of the dev
    /// database and fail later during seeding.
    /// </summary>
    private static async Task EnsureCompetitorGapTablesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct) || !await TableExistsAsync(context, "Patients", ct))
        {
            return;
        }

        if (!await TableExistsAsync(context, "Reagents", ct))
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE "Reagents" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Reagents" PRIMARY KEY AUTOINCREMENT,
                    "Code" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedUtc" TEXT NOT NULL,
                    "CreatedBy" TEXT NOT NULL,
                    "ModifiedUtc" TEXT NULL,
                    "ModifiedBy" TEXT NULL,
                    "RowVersion" BLOB NULL
                )
                """, ct);
        }

        if (!await TableExistsAsync(context, "ReagentLots", ct))
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE "ReagentLots" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_ReagentLots" PRIMARY KEY AUTOINCREMENT,
                    "ReagentId" INTEGER NOT NULL,
                    "LotNumber" TEXT NOT NULL,
                    "ReceivedUtc" TEXT NOT NULL,
                    "OpenedUtc" TEXT NULL,
                    "ClosedUtc" TEXT NULL,
                    "ExpiresUtc" TEXT NOT NULL,
                    "State" INTEGER NOT NULL,
                    "CreatedUtc" TEXT NOT NULL,
                    "CreatedBy" TEXT NOT NULL,
                    "ModifiedUtc" TEXT NULL,
                    "ModifiedBy" TEXT NULL,
                    "RowVersion" BLOB NULL
                )
                """, ct);
        }

        if (!await TableExistsAsync(context, "ReagentDailyQcs", ct))
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE "ReagentDailyQcs" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_ReagentDailyQcs" PRIMARY KEY AUTOINCREMENT,
                    "ReagentLotId" INTEGER NOT NULL,
                    "QcDateUtc" TEXT NOT NULL,
                    "ExpectedResult" TEXT NOT NULL,
                    "ObservedResult" TEXT NOT NULL,
                    "Passed" INTEGER NOT NULL,
                    "PerformedBy" TEXT NOT NULL,
                    "PerformedUtc" TEXT NOT NULL,
                    "ReviewedBy" TEXT NULL,
                    "ReviewedUtc" TEXT NULL,
                    "CreatedUtc" TEXT NOT NULL,
                    "CreatedBy" TEXT NOT NULL,
                    "ModifiedUtc" TEXT NULL,
                    "ModifiedBy" TEXT NULL,
                    "RowVersion" BLOB NULL
                )
                """, ct);
        }

        if (!await TableExistsAsync(context, "TestResultReagentLots", ct))
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE "TestResultReagentLots" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TestResultReagentLots" PRIMARY KEY AUTOINCREMENT,
                    "TestResultId" INTEGER NOT NULL,
                    "ReagentLotId" INTEGER NOT NULL,
                    "ReagentCode" TEXT NOT NULL,
                    "LotNumber" TEXT NOT NULL,
                    "RecordedUtc" TEXT NOT NULL,
                    "CreatedUtc" TEXT NOT NULL,
                    "CreatedBy" TEXT NOT NULL,
                    "ModifiedUtc" TEXT NULL,
                    "ModifiedBy" TEXT NULL,
                    "RowVersion" BLOB NULL
                );
                CREATE INDEX "IX_TestResultReagentLots_ReagentLotId" ON "TestResultReagentLots" ("ReagentLotId");
                CREATE INDEX "IX_TestResultReagentLots_TestResultId" ON "TestResultReagentLots" ("TestResultId");
                """, ct);
        }

        if (!await TableExistsAsync(context, "StockParLevels", ct))
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE "StockParLevels" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_StockParLevels" PRIMARY KEY AUTOINCREMENT,
                    "ProductTypeId" INTEGER NOT NULL,
                    "Abo" INTEGER NULL,
                    "MinimumAvailable" INTEGER NOT NULL,
                    "CreatedUtc" TEXT NOT NULL,
                    "CreatedBy" TEXT NOT NULL,
                    "ModifiedUtc" TEXT NULL,
                    "ModifiedBy" TEXT NULL,
                    "RowVersion" BLOB NULL
                )
                """, ct);
        }
    }

    private static async Task EnsureTransfusionIndicationCriteriaTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "TransfusionIndicationCriteria", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "TransfusionIndicationCriteria" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TransfusionIndicationCriteria" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "ComponentClass" INTEGER NOT NULL,
                "LabTestCode" TEXT NULL,
                "Comparison" INTEGER NULL,
                "Threshold" TEXT NULL,
                "Unit" TEXT NULL,
                "LookbackHours" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX "IX_TransfusionIndicationCriteria_Code" ON "TransfusionIndicationCriteria" ("Code")""", ct);
    }

    private static async Task EnsureDatabaseBackupsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "DatabaseBackups", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "DatabaseBackups" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DatabaseBackups" PRIMARY KEY AUTOINCREMENT,
                "StartedUtc" TEXT NOT NULL,
                "CompletedUtc" TEXT NULL,
                "Trigger" TEXT NOT NULL,
                "RequestedBy" TEXT NOT NULL,
                "Provider" TEXT NOT NULL,
                "FilePath" TEXT NOT NULL,
                "SizeBytes" INTEGER NULL,
                "Sha256" TEXT NULL,
                "Succeeded" INTEGER NOT NULL,
                "Verified" INTEGER NOT NULL,
                "VerifyDetail" TEXT NULL,
                "AuditEventCount" INTEGER NULL,
                "LastAuditHash" TEXT NULL,
                "Error" TEXT NULL,
                "FileDeletedUtc" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_DatabaseBackups_StartedUtc" ON "DatabaseBackups" ("StartedUtc")""", ct);
    }

    private static async Task EnsureSupplierOrderTablesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "SupplierOrders", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "SupplierOrders" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SupplierOrders" PRIMARY KEY AUTOINCREMENT,
                "OrderNumber" TEXT NOT NULL,
                "BloodSupplierId" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "Notes" TEXT NULL,
                "SubmittedBy" TEXT NULL,
                "SubmittedUtc" TEXT NULL,
                "SubmitSignatureId" INTEGER NULL,
                "BloodDeliveryId" INTEGER NULL,
                "ReceivedUtc" TEXT NULL,
                "CancelledBy" TEXT NULL,
                "CancelReason" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL,
                CONSTRAINT "FK_SupplierOrders_BloodSuppliers_BloodSupplierId"
                    FOREIGN KEY ("BloodSupplierId") REFERENCES "BloodSuppliers" ("Id") ON DELETE RESTRICT
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "SupplierOrderLines" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SupplierOrderLines" PRIMARY KEY AUTOINCREMENT,
                "SupplierOrderId" INTEGER NOT NULL,
                "ProductTypeId" INTEGER NOT NULL,
                "Abo" INTEGER NULL,
                "Quantity" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL,
                CONSTRAINT "FK_SupplierOrderLines_SupplierOrders_SupplierOrderId"
                    FOREIGN KEY ("SupplierOrderId") REFERENCES "SupplierOrders" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SupplierOrderLines_ProductTypes_ProductTypeId"
                    FOREIGN KEY ("ProductTypeId") REFERENCES "ProductTypes" ("Id") ON DELETE RESTRICT
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX "IX_SupplierOrders_OrderNumber" ON "SupplierOrders" ("OrderNumber")""", ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_SupplierOrders_BloodSupplierId" ON "SupplierOrders" ("BloodSupplierId")""", ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_SupplierOrderLines_SupplierOrderId" ON "SupplierOrderLines" ("SupplierOrderId")""", ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_SupplierOrderLines_ProductTypeId" ON "SupplierOrderLines" ("ProductTypeId")""", ct);
    }

    private static async Task EnsureAlertsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "Alerts", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "Alerts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Alerts" PRIMARY KEY AUTOINCREMENT,
                "Source" TEXT NOT NULL,
                "Key" TEXT NOT NULL,
                "Severity" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "Link" TEXT NULL,
                "RaisedUtc" TEXT NOT NULL,
                "AcknowledgedBy" TEXT NULL,
                "AcknowledgedUtc" TEXT NULL,
                "AcknowledgeNote" TEXT NULL,
                "ResolvedUtc" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_Alerts_Source_ResolvedUtc" ON "Alerts" ("Source", "ResolvedUtc")""", ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_Alerts_Key" ON "Alerts" ("Key")""", ct);
    }

    private static async Task EnsureSpecimenRejectionReasonsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "SpecimenRejectionReasons", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "SpecimenRejectionReasons" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SpecimenRejectionReasons" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Text" TEXT NOT NULL,
                "RequiresComment" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX "IX_SpecimenRejectionReasons_Code" ON "SpecimenRejectionReasons" ("Code")""", ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_SpecimenRejectionReasons_IsActive_SortOrder" ON "SpecimenRejectionReasons" ("IsActive", "SortOrder")""", ct);
    }

    private static async Task EnsureReactionCatalogEntriesTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "ReactionCatalogEntries", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "ReactionCatalogEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ReactionCatalogEntries" PRIMARY KEY AUTOINCREMENT,
                "Kind" INTEGER NOT NULL,
                "Code" TEXT NOT NULL,
                "Text" TEXT NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "IsNoneOption" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX "IX_ReactionCatalogEntries_Kind_Code" ON "ReactionCatalogEntries" ("Kind", "Code")""", ct);
    }

    /// <summary>
    /// Creates the per-instrument translation table on an existing dev database and copies the old assay and analyte
    /// map into it, as migration 20261125000000_InstrumentValueTranslations does.
    /// </summary>
    private static async Task EnsureInstrumentValueTranslationsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "InstrumentInterfaces", ct)
            || await TableExistsAsync(context, "InstrumentValueTranslations", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "InstrumentValueTranslations" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InstrumentValueTranslations" PRIMARY KEY AUTOINCREMENT,
                "InterfaceId" INTEGER NOT NULL,
                "DataItemKey" TEXT NOT NULL,
                "InstrumentValue" TEXT NOT NULL,
                "LocalValue" TEXT NOT NULL,
                "Context" TEXT NULL,
                "Direction" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL,
                CONSTRAINT "FK_InstrumentValueTranslations_InstrumentInterfaces_InterfaceId" FOREIGN KEY ("InterfaceId") REFERENCES "InstrumentInterfaces" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX "IX_InstrumentValueTranslations_InterfaceId_DataItemKey_Context_InstrumentValue_LocalValue_Direction"
                ON "InstrumentValueTranslations" ("InterfaceId", "DataItemKey", "Context", "InstrumentValue", "LocalValue", "Direction");
            """, ct);
        if (await TableExistsAsync(context, "InstrumentTestMappings", ct))
        {
            await context.Database.ExecuteSqlRawAsync(Migrations.InstrumentValueTranslations.CopyTestMappingsSql, ct);
        }
    }

    private static async Task EnsureSpecimenStorageEventsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "SpecimenStorageEvents", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "SpecimenStorageEvents" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SpecimenStorageEvents" PRIMARY KEY AUTOINCREMENT,
                "SpecimenId" INTEGER NOT NULL,
                "Location" TEXT NOT NULL,
                "PreviousLocation" TEXT NULL,
                "StoredUtc" TEXT NOT NULL,
                "StoredBy" TEXT NOT NULL,
                "Note" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            );
            CREATE INDEX "IX_SpecimenStorageEvents_SpecimenId" ON "SpecimenStorageEvents" ("SpecimenId");
            """, ct);
    }

    private static async Task EnsureRhigEvaluationsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "RhigEvaluations", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "RhigEvaluations" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RhigEvaluations" PRIMARY KEY AUTOINCREMENT,
                "PatientId" INTEGER NOT NULL,
                "Context" INTEGER NOT NULL,
                "EventUtc" TEXT NOT NULL,
                "InfantRh" INTEGER NOT NULL,
                "FetalBleedScreen" INTEGER NOT NULL,
                "FetalBleedPercent" TEXT NULL,
                "Decision" INTEGER NOT NULL,
                "VialsRecommended" INTEGER NULL,
                "DecisionNote" TEXT NULL,
                "RecordedBy" TEXT NOT NULL,
                "RecordedUtc" TEXT NOT NULL,
                "CompletedBy" TEXT NULL,
                "CompletedUtc" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            );
            CREATE INDEX "IX_RhigEvaluations_PatientId" ON "RhigEvaluations" ("PatientId");
            """, ct);
    }

    private static async Task EnsurePatientDuplicateDismissalsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "PatientDuplicateDismissals", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "PatientDuplicateDismissals" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PatientDuplicateDismissals" PRIMARY KEY AUTOINCREMENT,
                "FirstPatientId" INTEGER NOT NULL,
                "SecondPatientId" INTEGER NOT NULL,
                "ReviewedReasons" TEXT NOT NULL,
                "Reason" TEXT NOT NULL,
                "DismissedBy" TEXT NOT NULL,
                "DismissedUtc" TEXT NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            );
            CREATE UNIQUE INDEX "IX_PatientDuplicateDismissals_FirstPatientId_SecondPatientId" ON "PatientDuplicateDismissals" ("FirstPatientId", "SecondPatientId");
            """, ct);
    }

    private static async Task EnsureInterfaceUnmappedValuesTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "InterfaceUnmappedValues", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "InterfaceUnmappedValues" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InterfaceUnmappedValues" PRIMARY KEY AUTOINCREMENT,
                "EndpointId" INTEGER NULL,
                "DataItemKey" TEXT NOT NULL,
                "Value" TEXT NOT NULL,
                "Direction" INTEGER NOT NULL,
                "FirstSeenUtc" TEXT NOT NULL,
                "LastSeenUtc" TEXT NOT NULL,
                "Count" INTEGER NOT NULL,
                "LastMessageControlId" TEXT NULL,
                "ResolvedUtc" TEXT NULL,
                "ResolvedBy" TEXT NULL,
                "ResolvedNote" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            );
            CREATE INDEX "IX_InterfaceUnmappedValues_EndpointId_DataItemKey_Direction_Value" ON "InterfaceUnmappedValues" ("EndpointId", "DataItemKey", "Direction", "Value");
            CREATE INDEX "IX_InterfaceUnmappedValues_ResolvedUtc" ON "InterfaceUnmappedValues" ("ResolvedUtc");
            """, ct);
    }

    private static async Task EnsureReactionWorkupSettingsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "ReactionWorkupSettings", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "ReactionWorkupSettings" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ReactionWorkupSettings" PRIMARY KEY AUTOINCREMENT,
                "PreTransfusionTestGroupCode" TEXT NULL,
                "PostTransfusionTestGroupCode" TEXT NULL,
                "Version" INTEGER NOT NULL,
                "EffectiveUtc" TEXT NULL,
                "RetiredUtc" TEXT NULL,
                "IsActive" INTEGER NOT NULL,
                "IsDraft" INTEGER NOT NULL,
                "IsPendingApproval" INTEGER NOT NULL,
                "ApprovedBy" TEXT NULL,
                "ApprovedUtc" TEXT NULL,
                "ChangeReason" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """, ct);
    }

    private static async Task EnsureUnitDiscardReasonsTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "UnitDiscardReasons", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "UnitDiscardReasons" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UnitDiscardReasons" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Text" TEXT NOT NULL,
                "RequiresComment" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX "IX_UnitDiscardReasons_Code" ON "UnitDiscardReasons" ("Code")""", ct);
        await context.Database.ExecuteSqlRawAsync(
            """CREATE INDEX "IX_UnitDiscardReasons_IsActive_SortOrder" ON "UnitDiscardReasons" ("IsActive", "SortOrder")""", ct);
    }

    private static async Task EnsureStorageTemperatureTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct)
            || !await TableExistsAsync(context, "Patients", ct)
            || await TableExistsAsync(context, "StorageTemperatureReadings", ct))
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "StorageTemperatureReadings" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_StorageTemperatureReadings" PRIMARY KEY AUTOINCREMENT,
                "InventoryLocationId" INTEGER NOT NULL,
                "ReadingC" TEXT NOT NULL,
                "ReadingUtc" TEXT NOT NULL,
                "RecordedBy" TEXT NOT NULL,
                "RangeMinC" TEXT NULL,
                "RangeMaxC" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "DeviationId" INTEGER NULL,
                "Comment" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "CreatedBy" TEXT NOT NULL,
                "ModifiedUtc" TEXT NULL,
                "ModifiedBy" TEXT NULL,
                "RowVersion" BLOB NULL,
                CONSTRAINT "FK_StorageTemperatureReadings_InventoryLocations_InventoryLocationId"
                    FOREIGN KEY ("InventoryLocationId") REFERENCES "InventoryLocations" ("Id") ON DELETE RESTRICT
            )
            """, ct);
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX "IX_StorageTemperatureReadings_InventoryLocationId_ReadingUtc"
                ON "StorageTemperatureReadings" ("InventoryLocationId", "ReadingUtc")
            """, ct);
    }

    private static async Task<string?> FindMissingTableAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct))
        {
            return null;
        }

        // An empty file is handled by EnsureCreated, not by a recreate.
        if (!await TableExistsAsync(context, "Patients", ct))
        {
            return null;
        }

        var mappedTables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => !string.IsNullOrWhiteSpace(table))
            .Select(table => table!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var table in mappedTables)
        {
            if (!await TableExistsAsync(context, table, ct))
            {
                return table;
            }
        }

        return null;
    }

    /// <summary>
    /// EnsureCreated does not rewrite existing tables. A required column added after the
    /// file was first created (for example TestServiceBillings.ChargeCodeId) must force a
    /// recreate; ALTER TABLE cannot introduce a non-null FK cleanly on populated rows.
    /// </summary>
    private static async Task<string?> FindMissingRequiredColumnAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct) || !await TableExistsAsync(context, "Patients", ct))
        {
            return null;
        }

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(table) || !await TableExistsAsync(context, table, ct))
            {
                continue;
            }

            var existing = await GetColumnNamesAsync(context, table, ct);
            var storeObject = StoreObjectIdentifier.Table(table, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                if (property.IsColumnNullable())
                {
                    continue;
                }

                var column = property.GetColumnName(storeObject);
                if (string.IsNullOrWhiteSpace(column))
                {
                    continue;
                }

                // A required column on the additive list is added with its default by ALTER TABLE below, so it does
                // not justify deleting the database and its demo data.
                if (!existing.Contains(column) && !IsAdditive(table, column))
                {
                    return $"{table}.{column}";
                }
            }
        }

        return null;
    }

    private static bool IsAdditive(string table, string column) =>
        AdditiveColumns.Any(c =>
            string.Equals(c.Table, table, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Column, column, StringComparison.OrdinalIgnoreCase));

    private static async Task<HashSet<string>> GetColumnNamesAsync(
        BloodBankDbContext context,
        string tableName,
        CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                names.Add(reader.GetString(1));
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }

        return names;
    }

    private static async Task<bool> TableExistsAsync(
        BloodBankDbContext context,
        string tableName,
        CancellationToken ct)
    {
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table' AND name = $name
                """;
            var param = command.CreateParameter();
            param.ParameterName = "$name";
            param.Value = tableName;
            command.Parameters.Add(param);

            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result) > 0;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        BloodBankDbContext context,
        string tableName,
        string columnName,
        CancellationToken ct)
    {
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            // PRAGMA table_info does not accept bound parameters for the table name.
            command.CommandText = $"PRAGMA table_info(\"{tableName}\")";

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
