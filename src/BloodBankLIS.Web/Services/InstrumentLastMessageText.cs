using BloodBankLIS.Application.Compliance;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// When an instrument last sent a message, for the status table (end-user testing pass 308: the ImmuLINK Echo read
/// "Listening" with no word that nothing had arrived for two days, and a silent instrument is the usual first sign of a
/// broken link on the bench).
/// </summary>
public static class InstrumentLastMessageText
{
    public static string For(DateTime? lastMessageUtc, DateTime nowUtc)
    {
        if (lastMessageUtc is not DateTime last)
        {
            return "No message received yet";
        }

        var age = AlertInboxView.OpenFor(last, nowUtc);
        return $"Last message {BloodBankLIS.Domain.Rules.ClinicalTimeText.Local(last)} ({(age == "just now" ? age : $"{age} ago")})";
    }
}
