namespace BloodBankLIS.Domain.Rules;

/// <summary>Canonical facility policy keys stored in <c>SystemSettings</c>.</summary>
public static class FacilityPolicyKeys
{
    public const string SpecimenAlloimmunizationHours = "Specimen.ValidityHours.AlloimmunizationRisk";
    public const string SpecimenStandardHours = "Specimen.ValidityHours.Standard";
    public const string SpecimenLookbackDays = "Specimen.AlloimmunizationLookbackDays";
    public const string RequireSecondVerifier = "Transfusion.RequireSecondVerifier";
    public const string BlockSelfVerify = "Result.BlockSelfVerify";
    public const string RetentionYears = "Record.RetentionYears";
    public const string SignatureValidityMinutes = "Signature.ValidityMinutes";
}
