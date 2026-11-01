namespace BloodBankLIS.Domain.Rules;

public static class MinimumStockRule
{
    public static int Shortfall(int onHand, int minimumAvailable) =>
        Math.Max(0, minimumAvailable - Math.Max(0, onHand));
}
