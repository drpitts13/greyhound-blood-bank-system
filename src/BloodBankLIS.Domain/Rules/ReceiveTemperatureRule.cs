namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace consignee receipt: record shipping-container temperature
/// and reject units outside the AABB cooler range (1–10 °C).
/// INSTITUTIONAL_POLICY_REVIEW: frozen plasma and other product-class ranges.
/// </summary>
public static class ReceiveTemperatureRule
{
    public const string Code = "INV-RCV-TEMP";
    public const decimal MinCelsius = 1.0m;
    public const decimal MaxCelsius = 10.0m;

    public static RuleResult Evaluate(bool required, decimal? celsius)
    {
        if (!required)
        {
            return RuleResult.Pass(Code);
        }

        if (celsius is null)
        {
            return RuleResult.HardStop(
                Code,
                "Record the receipt temperature in Celsius before accepting the unit.");
        }

        if (celsius.Value < MinCelsius || celsius.Value > MaxCelsius)
        {
            return RuleResult.HardStop(
                Code,
                $"Receipt temperature {celsius.Value:0.#} °C is outside the {MinCelsius:0.#}–{MaxCelsius:0.#} °C shipping range. Return the unit to the supplier.");
        }

        return RuleResult.Pass(Code);
    }
}
