namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Where an open delivery stands on the Open deliveries board (end-user testing pass 142: two deliveries with every unit
/// entered, 1 of 1 and 2 of 2, had stayed open three days, each row offering only "Enter units", so nothing said they
/// were waiting to be completed against the packing list). A row says how many units are still to enter, or that all
/// are entered (or more than expected) and the delivery is ready to review and complete.
/// </summary>
public static class OpenDeliveryStatus
{
    public static bool ReadyToComplete(int expected, int entered) => expected > 0 && entered >= expected;

    public static string Text(int expected, int entered) =>
        expected <= 0
            ? $"{entered} entered; no expected count"
            : entered < expected
                ? $"{expected - entered} of {expected} still to enter"
                : entered == expected
                    ? "All entered: review and complete"
                    : $"{entered - expected} more than expected: review and complete with a note";
}
