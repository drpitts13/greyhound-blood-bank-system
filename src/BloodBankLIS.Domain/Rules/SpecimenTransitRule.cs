namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Time from draw to receipt in the blood bank, shown on the pending-specimen list so delayed tubes stand out.
/// Display only: it sets no limit (the specimen's expiry is the limit that applies).
/// </summary>
public static class SpecimenTransitRule
{
    public static int? DrawToReceiptMinutes(DateTime collectedUtc, DateTime? receivedUtc) =>
        receivedUtc is DateTime received && received >= collectedUtc
            ? (int)Math.Floor((received - collectedUtc).TotalMinutes)
            : null;

    public static int WaitingMinutes(DateTime collectedUtc, DateTime nowUtc) =>
        nowUtc <= collectedUtc ? 0 : (int)Math.Floor((nowUtc - collectedUtc).TotalMinutes);

    public static string Duration(int minutes) =>
        minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60:00} min";

    /// <summary>"received 42 min after draw" or "waiting 1 h 05 min since draw".</summary>
    public static string Describe(DateTime collectedUtc, DateTime? receivedUtc, DateTime nowUtc) =>
        DrawToReceiptMinutes(collectedUtc, receivedUtc) is int transit
            ? $"received {Duration(transit)} after draw"
            : $"waiting {Duration(WaitingMinutes(collectedUtc, nowUtc))} since draw";
}
