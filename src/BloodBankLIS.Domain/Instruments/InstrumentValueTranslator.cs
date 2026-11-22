using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Instruments;

/// <summary>
/// Applies one instrument's translation rows. Lookups ignore case; a row limited to the result's test wins over a row
/// for every test; then the lower sort order. When one instrument code maps to several tests, a test ordered on the
/// specimen wins. Values with no translation are collected as misses for the unmapped list.
/// </summary>
public sealed class InstrumentValueTranslator
{
    public sealed record Miss(string DataItemKey, string Value, InterfaceTranslationDirection Direction);

    /// <summary>A translation and the row it came from (null when nothing matched).</summary>
    public sealed record Outcome(string Value, bool Translated, InstrumentValueTranslation? Row);

    private readonly List<InstrumentValueTranslation> _rows;
    private readonly List<Miss> _misses = new();

    private InstrumentValueTranslator(IEnumerable<InstrumentValueTranslation> rows) =>
        _rows = rows.Where(r => !string.IsNullOrWhiteSpace(r.InstrumentValue) && !string.IsNullOrWhiteSpace(r.LocalValue)).ToList();

    public static InstrumentValueTranslator From(IEnumerable<InstrumentValueTranslation>? rows) => new(rows ?? []);

    public IReadOnlyList<Miss> Misses => _misses;

    public bool HasRows(string key) => _rows.Any(r => r.DataItemKey == key);

    /// <summary>Inbound: the LIS value for an instrument value, or null.</summary>
    public string? ToLocal(string key, string? instrumentValue, string? context = null, IReadOnlyCollection<string>? orderedTests = null) =>
        Inbound(key, instrumentValue, context, orderedTests).Row?.LocalValue;

    /// <summary>Outbound: the instrument value for an LIS value, or null.</summary>
    public string? ToInstrument(string key, string? localValue, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(localValue))
        {
            return null;
        }

        return Ranked(key, InterfaceTranslationDirection.Outbound, context)
            .FirstOrDefault(r => Same(r.LocalValue, localValue))?.InstrumentValue;
    }

    public Outcome Inbound(string key, string? instrumentValue, string? context = null, IReadOnlyCollection<string>? orderedTests = null)
    {
        var value = instrumentValue?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return new Outcome(value, false, null);
        }

        var matches = Ranked(key, InterfaceTranslationDirection.Inbound, context)
            .Where(r => Same(r.InstrumentValue, value))
            .ToList();
        if (matches.Count == 0)
        {
            return new Outcome(value, false, null);
        }

        var row = orderedTests is { Count: > 0 }
            ? matches.FirstOrDefault(m => orderedTests.Contains(m.LocalValue, StringComparer.OrdinalIgnoreCase)) ?? matches[0]
            : matches[0];
        return new Outcome(row.LocalValue, true, row);
    }

    /// <summary>
    /// The action for an instrument error or flag code. A code with no translation is held, so an unknown flag never
    /// posts unseen (OCD-066), and is recorded as a miss.
    /// </summary>
    public (InstrumentErrorAction Action, bool Translated) ErrorAction(string code, string? context = null)
    {
        var outcome = Inbound(InstrumentTranslationKeys.ErrorCode, code, context);
        if (outcome.Translated && Enum.TryParse<InstrumentErrorAction>(outcome.Value, ignoreCase: true, out var action))
        {
            return (action, true);
        }

        RecordMiss(InstrumentTranslationKeys.ErrorCode, code, InterfaceTranslationDirection.Inbound);
        return (InstrumentErrorAction.Hold, false);
    }

    /// <summary>
    /// An error code as staff should read it: the code with its meaning from the instrument's error code table when
    /// one is recorded ("Q (Questionable reaction)"), else the code alone.
    /// </summary>
    public string? Meaning(string code, string? context = null) =>
        Ranked(InstrumentTranslationKeys.ErrorCode, InterfaceTranslationDirection.Inbound, context)
            .FirstOrDefault(r => Same(r.InstrumentValue, code))?.Description?.Trim() is { Length: > 0 } meaning ? meaning : null;

    public string Describe(string code, string? context = null)
    {
        var row = Ranked(InstrumentTranslationKeys.ErrorCode, InterfaceTranslationDirection.Inbound, context)
            .FirstOrDefault(r => Same(r.InstrumentValue, code));
        return string.IsNullOrWhiteSpace(row?.Description) ? code.Trim() : $"{row.InstrumentValue.Trim()} ({row.Description.Trim()})";
    }

    public void RecordMiss(string key, string? value, InterfaceTranslationDirection direction)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v) || _misses.Any(m => m.DataItemKey == key && m.Direction == direction && Same(m.Value, v)))
        {
            return;
        }

        _misses.Add(new Miss(key, v, direction));
    }

    private IEnumerable<InstrumentValueTranslation> Ranked(string key, InterfaceTranslationDirection direction, string? context) =>
        _rows
            .Where(r => r.DataItemKey == key
                && (r.Direction == InterfaceTranslationDirection.Both || r.Direction == direction)
                && (string.IsNullOrWhiteSpace(r.Context) || Same(r.Context, context)))
            .OrderBy(r => string.IsNullOrWhiteSpace(r.Context) ? 1 : 0)
            .ThenBy(r => r.SortOrder)
            .ThenBy(r => r.Id);

    private static bool Same(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
