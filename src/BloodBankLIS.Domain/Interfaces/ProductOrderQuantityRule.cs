namespace BloodBankLIS.Domain.Interfaces;

/// <summary>
/// The quantity of a blood product order from the EHR (end-user testing pass 119: an ORM/OML for a product was refused as
/// an unknown test, so a transfusion request could not arrive by interface). TQ1-2 gives how many units, or a volume when
/// its units are mL (an aliquot, one line). A blank quantity is one unit.
/// </summary>
public static class ProductOrderQuantityRule
{
    public const int MaxUnits = 20;

    /// <summary>(Units, VolumeMl), or an error naming what is wrong.</summary>
    public static (int Units, int? VolumeMl, string? Error) Read(string? quantity, string? units)
    {
        var unitText = (units ?? string.Empty).Trim().ToUpperInvariant();
        var isMl = unitText is "ML" or "MILLILITER" or "MILLILITERS" or "MILLILITRE" or "MILLILITRES";
        if (string.IsNullOrWhiteSpace(quantity))
        {
            return isMl ? (0, null, "The order gives mL but no volume.") : (1, null, null);
        }

        if (!decimal.TryParse(quantity.Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value)
            || value <= 0 || value != decimal.Truncate(value))
        {
            return (0, null, $"The order quantity '{quantity.Trim()}' is not a whole number.");
        }

        if (isMl)
        {
            return value > BloodBankLIS.Domain.Rules.RequestedVolumeRule.MaxMl
                ? (0, null, $"The order asks for {value} mL; at most {BloodBankLIS.Domain.Rules.RequestedVolumeRule.MaxMl} mL can be ordered.")
                : (1, (int)value, null);
        }

        if (unitText.Length > 0 && unitText is not ("U" or "UNIT" or "UNITS" or "EA" or "EACH" or "BAG" or "BAGS"))
        {
            return (0, null, $"The order quantity units '{units!.Trim()}' are not units or mL.");
        }

        return value > MaxUnits
            ? (0, null, $"The order asks for {value} units; at most {MaxUnits} can be ordered at once.")
            : ((int)value, null, null);
    }
}
