namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Duplicate testing: a test being ordered that the patient already has open on another order (not completed,
/// cancelled, or discontinued; the line active and its result not verified). It warns and never blocks, because a
/// repeat can be intended (a second ABO/Rh determination, a new specimen after a rejection); the new order can be
/// cancelled in one step when it was not.
/// </summary>
public static class DuplicateTestOrderRule
{
    public const string Code = "ORD-DUP-TEST";

    public sealed record OpenLine(string TestCode, string OrderNumber);

    public static RuleResult? Evaluate(IEnumerable<string> orderedTestCodes, IEnumerable<OpenLine> openLines)
    {
        var ordered = orderedTestCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicates = openLines
            .Where(l => ordered.Contains(l.TestCode.Trim()))
            .GroupBy(l => l.TestCode.Trim().ToUpperInvariant())
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key} on {string.Join(", ", g.Select(l => l.OrderNumber).Distinct().Order(StringComparer.OrdinalIgnoreCase))}")
            .ToList();
        if (duplicates.Count == 0)
        {
            return null;
        }

        return RuleResult.Warning(Code,
            $"Already ordered and still open: {string.Join("; ", duplicates)}. If this order repeats it by mistake, cancel it; one result can serve the open order.");
    }

    /// <summary>
    /// An order from the EHR has no one at the screen to see the warning, so the bench is told by alert, linked to the
    /// new order on the patient's chart. One per order; it never blocks or cancels anything.
    /// </summary>
    public static AlertCandidate Alert(long patientId, string orderNumber, string? mrn, string? patientName, string warning)
    {
        var who = (string.IsNullOrWhiteSpace(patientName), string.IsNullOrWhiteSpace(mrn)) switch
        {
            (false, false) => $"{patientName!.Trim()} (MRN {mrn!.Trim()})",
            (true, false) => $"MRN {mrn!.Trim()}",
            (false, true) => patientName!.Trim(),
            _ => $"patient {patientId}"
        };
        return new AlertCandidate(
            AlertRule.DuplicateEhrOrder,
            $"DUPORDER:{orderNumber.Trim()}",
            AlertSeverity.Warning,
            $"EHR order {orderNumber.Trim()} repeats an open test",
            $"Order {orderNumber.Trim()} for {who} came from the EHR. {warning}",
            $"/patients/{patientId}?tab=orders&order={Uri.EscapeDataString(orderNumber.Trim())}");
    }
}
