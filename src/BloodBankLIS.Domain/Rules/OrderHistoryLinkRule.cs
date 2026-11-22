namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Which order's history opens when the chart's Orders tab is reached for one order number (the find box, Blood orders
/// to prepare): the single order whose number matches exactly, ignoring case and surrounding spaces. No match, or more
/// than one, opens nothing; the tab still shows the search.
/// </summary>
public static class OrderHistoryLinkRule
{
    public static long? HistoryToOpen(IEnumerable<(long Id, string OrderNumber)> orders, string? orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return null;
        }

        var wanted = orderNumber.Trim();
        var matches = orders.Where(o => string.Equals(o.OrderNumber?.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
            .Select(o => o.Id).Distinct().Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
}
