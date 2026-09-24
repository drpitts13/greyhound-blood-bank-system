using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Results;

/// <summary>Names the cell reactions a crossmatch test defines as required but the entry omitted.</summary>
public static class CrossmatchReactionValidation
{
    public static IReadOnlyList<string> MissingRequired(
        IEnumerable<ResolvedPanelSubtestDto>? subtests,
        IReadOnlyDictionary<string, string>? values)
    {
        var provided = values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        foreach (var subtest in subtests ?? [])
        {
            if (!subtest.Required)
            {
                continue;
            }

            if (subtest.Phases is { Count: > 0 })
            {
                foreach (var phase in subtest.Phases.Where(p => p.Required))
                {
                    var key = PhaseResultKeys.Compose(subtest.SubtestCode, phase.PhaseCode);
                    if (!HasValue(provided, key))
                    {
                        missing.Add($"{subtest.Label} {phase.Label}".Trim());
                    }
                }

                continue;
            }

            if (!HasValue(provided, subtest.SubtestCode))
            {
                missing.Add(string.IsNullOrWhiteSpace(subtest.Label) ? subtest.SubtestCode : subtest.Label);
            }
        }

        return missing;
    }

    private static bool HasValue(IReadOnlyDictionary<string, string> provided, string key) =>
        provided.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
}
