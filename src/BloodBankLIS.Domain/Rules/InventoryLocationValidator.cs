using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

public static class InventoryLocationValidator
{
    public static RuleEvaluation Validate(InventoryLocation location, bool duplicateCode)
    {
        ArgumentNullException.ThrowIfNull(location);
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(location.Code))
        {
            results.Add(RuleResult.HardStop("INVLOC.CODE.REQUIRED", "Location code is required."));
        }

        if (string.IsNullOrWhiteSpace(location.Name))
        {
            results.Add(RuleResult.HardStop("INVLOC.NAME.REQUIRED", "Location name is required."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop("INVLOC.CODE.DUPLICATE", $"Another inventory location already uses '{location.Code}'."));
        }

        if (location.StorageTempMinC is { } min && location.StorageTempMaxC is { } max && min > max)
        {
            results.Add(RuleResult.HardStop("INVLOC.TEMP.RANGE", "Storage temperature minimum cannot exceed the maximum."));
        }

        if (location.DefaultInTransitHours is < 0)
        {
            results.Add(RuleResult.HardStop("INVLOC.TRANSIT.NEGATIVE", "In-transit due hours cannot be negative."));
        }

        if (!location.AllowsIssue && !location.AllowsRemoteIssue && location.IsActive)
        {
            results.Add(RuleResult.Warning(
                "INVLOC.NO-ISSUE",
                "Location is active but neither issue nor remote issue is enabled. Units stored here cannot be issued until an issue point is configured."));
        }

        return new RuleEvaluation(results);
    }
}
