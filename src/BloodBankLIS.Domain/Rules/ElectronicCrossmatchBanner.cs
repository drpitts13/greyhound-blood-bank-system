namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Chart-header copy for an electronic crossmatch that is already not eligible.
/// Shows the existing blocking reason. Does not decide eligibility.
/// </summary>
public static class ElectronicCrossmatchBanner
{
    public static string? Notice(bool eligible, string? blockingReason)
    {
        if (eligible || string.IsNullOrWhiteSpace(blockingReason))
        {
            return null;
        }

        return blockingReason.Trim();
    }

    /// <summary>
    /// The chart header warns only about this patient: when the facility has electronic crossmatch switched off it is
    /// off for every patient, so a warning on every chart would only teach staff to skip warnings. The Overview's
    /// criteria list still says why.
    /// </summary>
    public static string? HeaderNotice(bool eligible, bool facilityAllows, string? blockingReason) =>
        facilityAllows ? Notice(eligible, blockingReason) : null;

    /// <summary>"Off at this facility", "Eligible", or "Not eligible".</summary>
    public static string BadgeText(bool eligible, bool facilityAllows) =>
        !facilityAllows ? "Off at this facility" : eligible ? "Eligible" : "Not eligible";
}
