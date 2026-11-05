namespace BloodBankLIS.Domain.Rules;

public static class MinimumStockRule
{
    public static int Shortfall(int onHand, int minimumAvailable) =>
        Math.Max(0, minimumAvailable - Math.Max(0, onHand));

    /// <summary>
    /// Open product orders still waiting for a unit outnumber the available units of that product. Each open order
    /// line wants at least one unit (orders carry no quantity), so this is a floor, not a projection.
    /// </summary>
    public static bool OpenOrdersExceedOnHand(int onHand, int openOrders) => openOrders > 0 && openOrders > onHand;
}
