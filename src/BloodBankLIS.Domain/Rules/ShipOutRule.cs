using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Shipping a unit to another facility (stock balancing, a patient transferred with blood). The receiving facility is
/// required (it is who a recall or lookback must notify); only an unreserved unit in stock (Available or Received) ships;
/// an expired unit does not. OCD-072 (decided 2026-11-10): the shipment also records the container, the packing
/// temperature, who packed it and the receiving contact.
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

    public const int MaxContainerLength = 100;
    public const int MaxPackedByLength = 100;
    public const int MaxContactLength = 200;

    /// <summary>Packing temperatures outside this range (°C) are taken as a typing slip, not a reading.</summary>
    public const decimal MinTemperatureC = -50m;
    public const decimal MaxTemperatureC = 50m;

    public const string DetailsCode = "INV-SHIP-DETAILS";

    /// <summary>
    /// OCD-072: the container, the packing temperature (°C), who packed it and the receiving contact are all required;
    /// the message names every one missing so the page can be completed in one go.
    /// </summary>
    public static RuleResult EvaluateDetails(string? container, decimal? temperatureC, string? packedBy, string? receivingContact)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(container))
        {
            missing.Add("the container");
        }

        if (temperatureC is null)
        {
            missing.Add("the packing temperature");
        }

        if (string.IsNullOrWhiteSpace(packedBy))
        {
            missing.Add("who packed it");
        }

        if (string.IsNullOrWhiteSpace(receivingContact))
        {
            missing.Add("the receiving contact");
        }

        if (missing.Count > 0)
        {
            return RuleResult.HardStop(DetailsCode, $"Record {Join(missing)} for the shipment (OCD-072).");
        }

        if (temperatureC is < MinTemperatureC or > MaxTemperatureC)
        {
            return RuleResult.HardStop(DetailsCode, $"A packing temperature of {temperatureC} °C is not a reading. Check it (between {MinTemperatureC} and {MaxTemperatureC} °C).");
        }

        if (container!.Trim().Length > MaxContainerLength || packedBy!.Trim().Length > MaxPackedByLength
            || receivingContact!.Trim().Length > MaxContactLength)
        {
            return RuleResult.HardStop(DetailsCode,
                $"The container and packer are at most {MaxContainerLength} characters, the receiving contact {MaxContactLength}.");
        }

        return RuleResult.Pass(DetailsCode);
    }

    private static string Join(IReadOnlyList<string> parts) => parts.Count switch
    {
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => string.Join(", ", parts.Take(parts.Count - 1)) + $" and {parts[^1]}"
    };

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
