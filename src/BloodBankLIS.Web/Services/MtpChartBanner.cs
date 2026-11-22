using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The chart header's note while the patient's massive transfusion protocol is open (end-user testing pass 317: Demo-
/// Massive, Morgan's chart said nothing of MTP #1, open for 13 days, which only the issue desk showed).
/// </summary>
public static class MtpChartBanner
{
    public static string For(MtpActivationDto mtp, DateTime nowUtc)
    {
        var caller = string.IsNullOrWhiteSpace(mtp.RequestedBy) ? string.Empty : $" by {mtp.RequestedBy.Trim()}";
        var open = BloodBankLIS.Application.Compliance.AlertInboxView.OpenFor(DateTime.SpecifyKind(mtp.ActivatedUtc, DateTimeKind.Utc), nowUtc);
        return $"Massive transfusion protocol #{mtp.Id} active: called {ClinicalTimeText.Local(mtp.ActivatedUtc)}{caller}, open {open}.";
    }
}
