namespace BloodBankLIS.Printing.Templates;

/// <summary>
/// Data model for a specimen label. A plain DTO assembled by the print service from
/// the audited specimen/patient records; the template owns layout (docs A.1).
/// </summary>
public sealed record SpecimenLabelModel(
    string AccessionNumber,
    string PatientName,
    string MedicalRecordNumber,
    DateOnly DateOfBirth,
    string SpecimenType,
    DateTime CollectedUtc,
    string? DrawLocation);

/// <summary>
/// P-tag / compatibility tag data contract (docs A.4). Generated from the issue record
/// so the printed tag always reflects what was issued, including emergency marking.
/// </summary>
public sealed record CompatibilityTagModel(
    string PatientName,
    string MedicalRecordNumber,
    DateOnly DateOfBirth,
    string PatientBloodType,
    string UnitNumber,
    string UnitBloodType,
    string ProductName,
    string CrossmatchMethod,
    string CrossmatchResult,
    DateTime UnitExpiresUtc,
    DateTime IssuedUtc,
    string IssuedBy,
    bool IsEmergency,
    bool TestsIncomplete = false);
