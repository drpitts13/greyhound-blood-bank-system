using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The chart header's note when the patient's new specimens take the shorter alloimmunization-risk window, and why
/// (end-user testing pass 309: a recent pregnancy was shown only on the Overview tab; pass 310: a recent transfusion was
/// never named, and the window and look-back are the facility's, not fixed).
/// </summary>
public static class SpecimenWindowBanner
{
    public static string? For(SpecimenWindowDto? window)
    {
        if (window is not { AlloimmunizationRisk: true })
        {
            return null;
        }

        var reasons = new List<string>();
        if (window.RecentPregnancyUtc is DateTime pregnancy)
        {
            reasons.Add($"recent pregnancy {ClinicalTimeText.Local(pregnancy)[..10]}");
        }

        if (window.LastTransfusionUtc is DateTime transfused)
        {
            reasons.Add($"transfused {ClinicalTimeText.Local(transfused)[..10]}");
        }

        var why = string.Join(" and ", reasons);
        return $"{char.ToUpperInvariant(why[0])}{why[1..]} (within {window.LookbackDays} days): new specimens expire {window.AlloimmunizationRiskHours} h after collection.";
    }
}
