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
