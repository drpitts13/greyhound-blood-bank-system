using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Plain wording for a row of blood type history: where the type came from, and whether it is one of the ABO/Rh
/// determinations the two-determination rule can use (a retired type and an undocumented manual entry are not).
/// </summary>
public static class BloodTypeHistoryLabel
{
    public static string Source(BloodTypeSource source, ManualTypeDocumentation? documentation, string? reference) => source switch
    {
        BloodTypeSource.TestResult => "Tested result",
        BloodTypeSource.Hl7 => "Interface result",
        BloodTypeSource.HistoricalImport => "Imported from a previous blood bank system",
        BloodTypeSource.ManualEntry => documentation switch
        {
            ManualTypeDocumentation.PreviousBloodBankSystem => $"Manual, from a previous blood bank system: {reference}",
            ManualTypeDocumentation.OutsideSource => $"Manual, from a documented outside source: {reference}",
            _ => "Manual, not documented"
        },
        _ => source.ToString()
    };

    public static bool CountsAsDetermination(BloodTypeSource source, ManualTypeDocumentation? documentation, bool retired) =>
        !retired && ManualBloodTypeDocumentationRule.CountsAsDetermination(source, documentation);
}
