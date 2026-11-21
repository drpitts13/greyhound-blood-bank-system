namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Why an outbound HL7 message (a result to the EHR, a charge to billing) has not gone, and whether it has waited long
/// enough to need someone (Cycle 660). The sender polls every few seconds, so a message still queued after
/// <see cref="StuckAfter"/> will not go by itself: no interface accepts it, or its retries are spent.
/// </summary>
public static class OutboundMessageWaitRule
{
    public static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(15);

    public const string NoInterface = "No enabled outbound interface is set up to send it. It sends once one is.";

    /// <summary>Said of a message whose retries are spent; the home card counts them by it.</summary>
    public const string RetriesSpent = "retries are spent";

    public static string Reason(bool hasInterface, bool lastSendFailed, int retryCount, int maxRetries)
    {
        if (!hasInterface)
        {
            return NoInterface;
        }

        if (retryCount >= maxRetries)
        {
            return $"Sending failed {retryCount} times and {RetriesSpent}. Fix the interface, then Send.";
        }

        return lastSendFailed
            ? $"The last send failed; it is retried ({retryCount} of {maxRetries} tries so far)."
            : "Queued; the next send attempt is within a minute.";
    }

    public static bool IsStuck(DateTime queuedUtc, DateTime nowUtc) => nowUtc - queuedUtc >= StuckAfter;
}
