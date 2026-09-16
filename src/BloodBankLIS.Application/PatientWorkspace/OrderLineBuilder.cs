using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.PatientWorkspace;

public static class OrderLineBuilder
{
    public static OrderType MapTestOrderType(string? code) => code?.ToUpperInvariant() switch
    {
        "TNS" or "TS" => OrderType.TypeAndScreen,
        "XM" or "CXM" or "EXM" => OrderType.Crossmatch,
        "ABORH" => OrderType.AboRh,
        "ABID" => OrderType.AntibodyIdentification,
        _ => OrderType.Other
    };

    public static bool IsCrossmatchTestCode(string? code) =>
        code is not null
        && (string.Equals(code, "XM", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "CXM", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "EXM", StringComparison.OrdinalIgnoreCase));

    public static void ApplyHeaderFromLines(Order order, IReadOnlyList<OrderLine> activeLines)
    {
        var ordered = activeLines.Where(l => l.IsActive).OrderBy(l => l.LineNumber).ToList();
        order.OrderName = ordered.Count == 0
            ? string.Empty
            : string.Join(", ", ordered.Select(l => l.LineName));

        var hasTest = ordered.Any(l => l.LineCategory == OrderCategory.Test);
        var hasProduct = ordered.Any(l => l.LineCategory == OrderCategory.Product);
        order.OrderCategory = hasTest && hasProduct
            ? OrderCategory.Mixed
            : hasProduct
                ? OrderCategory.Product
                : OrderCategory.Test;

        var firstTest = ordered.FirstOrDefault(l => l.LineCategory == OrderCategory.Test);
        var firstProduct = ordered.FirstOrDefault(l => l.LineCategory == OrderCategory.Product);
        order.TestCode = firstTest?.TestCode;
        order.OrderType = firstTest?.OrderType ?? OrderType.Other;
        order.ProductTypeId = firstProduct?.ProductTypeId;
        order.FulfillmentStatus = hasProduct ? FulfillmentStatus.Ordered : null;
    }

    public static bool RequiresCrossmatchLine(IReadOnlyList<OrderLineInputDto> lines, IReadOnlyDictionary<long, ProductType> productTypes)
    {
        var hasXm = lines.Any(l =>
            l.LineCategory == OrderCategory.Test
            && IsCrossmatchTestCode(l.TestCode));

        if (hasXm)
        {
            return false;
        }

        return lines.Any(l =>
            l.LineCategory == OrderCategory.Product
            && l.ProductTypeId is > 0
            && productTypes.TryGetValue(l.ProductTypeId.Value, out var pt)
            && pt.RequiresCrossmatch);
    }

    /// <summary>
    /// Product orders no longer receive a hardcoded XM line at create time.
    /// Crossmatch tests attach after the current antibody screen is verified.
    /// </summary>
    public static IReadOnlyList<OrderLineInputDto> WithCrossmatchLineIfNeeded(
        IReadOnlyList<OrderLineInputDto> lines,
        IReadOnlyDictionary<long, ProductType> productTypes)
    {
        _ = productTypes;
        return lines;
    }
}
