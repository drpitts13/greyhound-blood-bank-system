using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-038 (product owner, 2026-11-01): an INACTIVE, UNVERIFIED starting set for the transfusion committee to review.
/// The values are commonly published restrictive thresholds, offered only so the committee has something concrete to
/// edit. They are not a recommendation of this software. REQUIRES REGULATORY / SME VERIFICATION against current
/// guidance and local policy. Lab test codes are placeholders to map to the facility's own test codes.
/// Every draft is loaded inactive with a DRAFT code and name, and cannot be activated until it has been reviewed and
/// renamed (<see cref="TransfusionIndicationRule.DraftCode"/>).
/// </summary>
public static class PbmDraftCriteria
{
    public const string CodePrefix = "DRAFT-";
    public const string NamePrefix = "DRAFT (unverified): ";

    public sealed record Draft(
        string Code,
        string Name,
        ComponentClass ComponentClass,
        string? LabTestCode,
        IndicationComparison? Comparison,
        decimal? Threshold,
        string? Unit,
        int LookbackHours,
        int SortOrder);

    public static IReadOnlyList<Draft> All { get; } =
    [
        new(CodePrefix + "HB7", NamePrefix + "Hemoglobin below 7 g/dL, hospitalized stable adult", ComponentClass.RedBloodCells, "HGB", IndicationComparison.LessThan, 7m, "g/dL", 24, 10),
        new(CodePrefix + "HB8", NamePrefix + "Hemoglobin below 8 g/dL, cardiac or orthopedic surgery or cardiovascular disease", ComponentClass.RedBloodCells, "HGB", IndicationComparison.LessThan, 8m, "g/dL", 24, 20),
        new(CodePrefix + "RBC-BLEED", NamePrefix + "Active bleeding or hemodynamic instability (orderer attests)", ComponentClass.RedBloodCells, null, null, null, null, 24, 30),
        new(CodePrefix + "PLT10", NamePrefix + "Platelets below 10 x10^9/L, prophylaxis", ComponentClass.Platelets, "PLT", IndicationComparison.LessThan, 10m, "x10^9/L", 24, 10),
        new(CodePrefix + "PLT50", NamePrefix + "Platelets below 50 x10^9/L before a major invasive procedure", ComponentClass.Platelets, "PLT", IndicationComparison.LessThan, 50m, "x10^9/L", 24, 20),
        new(CodePrefix + "PLT-BLEED", NamePrefix + "Active bleeding with thrombocytopenia or platelet dysfunction (orderer attests)", ComponentClass.Platelets, null, null, null, null, 24, 30),
        new(CodePrefix + "INR", NamePrefix + "INR above 1.5 with bleeding or before an invasive procedure", ComponentClass.Plasma, "INR", IndicationComparison.GreaterThan, 1.5m, "ratio", 24, 10),
        new(CodePrefix + "FFP-MTP", NamePrefix + "Massive transfusion protocol activated (orderer attests)", ComponentClass.Plasma, null, null, null, null, 24, 20),
        new(CodePrefix + "FIB", NamePrefix + "Fibrinogen below 150 mg/dL with bleeding", ComponentClass.Cryoprecipitate, "FIB", IndicationComparison.LessThan, 150m, "mg/dL", 24, 10)
    ];

    public static bool IsDraft(string? code, string? name) =>
        (code?.Trim().StartsWith(CodePrefix, StringComparison.OrdinalIgnoreCase) ?? false)
        || (name?.TrimStart().StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase) ?? false);
}
