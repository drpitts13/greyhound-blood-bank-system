namespace BloodBankLIS.Domain.Rules;

public static class MinimumStockRule
{
    public static int Shortfall(int onHand, int minimumAvailable) =>
        Math.Max(0, minimumAvailable - Math.Max(0, onHand));

    /// <summary>
    /// What a shortage still needs ordered: the shortfall less units already on submitted supplier orders that have not
    /// arrived. Those units are not on the shelf, so the product stays short until they are received.
    /// </summary>
    public static int StillToOrder(int shortfall, int onOrder) =>
        Math.Max(0, Math.Max(0, shortfall) - Math.Max(0, onOrder));

    /// <summary>
    /// Open product orders still waiting for a unit outnumber the available units of that product. Each open order
    /// line wants at least one unit (orders carry no quantity), so this is a floor, not a projection.
    /// </summary>
    public static bool OpenOrdersExceedOnHand(int onHand, int openOrders) => openOrders > 0 && openOrders > onHand;
}
