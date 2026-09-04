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

    public const string EncounterAccountNumber = "Encounter.AccountNumber";
    public const string EncounterVisitNumber = "Encounter.VisitNumber";
    public const string EncounterAdmitUtc = "Encounter.AdmitUtc";
    public const string EncounterDischargeUtc = "Encounter.DischargeUtc";
    public const string EncounterCurrentLocation = "Encounter.CurrentLocation";
    public const string EncounterAttendingProviderId = "Encounter.AttendingProviderId";
    public const string EncounterAttendingProviderName = "Encounter.AttendingProviderName";

    public const string OrderControl = "Order.OrderControl";
    public const string OrderNumber = "Order.OrderNumber";
    public const string OrderTestCode = "Order.TestCode";
    public const string OrderProviderId = "Order.OrderingProviderId";
    public const string OrderProviderName = "Order.OrderingProviderName";
    public const string OrderLocationCode = "Order.OrderingLocationCode";

    public const string ResultObrTestCode = "TestResult.ObrTestCode";
    public const string ResultVerifiedUtc = "TestResult.VerifiedUtc";
    public const string ResultObxIdentifier = "TestResult.ObxIdentifier";
    public const string ResultValue = "TestResult.Value";
    public const string ResultUnits = "TestResult.Units";
    public const string ResultInterpretation = "TestResult.Interpretation";
    public const string ResultObxStatus = "TestResult.Status";

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
}
