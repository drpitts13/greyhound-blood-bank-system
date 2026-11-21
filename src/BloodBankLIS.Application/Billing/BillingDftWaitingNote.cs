using BloodBankLIS.Application.Compliance;

namespace BloodBankLIS.Application.Billing;

/// <summary>
/// What the billing queue says under a charge's DFT that has not gone (end-user testing pass 176): how long it has been
/// queued and why it has not been sent, in the outbound sender's words.
/// </summary>
public static class BillingDftWaitingNote
{
    public static string For(DateTime queuedUtc, DateTime nowUtc, string reason) =>
        $"Not sent, queued {AlertInboxView.OpenFor(queuedUtc, nowUtc)}: {reason}";
}
