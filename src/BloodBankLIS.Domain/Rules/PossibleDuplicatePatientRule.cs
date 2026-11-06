namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A patient being registered may already exist under another medical record number. Two charts for one person split
/// the ABO/Rh and antibody history the issue gate relies on, so registration names any active patient with the same
/// date of birth and the same last name and first initial, or with first and last names swapped. A match is a prompt to
/// open the existing chart, not a proof: the user may confirm it is a different person, and that is recorded.
/// </summary>
public static class PossibleDuplicatePatientRule
{
    public const string Code = "PAT-POSSIBLE-DUP";

    public static bool IsPossibleDuplicate(
        string? lastName, string? firstName, DateOnly dateOfBirth,
        string? otherLastName, string? otherFirstName, DateOnly otherDateOfBirth)
    {
        if (dateOfBirth != otherDateOfBirth)
        {
            return false;
        }

        var (last, first) = (Normalize(lastName), Normalize(firstName));
        var (otherLast, otherFirst) = (Normalize(otherLastName), Normalize(otherFirstName));
        if (last.Length == 0 || first.Length == 0 || otherLast.Length == 0 || otherFirst.Length == 0)
        {
            return false;
        }

        var sameLastAndInitial = last == otherLast && first[0] == otherFirst[0];
        var swapped = last == otherFirst && first == otherLast;
        return sameLastAndInitial || swapped;
    }

    /// <summary>Letters only, upper case: "O'Neil-Smith" and "ONEIL SMITH" compare equal.</summary>
    public static string Normalize(string? name) =>
        new((name ?? string.Empty).Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());

    public static string Message(IEnumerable<string> matches) =>
        $"This may be a patient who is already registered: {string.Join("; ", matches)}. Open that chart, or confirm this is a different person.";
}
