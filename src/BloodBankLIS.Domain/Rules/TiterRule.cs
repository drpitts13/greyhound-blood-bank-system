using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>How a test's titer is set up: the dilution series, the phase, the endpoint grade, and scoring.</summary>
public sealed record TiterSettings(int StartDilution, int EndDilution, string? PhaseCode, string EndpointMinGrade, bool Scored)
{
    public static TiterSettings? From(TestDefinition d) =>
        d.TiterStartDilution is int start && d.TiterEndDilution is int end && !string.IsNullOrWhiteSpace(d.TiterEndpointMinGrade)
            ? new(start, end, d.TiterPhaseCode, d.TiterEndpointMinGrade.Trim(), d.TiterScored)
            : null;
}

/// <summary>A checked titer: the value to store (null when refused), the entry outcome, and any pattern warning.</summary>
public sealed record TiterEvaluation(TiterResultValue? Value, RuleResult Result, RuleResult? Pattern);

/// <summary>
/// Titers as a doubling dilution series (1:1, 1:2 … 1:2048). The endpoint is the highest dilution whose grade reaches
/// the test's endpoint grade; the score sums a value per grade. The endpoint grade, the score values, and what change
/// between titers matters are the facility's decisions (OCD-065); this rule only applies what is configured.
/// </summary>
public static class TiterRule
{
    public const string ConfigCode = "TITER-CONFIG";
    public const string GradeCode = "TITER-GRADE";
    public const string MissingCode = "TITER-MISSING";
    public const string PatternCode = "TITER-PATTERN";
    public const int MaxSteps = 13;

    /// <summary>Grades a dilution can be read as, weakest first; NT marks a dilution not tested past the endpoint.</summary>
    public static IReadOnlyList<string> Grades { get; } = ["0", "w+", "1+", "2+", "3+", "4+"];

    /// <summary>
    /// Example score values per grade (AABB technical manual convention), seeded for review under OCD-065.
    /// </summary>
    public static IReadOnlyDictionary<string, int> ExampleScores { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = 0, ["w+"] = 2, ["1+"] = 5, ["2+"] = 8, ["3+"] = 10, ["4+"] = 12
    };

    /// <summary>The dilutions from start to end, doubling: 1, 2, 4 … end. Empty when the series is not a doubling one.</summary>
    public static IReadOnlyList<int> Dilutions(int start, int end)
    {
        if (!IsValidSeries(start, end))
        {
            return [];
        }

        var list = new List<int>();
        for (var d = start; d <= end; d *= 2)
        {
            list.Add(d);
        }

        return list;
    }

    /// <summary>Start of at least 1, and an end that is the start doubled 1 to 13 times.</summary>
    public static bool IsValidSeries(int start, int end)
    {
        if (start < 1 || end <= start || end % start != 0)
        {
            return false;
        }

        var ratio = end / start;
        var steps = 0;
        while (ratio > 1 && ratio % 2 == 0)
        {
            ratio /= 2;
            steps++;
        }

        return ratio == 1 && steps <= MaxSteps;
    }

    /// <summary>The grade's strength, weakest 0; -1 for anything that is not a titer grade.</summary>
    public static int Strength(string? grade)
    {
        var normalized = CellReactionGradeValue.NormalizeDisplay(grade);
        for (var i = 0; i < Grades.Count; i++)
        {
            if (string.Equals(Grades[i], normalized, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public static TiterEvaluation Evaluate(TiterSettings settings, IEnumerable<TiterReading>? readings)
    {
        var dilutions = Dilutions(settings.StartDilution, settings.EndDilution);
        var minStrength = Strength(settings.EndpointMinGrade);
        if (dilutions.Count == 0 || minStrength <= 0)
        {
            return new(null, RuleResult.HardStop(ConfigCode,
                "This titer test is not set up: it needs a doubling dilution series and a positive endpoint grade. Ask an administrator."), null);
        }

        var byDilution = new Dictionary<int, string>();
        foreach (var reading in readings ?? [])
        {
            if (!dilutions.Contains(reading.Dilution))
            {
                return new(null, RuleResult.HardStop(GradeCode, $"1:{reading.Dilution} is not one of this test's dilutions."), null);
            }

            var grade = CellReactionGradeValue.NormalizeDisplay(reading.Grade);
            if (string.IsNullOrWhiteSpace(grade) || string.Equals(grade, "NT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Strength(grade) < 0)
            {
                return new(null, RuleResult.HardStop(GradeCode, $"\"{reading.Grade}\" at 1:{reading.Dilution} is not a titer grade ({string.Join(", ", Grades)})."), null);
            }

            byDilution[reading.Dilution] = Grades[Strength(grade)];
        }

        // Every dilution must be read until the first negative one (or to the end when none is negative). Anything read
        // after that is kept, so a reaction past a negative (prozone or an entry slip) is seen.
        var stored = new List<TiterReading>();
        var negativeSeen = false;
        foreach (var dilution in dilutions)
        {
            if (!byDilution.TryGetValue(dilution, out var grade))
            {
                if (negativeSeen)
                {
                    continue;
                }

                return new(null, RuleResult.HardStop(MissingCode,
                    $"Enter the reaction at 1:{dilution}. Read each dilution until the first negative one."), null);
            }

            stored.Add(new TiterReading(dilution, grade));
            negativeSeen |= Strength(grade) == 0;
        }

        RuleResult? pattern = null;
        var firstNegative = stored.FirstOrDefault(r => Strength(r.Grade) == 0);
        if (firstNegative is not null && stored.Any(r => r.Dilution > firstNegative.Dilution && Strength(r.Grade) > 0))
        {
            pattern = RuleResult.Warning(PatternCode,
                $"A reaction is recorded after the negative 1:{firstNegative.Dilution}. Check for a prozone or an entry error before saving.");
        }

        var endpoint = stored.Where(r => Strength(r.Grade) >= minStrength).Select(r => (int?)r.Dilution).DefaultIfEmpty(null).Max();
        int? score = settings.Scored ? stored.Sum(r => ExampleScores.GetValueOrDefault(r.Grade, 0)) : null;
        var value = new TiterResultValue(settings.PhaseCode, settings.StartDilution, stored, endpoint, score);
        return new(value, RuleResult.Pass(GradeCode), pattern);
    }

    /// <summary>
    /// Change from the previous titer in dilution steps (+2 = two dilutions higher). A titer with no endpoint counts as
    /// one step below its start. Information only: what change is significant is the facility's (OCD-065).
    /// </summary>
    public static int CompareToPrevious(TiterResultValue previous, TiterResultValue current) =>
        Steps(current) - Steps(previous);

    /// <summary>"2 dilutions higher than 1:8 on 2026-10-01", "same as …", or "1 dilution lower than …".</summary>
    public static string DescribeChange(TiterResultValue previous, DateTime previousUtc, TiterResultValue current)
    {
        var steps = CompareToPrevious(previous, current);
        var since = $"{previous.EndpointLabel} on {previousUtc:yyyy-MM-dd}";
        return steps switch
        {
            0 => $"Same as {since}",
            > 0 => $"{steps} dilution{(steps == 1 ? "" : "s")} higher than {since}",
            _ => $"{-steps} dilution{(steps == -1 ? "" : "s")} lower than {since}"
        };
    }

    private static int Steps(TiterResultValue value)
    {
        var dilution = value.Endpoint ?? 0;
        var steps = 0;
        if (dilution == 0)
        {
            // Below the start: one step under the first dilution.
            dilution = value.StartDilution;
            steps = -1;
        }

        while (dilution > 1)
        {
            dilution /= 2;
            steps++;
        }

        return steps;
    }
}
