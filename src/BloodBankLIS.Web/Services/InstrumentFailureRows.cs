using BloodBankLIS.Application.Admin;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The Instruments message list's open failures, readable from the row (end-user testing pass 141: rows gave the time,
/// kind, status and specimen, so each failure had to be opened to learn why it failed or whose it was, and the same
/// failure sent twice, messages 8 and 9, looked like two problems). A row says why it failed, and a later open failure
/// with the same specimen and the same reason says it repeats the earlier one, so one fix covers both.
/// </summary>
public static class InstrumentFailureRows
{
    public const int ReasonLength = 140;

    /// <summary>The failure's reason, shortened for the row; the full text is in the message.</summary>
    public static string? Reason(InstrumentMessageSummaryDto message)
    {
        if (!message.OpenFailure || string.IsNullOrWhiteSpace(message.ErrorDetail))
        {
            return null;
        }

        var text = message.ErrorDetail.Trim();
        return text.Length <= ReasonLength ? text : text[..(ReasonLength - 1)].TrimEnd() + "…";
    }

    /// <summary>
    /// For each open failure that repeats an earlier open failure (same specimen, same reason), the id of the first one.
    /// </summary>
    public static IReadOnlyDictionary<long, long> RepeatOf(IEnumerable<InstrumentMessageSummaryDto> messages)
    {
        var result = new Dictionary<long, long>();
        foreach (var group in messages
                     .Where(m => m.OpenFailure && !string.IsNullOrWhiteSpace(m.SpecimenId) && !string.IsNullOrWhiteSpace(m.ErrorDetail))
                     .GroupBy(m => (Specimen: m.SpecimenId!.Trim().ToUpperInvariant(), Reason: m.ErrorDetail!.Trim())))
        {
            var ordered = group.OrderBy(m => m.ReceivedUtc).ThenBy(m => m.Id).ToList();
            foreach (var later in ordered.Skip(1))
            {
                result[later.Id] = ordered[0].Id;
            }
        }

        return result;
    }
}
