using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Components.Shared;

public sealed class PatientAccessionForm
{
    public string AccessionNumber { get; set; } = string.Empty;
    public string SpecimenType { get; set; } = "EDTA";
    public DateTime CollectedLocal { get; set; } = DateTime.UtcNow;
    public int? ValidityHours { get; set; }
    public string? Identifier1Value { get; set; }
    public string? Identifier2Value { get; set; }
    public string? Comment { get; set; }
}

public sealed class PatientSpecimenEditForm
{
    public DateTime CollectedLocal { get; set; } = DateTime.UtcNow;
    public string? Barcode { get; set; }
    public string? DrawLocation { get; set; }
    public string? Collector { get; set; }
    public int? ValidityHours { get; set; }
    public string? Comment { get; set; }
}

public sealed class PatientDemoEditModel
{
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Sex Sex { get; set; } = Sex.Unknown;
    public PatientStatus Status { get; set; } = PatientStatus.Active;
    public string? Comment { get; set; }
}
