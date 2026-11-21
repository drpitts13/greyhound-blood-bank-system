using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Where a modification's result may be stored (end-user testing loop 2 tick 19). A thawed or deglycerolized unit is
/// liquid again and cannot be recorded in a freezer; a frozen (glycerolized) unit must be recorded in one. Other types
/// keep the source's location or the one chosen.
/// </summary>
public static class ModificationResultStorageRule
{
    public static bool LeavesFreezer(ModificationType type) => type is ModificationType.Thaw or ModificationType.Deglycerolize;

    public static bool GoesToFreezer(ModificationType type) => type == ModificationType.Freeze;

    /// <summary>
    /// The problem with storing the result at a location of <paramref name="locationType"/> (labelled
    /// <paramref name="location"/>), or null when it is fine. <paramref name="chosen"/> says whether the technologist
    /// chose it or the result would stay where the source is.
    /// </summary>
    public static string? Problem(ModificationType type, LocationType locationType, string location, bool chosen)
    {
        var what = type switch
        {
            ModificationType.Thaw => "A thawed unit",
            ModificationType.Deglycerolize => "A deglycerolized unit",
            _ => "A frozen unit"
        };

        if (LeavesFreezer(type) && locationType == LocationType.Freezer)
        {
            return chosen
                ? $"{what} cannot go to {location}, a freezer. Choose a refrigerator."
                : $"{what} cannot stay in {location}, a freezer. Choose the result location (a refrigerator).";
        }

        if (GoesToFreezer(type) && locationType != LocationType.Freezer)
        {
            return chosen
                ? $"{what} must go to a freezer; {location} is not one. Choose a freezer."
                : $"{what} cannot stay in {location}, which is not a freezer. Choose the result location (a freezer).";
        }

        return null;
    }
}
