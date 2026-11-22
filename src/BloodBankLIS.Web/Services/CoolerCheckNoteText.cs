using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// What a cooler check with differences found, written under the "Cooler differences" badge on the In transit board
/// (end-user testing pass 290: what differed was only in the badge's hover text, which a touch screen never shows).
/// </summary>
public static class CoolerCheckNoteText
{
    public static string? For(DateTime? lastCheckedUtc, bool? lastCheckReady, string? summary, string? checkedBy)
    {
        if (lastCheckedUtc is not DateTime at || lastCheckReady == true)
        {
            return null;
        }

        var who = string.IsNullOrWhiteSpace(checkedBy) ? string.Empty : $" by {checkedBy.Trim()}";
        var what = string.IsNullOrWhiteSpace(summary) ? string.Empty : $": {summary.Trim()}";
        return $"Checked {ClinicalTimeText.Local(at)}{who}{what}";
    }
}
