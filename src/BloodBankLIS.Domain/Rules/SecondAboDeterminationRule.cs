using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// AABB computer-crossmatch requires two concordant ABO/Rh determinations, at least
/// one of which is current. Historical rows that match the current type satisfy this.
/// </summary>
public static class SecondAboDeterminationRule
{
    public const string Code = ElectronicCrossmatchEligibilityRule.Code;

    public sealed record Determination(AboRh BloodType, bool IsCurrent);

    public static bool HasSecondConcordant(IReadOnlyList<Determination> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var current = history.FirstOrDefault(h => h.IsCurrent && h.BloodType.IsKnown);
        if (current is null)
        {
            return false;
        }

        return history.Any(h =>
            !h.IsCurrent
            && h.BloodType.IsKnown
            && h.BloodType == current.BloodType);
    }
}
