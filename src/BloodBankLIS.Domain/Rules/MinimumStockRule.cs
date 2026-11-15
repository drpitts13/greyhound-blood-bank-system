using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

public static class MinimumStockRule
{
    /// <summary>
    /// The blood group a minimum covers, as the board and alerts name it: "O Neg", "O", "Rh Neg" (any ABO), or
    /// <paramref name="any"/> when neither is set (pass 105).
    /// </summary>
    public static string GroupText(AboGroup? abo, RhType? rhD, string any = "Any") =>
        (abo, rhD) switch
        {
            (null, null) => any,
            (AboGroup a, null) => a.ToString(),
            (null, RhType r) => $"Rh {RhText(r)}",
            (AboGroup a, RhType r) => $"{a} {RhText(r)}"
        };

    private static string RhText(RhType r) => r switch
    {
        RhType.Positive => "Pos",
        RhType.Negative => "Neg",
        _ => r.ToString()
    };

    /// <summary>A supplier order line counts toward a minimum when it is for the same product and within its group.</summary>
    public static bool Covers(long levelProductTypeId, AboGroup? levelAbo, RhType? levelRhD, long lineProductTypeId, AboGroup? lineAbo, RhType? lineRhD) =>
        lineProductTypeId == levelProductTypeId
        && (levelAbo is null || lineAbo == levelAbo)
        && (levelRhD is null || lineRhD == levelRhD);

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
