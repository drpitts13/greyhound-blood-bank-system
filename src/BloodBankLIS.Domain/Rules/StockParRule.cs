namespace BloodBankLIS.Domain.Rules;

public static class StockParRule
{
    public static int Shortfall(int onHand, int minimumAvailable) =>
        Math.Max(0, minimumAvailable - Math.Max(0, onHand));
}
