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
    public const string RetentionYears = "Record.RetentionYears";
    public const string SignatureValidityMinutes = "Signature.ValidityMinutes";
}
