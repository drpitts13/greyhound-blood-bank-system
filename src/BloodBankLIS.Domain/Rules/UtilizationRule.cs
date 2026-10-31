using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Blood utilization arithmetic. A crossmatched unit is a distinct patient and unit pair
/// with a compatible crossmatch (any method). A unit counts as transfused when a
/// transfusion was documented as completed or stopped after it started. The software
/// reports the ratios only; it does not supply a target or benchmark.
/// </summary>
public static class UtilizationRule
{
    public static bool CountsAsTransfused(TransfusionDisposition disposition) =>
        disposition is TransfusionDisposition.Completed or TransfusionDisposition.Stopped;

    /// <summary>Crossmatch-to-transfusion ratio, or null when nothing was transfused.</summary>
    public static decimal? CrossmatchToTransfusionRatio(int crossmatched, int transfused) =>
        transfused <= 0 ? null : Math.Round((decimal)crossmatched / transfused, 2, MidpointRounding.AwayFromZero);

    /// <summary>Share of a base count as a percentage, or null when the base is zero.</summary>
    public static decimal? Percent(int part, int whole) =>
        whole <= 0 ? null : Math.Round(100m * part / whole, 1, MidpointRounding.AwayFromZero);
}
