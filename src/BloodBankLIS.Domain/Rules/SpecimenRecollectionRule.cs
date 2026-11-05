using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// After a tube is rejected or cancelled, the orders on it cannot be resulted until a new specimen is collected and
/// linked. The bench says so, and why, instead of a bare "blocked"; rejecting a tube names the orders that wait.
/// </summary>
public static class SpecimenRecollectionRule
{
    public const string Code = "SPEC-RECOLLECT";

    public const string BenchLabel = "Recollect";

    public static bool NeedsRecollection(SpecimenStatus? status) =>
        status is SpecimenStatus.Rejected or SpecimenStatus.Cancelled;

    /// <summary>What the bench shows on a line whose tube was rejected or cancelled.</summary>
    public static string BlockReason(string accessionNumber, SpecimenStatus status, string? rejectionReason) =>
        status == SpecimenStatus.Rejected
            ? $"Specimen {accessionNumber} was rejected{(string.IsNullOrWhiteSpace(rejectionReason) ? "" : $" ({rejectionReason.Trim()})")}. Collect a new specimen and link it to this order."
            : $"Specimen {accessionNumber} was cancelled. Collect a new specimen and link it to this order.";

    /// <summary>The warning on rejecting a tube that open orders still use.</summary>
    public static RuleResult OrdersWaiting(IReadOnlyList<string> orderNumbers) =>
        RuleResult.Warning(Code,
            $"{(orderNumbers.Count == 1 ? "Order" : "Orders")} {string.Join(", ", orderNumbers)} {(orderNumbers.Count == 1 ? "needs" : "need")} a new specimen. Collect one and accession it from the patient's chart; the test worklist shows these lines as Recollect.");
}
