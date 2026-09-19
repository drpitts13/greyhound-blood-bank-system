using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

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
    public DateTime ExpiresLocal { get; set; } = DateTime.UtcNow;
    public string SpecimenType { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? DrawLocation { get; set; }
    public string? Collector { get; set; }
    public int PolicyHours { get; set; } = 168;
    public DateTime PolicyExpires { get; set; } = DateTime.UtcNow;
    public bool ExpiryTouched { get; set; }
    public string? Comment { get; set; }
    public string OverrideReason { get; set; } = string.Empty;
    public string AuthorizedBy { get; set; } = string.Empty;
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

public static class SpecimenTypeOrderHint
{
    public static string? FirstRequiredType(
        IEnumerable<TestDefinitionListItemDto> tests,
        IEnumerable<string> selectedCodes)
    {
        var byCode = tests.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var code in selectedCodes)
        {
            if (byCode.TryGetValue(code, out var test) && !string.IsNullOrWhiteSpace(test.RequiredSpecimenType))
            {
                return test.RequiredSpecimenType;
            }
        }

        return null;
    }

    public static string? MismatchWarning(
        IEnumerable<TestDefinitionListItemDto> tests,
        IEnumerable<string> selectedCodes,
        string? specimenType)
    {
        if (string.IsNullOrWhiteSpace(specimenType))
        {
            return null;
        }

        var mismatched = tests
            .Where(t => selectedCodes.Contains(t.Code, StringComparer.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(t.RequiredSpecimenType)
                && !string.Equals(t.RequiredSpecimenType, specimenType, StringComparison.OrdinalIgnoreCase))
            .Select(t => $"{t.Code} requires {t.RequiredSpecimenType}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (mismatched.Count == 0)
        {
            return null;
        }

        return $"Specimen type '{specimenType}' does not match {string.Join("; ", mismatched)}. Result entry will be blocked unless the specimen type matches.";
    }

    public static DateTime? PreviewExpiresUtc(
        DateTime collectedUtc,
        string? specimenType,
        IEnumerable<SpecimenTypeListItemDto> types)
    {
        var type = types.FirstOrDefault(t =>
            string.Equals(t.Code, specimenType, StringComparison.OrdinalIgnoreCase));
        if (type is null || !SpecimenExpirationCode.TryParse(type.ExpirationCode, out var code))
        {
            return null;
        }

        return SpecimenExpirationCalculator.ComputeExpiresUtc(collectedUtc, code, type.ExpirationMode);
    }
}
