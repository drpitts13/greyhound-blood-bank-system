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

    /// <summary>
    /// End-user testing pass 342: whether a line is above the facility's C:T ratio target. Units crossmatched with none
    /// transfused are above any target; no target, or nothing crossmatched, is never above.
    /// </summary>
    public static bool AboveCtTarget(int crossmatched, int transfused, decimal? target) =>
        target is decimal t && t > 0 && crossmatched > 0
        && (transfused <= 0 || (decimal)crossmatched / transfused > t);

    /// <summary>The target from its setting in tenths, or null when not set.</summary>
    public static decimal? CtTargetFromTenths(int tenths) => tenths > 0 ? tenths / 10m : null;

    /// <summary>Share of a base count as a percentage, or null when the base is zero.</summary>
    public static decimal? Percent(int part, int whole) =>
        whole <= 0 ? null : Math.Round(100m * part / whole, 1, MidpointRounding.AwayFromZero);
}
