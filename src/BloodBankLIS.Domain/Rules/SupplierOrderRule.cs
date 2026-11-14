namespace BloodBankLIS.Domain.Rules;

public enum SupplierOrderStatus
{
    Draft = 0,
    Submitted = 1,
    Received = 2,
    Cancelled = 3
}

/// <summary>
/// Supplier order lifecycle: Draft (editable) → Submitted (signed) → Received (linked to
/// a delivery from the same supplier). Draft or Submitted can be cancelled with a reason.
/// The quantity cap is an entry guard, not an ordering rule.
/// </summary>
public static class SupplierOrderRule
{
    public const string SignatureAction = "SupplierOrderSubmit";
    public const int MaxLineQuantity = 999;

    /// <summary>
    /// When a submitted order's blood is due: the submit time plus <c>Inventory.ExpectedArrivalDueHours</c>, the same
    /// window as an expected packing list. Null when the order has not been submitted.
    /// </summary>
    public static DateTime? ArrivalDueUtc(DateTime? submittedUtc, int dueHours) =>
        submittedUtc is DateTime submitted ? submitted.AddHours(Math.Max(1, dueHours)) : null;

    /// <summary>A submitted order not received by its arrival time: chase the supplier or plan around it.</summary>
    public static bool IsArrivalOverdue(SupplierOrderStatus status, DateTime? submittedUtc, int dueHours, DateTime nowUtc, DateTime? promisedUtc = null) =>
        status == SupplierOrderStatus.Submitted && (promisedUtc ?? ArrivalDueUtc(submittedUtc, dueHours)) is DateTime due && nowUtc > due;

    public const string FollowUpCode = "SUPPLIER-ORDER-FOLLOWUP";

    /// <summary>
    /// A follow-up with the supplier is recorded on a submitted order, with what was said; a promised arrival, when given,
    /// is in the future (end-user testing pass 45: an overdue order could only be cancelled).
    /// </summary>
    public static RuleResult EvaluateFollowUp(SupplierOrderStatus status, string? note, DateTime? promisedUtc, DateTime nowUtc)
    {
        if (status != SupplierOrderStatus.Submitted)
        {
            return RuleResult.HardStop(FollowUpCode, "A follow-up is recorded on a submitted order that has not arrived.");
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return RuleResult.HardStop(FollowUpCode, "Say who you spoke to and what they said.");
        }

        return promisedUtc is DateTime promised && promised <= nowUtc
            ? RuleResult.HardStop(FollowUpCode, "The promised arrival must be later than now.")
            : RuleResult.Pass(FollowUpCode);
    }

    public const string NotDraftCode = "SUPORD-NOT-DRAFT";
    public const string EmptyCode = "SUPORD-EMPTY";
    public const string QuantityCode = "SUPORD-QTY";
    public const string SignatureCode = "SUPORD-SIGNATURE";
    public const string NotSubmittedCode = "SUPORD-NOT-SUBMITTED";
    public const string SupplierMismatchCode = "SUPORD-SUPPLIER";
    public const string CancelCode = "SUPORD-CANCEL";

    public static RuleResult EvaluateEdit(SupplierOrderStatus status) =>
        status == SupplierOrderStatus.Draft
            ? RuleResult.Pass(NotDraftCode)
            : RuleResult.HardStop(NotDraftCode, $"Only a draft order can be changed. This order is {status}.");

    public static RuleResult EvaluateQuantity(int quantity) =>
        quantity is >= 0 and <= MaxLineQuantity
            ? RuleResult.Pass(QuantityCode)
            : RuleResult.HardStop(QuantityCode, $"Quantity must be 0 to {MaxLineQuantity}.");

    public static RuleResult EvaluateSubmit(SupplierOrderStatus status, int unitsOrdered, bool signatureValid)
    {
        if (status != SupplierOrderStatus.Draft)
        {
            return RuleResult.HardStop(NotDraftCode, $"Only a draft order can be submitted. This order is {status}.");
        }

        if (unitsOrdered <= 0)
        {
            return RuleResult.HardStop(EmptyCode, "The order has no units to order.");
        }

        return signatureValid
            ? RuleResult.Pass(SignatureCode)
            : RuleResult.HardStop(SignatureCode, "Submitting an order needs your electronic signature.");
    }

    public static RuleResult EvaluateReceive(SupplierOrderStatus status, long orderSupplierId, long deliverySupplierId)
    {
        if (status != SupplierOrderStatus.Submitted)
        {
            return RuleResult.HardStop(NotSubmittedCode, $"Only a submitted order can be received. This order is {status}.");
        }

        return orderSupplierId == deliverySupplierId
            ? RuleResult.Pass(SupplierMismatchCode)
            : RuleResult.HardStop(SupplierMismatchCode, "The delivery is from a different supplier than the order.");
    }

    public static RuleResult EvaluateCancel(SupplierOrderStatus status, string? reason)
    {
        if (status is not (SupplierOrderStatus.Draft or SupplierOrderStatus.Submitted))
        {
            return RuleResult.HardStop(CancelCode, $"A {status} order cannot be cancelled.");
        }

        return string.IsNullOrWhiteSpace(reason)
            ? RuleResult.HardStop(CancelCode, "A reason is required to cancel an order.")
            : RuleResult.Pass(CancelCode);
    }
}
