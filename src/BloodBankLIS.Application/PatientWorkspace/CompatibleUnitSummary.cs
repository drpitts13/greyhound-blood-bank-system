namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// The note above the unit picker when units are offered for a patient: how many units are compatible, and of which
/// products. End-user testing, 2026-11-12: a patient needing irradiated, CMV-negative red cells was told "6 available
/// units are compatible"; all six were plasma, and nothing said that no red cell qualified. End-user testing pass 91: a
/// patient with anti-K was told six red cells were compatible "with this patient's antibodies" when none was typed for K;
/// units not confirmed antigen-negative are now counted apart.
/// </summary>
public static class CompatibleUnitSummary
{
    public static string Note(IReadOnlyList<CompatibleUnitDto> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        var confirmed = units.Where(u => string.IsNullOrWhiteSpace(u.AntigenNegFlag)).ToList();
        var unconfirmed = units.Where(u => !string.IsNullOrWhiteSpace(u.AntigenNegFlag)).ToList();

        var head = confirmed.Count switch
        {
            0 => unconfirmed.Count == 0
                ? "No available unit is compatible with this patient's type, special requirements, and antibodies."
                : "No available unit is confirmed compatible with this patient's antibodies.",
            1 => $"1 available unit is compatible with this patient's type, special requirements, and antibodies: {Products(confirmed)}.",
            _ => $"{confirmed.Count} available units are compatible with this patient's type, special requirements, and antibodies: {Products(confirmed)}."
        };
        if (unconfirmed.Count == 0)
        {
            return head;
        }

        var untested = unconfirmed.Count(u => u.AntigenNegFlag is BloodBankLIS.Domain.Rules.BloodAttributeCompatibilityRule.UntestedFlag
            or BloodBankLIS.Domain.Rules.BloodAttributeCompatibilityRule.MixedFlag);
        var positive = unconfirmed.Count - untested;
        var why = (untested, positive) switch
        {
            (> 0, 0) => "not tested",
            (0, > 0) => "antigen-positive",
            _ => $"{untested} not tested, {positive} antigen-positive"
        };
        return $"{head} {unconfirmed.Count} more {(unconfirmed.Count == 1 ? "matches" : "match")} the type and special requirements but "
            + $"{(unconfirmed.Count == 1 ? "is" : "are")} not confirmed antigen-negative for the patient's antibodies ({why}): {Products(unconfirmed)}. "
            + "Type them first, or reserve one only with an authorized override.";
    }

    private static string Products(IEnumerable<CompatibleUnitDto> units) => string.Join(", ", units
        .GroupBy(u => string.IsNullOrWhiteSpace(u.ProductName) ? u.ProductCode : u.ProductName.Trim(), StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(g => g.Count())
        .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
        .Select(g => $"{g.Count()} {g.Key}"));
}
