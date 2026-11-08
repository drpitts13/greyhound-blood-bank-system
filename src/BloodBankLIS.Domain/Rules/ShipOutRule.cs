using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Shipping a unit to another facility (stock balancing, a patient transferred with blood). The receiving facility is
/// required (it is who a recall or lookback must notify); only an unreserved unit in stock (Available or Received) ships;
/// an expired unit does not. What the shipment must also record (container, temperature, packer) is the facility's to
/// set (OCD-072).
/// </summary>
public static class ShipOutRule
{
    public const string Code = "INV-SHIP-OUT";
    public const int MaxFacilityLength = 200;
    public const int MaxReferenceLength = 100;

    /// <summary>
    /// Which shipment a shipped unit belongs to: the facility (ignoring case and spaces) and the reference, or, without a
    /// reference, the facility and the UTC day it was shipped.
    /// </summary>
    public static string ShipmentKey(string? facility, string? reference, DateTime? shippedUtc)
    {
        var place = string.Join(' ', (facility ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        var shipment = string.IsNullOrWhiteSpace(reference)
            ? shippedUtc?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
            : reference.Trim().ToUpperInvariant();
        return $"{place}|{shipment}";
    }

    public static RuleResult Evaluate(UnitStatus status, DateTime expiresUtc, DateTime nowUtc, string? facility, string? reference)
    {
        var to = facility?.Trim() ?? string.Empty;
        if (to.Length == 0)
        {
            return RuleResult.HardStop(Code, "Name the facility the units are shipped to.");
        }

        if (to.Length > MaxFacilityLength)
        {
            return RuleResult.HardStop(Code, $"A facility name is at most {MaxFacilityLength} characters.");
        }

        if ((reference?.Trim().Length ?? 0) > MaxReferenceLength)
        {
            return RuleResult.HardStop(Code, $"A shipment reference is at most {MaxReferenceLength} characters.");
        }

        if (status == UnitStatus.ShippedOut)
        {
            return RuleResult.HardStop(Code, "The unit was already shipped out.");
        }

        if (status is not (UnitStatus.Available or UnitStatus.Received))
        {
            return RuleResult.HardStop(Code, $"The unit is {status}; only an unreserved unit in stock ships. Release its reservation or resolve it first.");
        }

        return expiresUtc <= nowUtc
            ? RuleResult.HardStop(Code, $"The unit expired at {expiresUtc:yyyy-MM-dd HH:mm} UTC; an expired unit is not shipped.")
            : RuleResult.Pass(Code);
    }
}
