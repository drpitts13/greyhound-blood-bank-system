using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The result values a reflex rule's trigger test can produce, for suggesting the trigger value and warning about one
/// that can never match (end-user testing loop 2 tick 11: the trigger value was free text, so "Postive" saved and never
/// fired). A reflex fires when a verified result's value or interpretation equals the trigger, ignoring case.
/// </summary>
public static class ReflexTriggerValues
{
    /// <summary>The test's allowed values (one per line, or separated by commas or semicolons) and interpretation keys.</summary>
    public static IReadOnlyList<string> Known(string? allowedResultValues, IEnumerable<string>? interpretationKeys)
    {
        var values = (allowedResultValues ?? string.Empty)
            .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // Loop 2 tick 20: an interpretation key is matched as the result service resolves it ("O|Positive" reads
            // "O Positive"), so it is suggested in that form.
            .Concat((interpretationKeys ?? []).Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => ResultInterpretation.Resolve(k.Trim(), null) ?? k.Trim()));
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// A warning when the trigger value is not one the test produces, or null when it is, when none is entered, or when
    /// the test lists no values (a free-text or numeric result can be anything).
    /// </summary>
    public static string? Warning(string? triggerValue, IReadOnlyList<string> known)
    {
        var value = triggerValue?.Trim();
        if (string.IsNullOrEmpty(value) || known.Count == 0 || known.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"No result of this test reads \"{value}\", so the rule would never fire. It produces: {string.Join(", ", known)}.";
    }
}
