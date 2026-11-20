using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// On a unit's own row: its cooler's last check found it missing (end-user testing pass 312: W0001230009101, missing from
/// Demo-Massive, Morgan's MTP cooler when it was checked, read on the overdue-disposition board like the three units
/// packed with it, "not received on the ward").
/// </summary>
public static class CoolerMissingText
{
    public static string? For(OpenCoolerDto? cooler, string? unitNumber) =>
        cooler is { LastCheckedUtc: DateTime checkedUtc } && !string.IsNullOrWhiteSpace(unitNumber)
        && cooler.MissingUnits.Any(u => string.Equals(u.Trim(), unitNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            ? $"Missing from {cooler.CoolerId} when it was checked ({ClinicalTimeText.Local(checkedUtc)}): find the unit, or record it missing on its unit page."
            : null;
}
