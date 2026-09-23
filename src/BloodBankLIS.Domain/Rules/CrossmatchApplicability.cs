using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Plasma, platelets, cryoprecipitate, and other non-cellular products are issued
/// without a crossmatch. Red cells and whole blood always require one.
/// </summary>
public static class CrossmatchApplicability
{
    public static bool IsNonCellular(ComponentClass componentClass) =>
        componentClass is ComponentClass.Plasma
            or ComponentClass.Platelets
            or ComponentClass.Cryoprecipitate
            or ComponentClass.Other;

    public static bool RequiresCrossmatch(ComponentClass componentClass, bool productRequiresCrossmatch)
    {
        if (IsNonCellular(componentClass))
        {
            return false;
        }

        if (componentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood)
        {
            return true;
        }

        return productRequiresCrossmatch;
    }
}
