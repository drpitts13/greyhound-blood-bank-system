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
    public string? Collector { get; set; }
    public string? DrawLocation { get; set; }
    public string? Comment { get; set; }
    public bool AcceptImmediately { get; set; } = true;
    // The visit the tube was drawn on; 0 when none is chosen.
    public long EncounterId { get; set; }
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
    public string Race { get; set; } = string.Empty;
    public string Ethnicity { get; set; } = string.Empty;
    public PatientStatus Status { get; set; } = PatientStatus.Active;
    public string? Comment { get; set; }
    public string NameChangeReason { get; set; } = string.Empty;
}

public static class SpecimenTypeOrderHint
{
    public static string? FirstRequiredType(
        IEnumerable<TestDefinitionListItemDto> tests,
        IEnumerable<string> selectedCodes)
    {
        // A type that suits every selected test, when there is one; otherwise the first test's preferred type.
        var codes = selectedCodes.ToList();
        var (common, _) = AcceptedSpecimenTypes.Common(Selected(tests, codes));
        if (common.Count > 0)
        {
            return common[0];
        }

        var byCode = tests.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes)
        {
            if (byCode.TryGetValue(code, out var test) && AcceptedSpecimenTypes.Preferred(test.RequiredSpecimenType) is { } preferred)
            {
                return preferred;
            }
        }

        return null;
    }

    /// <summary>The selected tests as (code, accepted types) pairs for <see cref="AcceptedSpecimenTypes"/>.</summary>
    public static IReadOnlyList<(string TestCode, string? Stored)> Selected(
        IEnumerable<TestDefinitionListItemDto> tests,
        IEnumerable<string> selectedCodes)
    {
        var byCode = tests.GroupBy(t => t.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return selectedCodes
            .Where(byCode.ContainsKey)
            .Select(c => (byCode[c].Code, byCode[c].RequiredSpecimenType))
            .ToList();
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
                && !AcceptedSpecimenTypes.Accepts(t.RequiredSpecimenType, specimenType))
            .Select(t => $"{t.Code} requires {AcceptedSpecimenTypes.Describe(t.RequiredSpecimenType)}")
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
