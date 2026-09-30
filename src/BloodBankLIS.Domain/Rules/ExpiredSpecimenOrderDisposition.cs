using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// What to do with an open order when a linked specimen has expired.
/// Pending crossmatch orders keep the request and drop the specimen.
/// Every other open order is cancelled. Closed orders are left as history.
/// </summary>
public enum ExpiredSpecimenOrderAction
{
    None = 0,
    Unlink = 1,
    Cancel = 2
}

/// <summary>One order line, reduced to the facts the disposition needs.</summary>
public readonly record struct OrderLineDispositionFacts(
    OrderCategory Category,
    OrderType OrderType,
    string? TestCode,
    long? ProductTypeId,
    bool IsActive);

public static class ExpiredSpecimenOrderDisposition
{
    public const string CancellationReasonCode = "SPEC-EXPIRED";
    public const string CancellationReason = "Specimen expired.";
    public const string UnlinkReason = "Specimen expired.";

    public static bool IsOpen(OrderStatus status) =>
        status is not (OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued);

    /// <summary>Same boundary as <see cref="SpecimenExpirationRule"/>: at or past expiration.</summary>
    public static bool SpecimenIsExpired(DateTime? expiresUtc, DateTime nowUtc) =>
        expiresUtc is not null && nowUtc >= expiresUtc.Value;

    public static bool IsCrossmatchTestCode(string? code) =>
        code is not null
        && (string.Equals(code, "XM", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "CXM", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "EXM", StringComparison.OrdinalIgnoreCase));

    public static bool LineIsCrossmatch(OrderType lineType, string? testCode, IReadOnlySet<string> crossmatchTestCodes)
    {
        if (lineType == OrderType.Crossmatch || IsCrossmatchTestCode(testCode))
        {
            return true;
        }

        return testCode is not null && crossmatchTestCodes.Contains(testCode);
    }

    /// <summary>
    /// An open order is a pending crossmatch order when an active line is a crossmatch
    /// test, an active product line requires crossmatch, or the order has no active
    /// lines and the header is still a crossmatch.
    /// </summary>
    public static bool IsPendingCrossmatchOrder(
        OrderStatus status,
        OrderType headerType,
        IEnumerable<OrderLineDispositionFacts> lines,
        IReadOnlySet<string> crossmatchTestCodes,
        Func<long, bool> productRequiresCrossmatch)
    {
        if (!IsOpen(status))
        {
            return false;
        }

        var active = lines.Where(l => l.IsActive).ToList();
        if (active.Count == 0)
        {
            return headerType == OrderType.Crossmatch;
        }

        if (active.Any(l => LineIsCrossmatch(l.OrderType, l.TestCode, crossmatchTestCodes)))
        {
            return true;
        }

        return active.Any(l =>
            l.Category == OrderCategory.Product
            && l.ProductTypeId is > 0
            && productRequiresCrossmatch(l.ProductTypeId.Value));
    }

    public static ExpiredSpecimenOrderAction Decide(bool specimenExpired, bool orderOpen, bool pendingCrossmatchOrder)
    {
        if (!specimenExpired || !orderOpen)
        {
            return ExpiredSpecimenOrderAction.None;
        }

        return pendingCrossmatchOrder
            ? ExpiredSpecimenOrderAction.Unlink
            : ExpiredSpecimenOrderAction.Cancel;
    }
}
