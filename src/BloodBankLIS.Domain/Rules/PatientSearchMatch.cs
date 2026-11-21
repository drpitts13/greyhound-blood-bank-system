using System.Globalization;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Word-by-word patient matching (end-user testing passes 99 and 100): "Kline, Nora" as written on a requisition, or a
/// name with a date of birth, "nora 1966-09-03". Every word must match the MRN, a name, a former name or the date of
/// birth (yyyy-MM-dd, M/d/yyyy, MM/dd/yyyy, yyyyMMdd, or the year).
/// </summary>
public static class PatientSearchMatch
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy", "yyyyMMdd"];

    public static IReadOnlyList<string> Words(string? text) =>
        (text ?? string.Empty).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>True when the word is a date of birth or a year, not part of a name.</summary>
    public static bool IsDateWord(string word) =>
        (word.Length == 4 && int.TryParse(word, out _))
        || DateOnly.TryParseExact(word, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static bool MatchesWord(
        string word, string mrn, string lastName, string firstName, string? middleName, DateOnly dateOfBirth, IEnumerable<string>? otherNames = null) =>
        mrn.Contains(word, StringComparison.OrdinalIgnoreCase)
        || lastName.Contains(word, StringComparison.OrdinalIgnoreCase)
        || firstName.Contains(word, StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(middleName) && middleName.Contains(word, StringComparison.OrdinalIgnoreCase))
        || otherNames?.Any(n => n.Contains(word, StringComparison.OrdinalIgnoreCase)) == true
        || MatchesBirthDate(word, dateOfBirth);

    public static bool MatchesAll(
        IReadOnlyList<string> words, string mrn, string lastName, string firstName, string? middleName, DateOnly dateOfBirth, IEnumerable<string>? otherNames = null) =>
        words.Count > 0 && words.All(w => MatchesWord(w, mrn, lastName, firstName, middleName, dateOfBirth, otherNames));

    public static bool MatchesBirthDate(string word, DateOnly dateOfBirth)
    {
        if (word.Length == 4 && int.TryParse(word, out var year))
        {
            return dateOfBirth.Year == year;
        }

        return DateOnly.TryParseExact(word, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && date == dateOfBirth;
    }
}
