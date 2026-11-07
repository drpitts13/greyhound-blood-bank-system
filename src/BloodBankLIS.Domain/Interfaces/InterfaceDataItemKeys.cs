namespace BloodBankLIS.Domain.Interfaces;

/// <summary>Stable catalog keys for interface field mapping.</summary>
public static class InterfaceDataItemKeys
{
    public const string PatientMrn = "Patient.MedicalRecordNumber";
    public const string PatientPriorMrn = "Patient.PriorMedicalRecordNumber";
    public const string PatientLastName = "Patient.LastName";
    public const string PatientFirstName = "Patient.FirstName";
    public const string PatientMiddleName = "Patient.MiddleName";
    public const string PatientDateOfBirth = "Patient.DateOfBirth";
    public const string PatientSex = "Patient.Sex";
    public const string PatientRace = "Patient.Race";
    public const string PatientEthnicity = "Patient.Ethnicity";

    public const string EncounterAccountNumber = "Encounter.AccountNumber";
    public const string EncounterVisitNumber = "Encounter.VisitNumber";
    public const string EncounterAdmitUtc = "Encounter.AdmitUtc";
    public const string EncounterDischargeUtc = "Encounter.DischargeUtc";
    public const string EncounterCurrentLocation = "Encounter.CurrentLocation";
    public const string EncounterAttendingProviderId = "Encounter.AttendingProviderId";
    public const string EncounterAttendingProviderName = "Encounter.AttendingProviderName";

    /// <summary>PV1-2 patient class, translated to and from the local visit (encounter) type.</summary>
    public const string EncounterPatientClass = "Encounter.PatientClass";

    public const string OrderControl = "Order.OrderControl";
    public const string OrderNumber = "Order.OrderNumber";
    public const string OrderTestCode = "Order.TestCode";
    public const string OrderProviderId = "Order.OrderingProviderId";
    public const string OrderProviderName = "Order.OrderingProviderName";
    public const string OrderLocationCode = "Order.OrderingLocationCode";
    public const string OrderPriority = "Order.Priority";

    /// <summary>
    /// The EHR procedure code of a test or test grouper, shared by inbound orders (OBR-4) and outbound results (OBR-4).
    /// A row on <see cref="OrderTestCode"/> or <see cref="ResultObrTestCode"/> for the same value takes precedence.
    /// </summary>
    public const string ProcedureCode = "Procedure.Code";

    public const string ResultObrTestCode = "TestResult.ObrTestCode";
    public const string ResultVerifiedUtc = "TestResult.VerifiedUtc";
    public const string ResultObxIdentifier = "TestResult.ObxIdentifier";
    public const string ResultValue = "TestResult.Value";
    public const string ResultUnits = "TestResult.Units";
    public const string ResultInterpretation = "TestResult.Interpretation";

    /// <summary>OBX-8 for a numeric result outside its ranges (L, H, LL, HH); otherwise OBX-8 carries the interpretation.</summary>
    public const string ResultAbnormalFlag = "TestResult.AbnormalFlag";
    public const string ResultObxStatus = "TestResult.Status";
    public const string ResultSpecimenAccession = "Specimen.AccessionNumber";

    public const string BillingServiceDate = "BillingEvent.ServiceDateUtc";
    public const string BillingTransactionType = "BillingEvent.TransactionType";
    public const string BillingCode = "BillingEvent.BillingCode";
    public const string BillingQuantity = "BillingEvent.Quantity";
    public const string BillingDescription = "BillingEvent.Description";
    public const string BillingRevenueCode = "BillingEvent.RevenueCode";
    public const string BillingProcedureCode = "BillingEvent.ProcedureCode";
    public const string BillingModifier = "BillingEvent.Modifier";
    public const string BillingPerformingLocation = "BillingEvent.PerformingLocationCode";

    public const string UnitNumber = "BloodUnit.UnitNumber";
    public const string UnitDin = "BloodUnit.Din";
    public const string TransfusionStartUtc = "TransfusionEvent.StartUtc";
    public const string TransfusionStopUtc = "TransfusionEvent.StopUtc";
    public const string TransfusionVolume = "TransfusionEvent.VolumeTransfused";
    public const string TransfusionLocation = "TransfusionEvent.Location";
    public const string Transfusionist = "TransfusionEvent.Transfusionist";
    public const string TransfusionReaction = "TransfusionEvent.ReactionSuspected";

    // OCD-063: the EHR's request to send a reserved unit now (HL7 v2.5 OMB^O27).
    public const string DispenseOrderControl = "DispenseRequest.OrderControl";
    public const string DispenseOrderNumber = "DispenseRequest.OrderNumber";
    public const string DispenseDestination = "DispenseRequest.Destination";
    public const string DispensePatientLocation = "DispenseRequest.PatientLocation";
    public const string DispenseDin = "DispenseRequest.Din";
    public const string DispenseRequester = "DispenseRequest.Requester";

    /// <summary>BPX-2 dispense status on the outbound BPS^O29.</summary>
    public const string DispenseStatus = "BloodProduct.DispenseStatus";
}
