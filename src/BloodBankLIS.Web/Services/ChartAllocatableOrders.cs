using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The orders the chart's reserve form offers: each open order once per product it asks for. An order for two units of
/// red cells is one choice, not two identical lines; an order for red cells and plasma is two (the product decides which
/// units can be reserved on it).
/// </summary>
public static class ChartAllocatableOrders
{
    public static List<AllocatableOrderDto> From(IEnumerable<PatientOrderDto> orders, long patientId) =>
        orders
            .Where(o => o.IsActive)
            .SelectMany(o => o.Lines
                .Where(l => l.LineCategory == OrderCategory.Product && l.ProductTypeId is > 0)
                .GroupBy(l => l.ProductTypeId!.Value)
                .Select(g => new AllocatableOrderDto(
                    o.Id,
                    patientId,
                    string.Empty,
                    string.Empty,
                    o.OrderNumber,
                    g.First().LineName,
                    g.Key,
                    o.SpecimenId,
                    o.AccessionNumber,
                    o.OrderedUtc)))
            .ToList();

    /// <summary>
    /// The units the reserve form offers: with an order chosen, only compatible units of the product(s) that order asks
    /// for (platelets and plasma are not offered on a red cell order); with none chosen, every compatible unit.
    /// Guidance only: reserving still checks the unit against the order.
    /// </summary>
    public static IReadOnlyList<CompatibleUnitDto> UnitsFor(
        IReadOnlyList<CompatibleUnitDto> compatible, IEnumerable<AllocatableOrderDto> orders, long orderId)
    {
        if (orderId <= 0)
        {
            return compatible;
        }

        var products = orders.Where(o => o.OrderId == orderId).Select(o => o.ProductTypeId).ToHashSet();
        return products.Count == 0 ? compatible : compatible.Where(u => products.Contains(u.ProductTypeId)).ToList();
    }

    /// <summary>
    /// "2 compatible units of Red Blood Cells, Leukoreduced in stock" for the chosen order, or "No compatible units of …
    /// in stock" so an empty picker is not a mystery; null when no order is chosen.
    /// </summary>
    public static string? OfferedLine(IReadOnlyList<CompatibleUnitDto> offered, IEnumerable<AllocatableOrderDto> orders, long orderId)
    {
        if (orderId <= 0)
        {
            return null;
        }

        var names = orders.Where(o => o.OrderId == orderId).Select(o => o.OrderName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0)
        {
            return null;
        }

        var what = string.Join(" or ", names);
        return offered.Count == 0
            ? $"No compatible units of {what} in stock."
            : $"{offered.Count} compatible {(offered.Count == 1 ? "unit" : "units")} of {what} in stock.";
    }
}
