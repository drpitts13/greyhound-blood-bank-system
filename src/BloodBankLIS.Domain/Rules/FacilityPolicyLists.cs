using System.Text.RegularExpressions;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Facility settings whose value is a list (2026-11-20: medical director decisions as facility settings). A list is
/// comma-separated; each list key says what may be in it, so a mistyped value is refused when it is saved and an
/// unreadable stored value falls back to the decided default.
/// </summary>
public static partial class FacilityPolicyLists
{
    /// <summary>OCD-019 as decided: final, correction of an unreleased result, preliminary, results entered not verified.</summary>
    public const string DefaultInboundResultStatuses = "F,C,P,R";

    /// <summary>OCD-020 as decided.</summary>
    public const string DefaultDosageSensitiveAntigens = "C,c,E,e,FYA,FYB,JKA,JKB,M,N,S,s";

    /// <summary>OCD-024 as decided.</summary>
    public const string DefaultGenotypeMethodWords = "genotype,molecular,predicted,dna,pcr";

    /// <summary>OCD-061 as decided.</summary>
    public const string DefaultBatchVerifyTests = "ABORH,ABSC,DAT";

    /// <summary>OCD-065: the example scores the titer tests were built with.</summary>
    public const string DefaultTiterGradeScores = "0=0,w+=2,1+=5,2+=8,3+=10,4+=12";

    public static IReadOnlyList<string> Items(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static IReadOnlySet<string> Set(string? value, StringComparer comparer) =>
        new HashSet<string>(Items(value), comparer);

    /// <summary>grade=score pairs; an unreadable pair is left out (the value was checked when it was saved).</summary>
    public static IReadOnlyDictionary<string, int> Scores(string? value)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Items(value))
        {
            var parts = item.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && int.TryParse(parts[1], out var score))
            {
                scores[parts[0]] = score;
            }
        }

        return scores;
    }

    [GeneratedRegex("^[A-Z]$")]
    private static partial Regex StatusLetter();

    [GeneratedRegex("^[A-Za-z0-9]{1,10}$")]
    private static partial Regex AntigenCode();

    /// <summary>What is wrong with a list value for its key, or null when it may be saved.</summary>
    public static string? Problem(string key, string? value)
    {
        var items = Items(value);
        switch (key)
        {
            case FacilityPolicyKeys.InboundResultStatuses:
                return items.FirstOrDefault(i => !StatusLetter().IsMatch(i)) is { } badStatus
                    ? $"'{badStatus}' is not an OBX-11 status; give single capital letters separated by commas (for example F,C,P,R)."
                    : null;
            case FacilityPolicyKeys.AntibodyIdDosageSensitiveAntigens:
                return items.FirstOrDefault(i => !AntigenCode().IsMatch(i)) is { } badAntigen
                    ? $"'{badAntigen}' is not an antigen code; give codes such as C, c, FYA, JKB separated by commas."
                    : null;
            case FacilityPolicyKeys.AntibodyIdGenotypeMethodWords:
                return items.Count == 0
                    ? "Give at least one word (for example genotype)."
                    : items.FirstOrDefault(i => i.Length > 40) is { } longWord ? $"'{longWord}' is longer than 40 characters." : null;
            case FacilityPolicyKeys.BatchVerifyTests:
                return items.FirstOrDefault(i => !BatchVerifyRule.Tests.Contains(i)) is { } badTest
                    ? $"'{badTest}' cannot be verified in a batch; choose from ABORH, ABSC and DAT."
                    : null;
            case FacilityPolicyKeys.TiterGradeScores:
                foreach (var item in items)
                {
                    var parts = item.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length != 2 || !TiterRule.Grades.Contains(parts[0], StringComparer.OrdinalIgnoreCase)
                        || !int.TryParse(parts[1], out var score) || score is < 0 or > 20)
                    {
                        return $"'{item}' is not grade=score with a grade of {string.Join(", ", TiterRule.Grades)} and a score from 0 to 20.";
                    }
                }

                return items.Count == 0 ? "Give a score for each grade (for example 0=0,w+=2,1+=5,2+=8,3+=10,4+=12)." : null;
            default:
                return null;
        }
    }
}
