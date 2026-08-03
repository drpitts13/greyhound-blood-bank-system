using System.Globalization;

namespace BloodBankLIS.Domain.Rules.Engine;

public enum RuleValueKind
{
    Null = 0,
    Boolean = 1,
    Number = 2,
    Text = 3,
    Date = 4,
    List = 5
}

/// <summary>
/// A single value flowing through rule expression evaluation. Comparison follows the
/// conventions already used elsewhere in the LIS: text is trimmed and compared
/// case-insensitively, and mismatched kinds coerce rather than throw so a malformed
/// rule degrades to "did not match" instead of failing a clinical workflow.
/// </summary>
public sealed class RuleValue
{
    private readonly object? _raw;

    private RuleValue(RuleValueKind kind, object? raw)
    {
        Kind = kind;
        _raw = raw;
    }

    public static RuleValue Null { get; } = new(RuleValueKind.Null, null);

    public static RuleValue True { get; } = new(RuleValueKind.Boolean, true);

    public static RuleValue False { get; } = new(RuleValueKind.Boolean, false);

    public RuleValueKind Kind { get; }

    public bool IsNull => Kind == RuleValueKind.Null;

    public static RuleValue FromBoolean(bool value) => value ? True : False;

    public static RuleValue FromNumber(decimal value) => new(RuleValueKind.Number, value);

    public static RuleValue FromNumber(int value) => new(RuleValueKind.Number, (decimal)value);

    public static RuleValue FromText(string? value) =>
        value is null ? Null : new RuleValue(RuleValueKind.Text, value);

    public static RuleValue FromDate(DateTime value) => new(RuleValueKind.Date, value);

    public static RuleValue FromDate(DateTime? value) => value is null ? Null : FromDate(value.Value);

    public static RuleValue FromEnum<TEnum>(TEnum value) where TEnum : struct, Enum =>
        FromText(value.ToString());

    public static RuleValue FromList(IEnumerable<RuleValue> items) =>
        new(RuleValueKind.List, items?.ToList() ?? new List<RuleValue>());

    public static RuleValue FromTextList(IEnumerable<string?> items) =>
        FromList((items ?? Array.Empty<string?>()).Select(FromText));

    public IReadOnlyList<RuleValue> AsList() =>
        _raw as List<RuleValue> ?? (IReadOnlyList<RuleValue>)Array.Empty<RuleValue>();

    public string? AsText() => Kind switch
    {
        RuleValueKind.Null => null,
        RuleValueKind.Text => (string)_raw!,
        RuleValueKind.Boolean => ((bool)_raw!) ? "true" : "false",
        RuleValueKind.Number => ((decimal)_raw!).ToString(CultureInfo.InvariantCulture),
        RuleValueKind.Date => ((DateTime)_raw!).ToString("O", CultureInfo.InvariantCulture),
        _ => null
    };

    /// <summary>Truthiness for a bare expression used as a condition, e.g. <c>order.hasTest('TS')</c>.</summary>
    public bool AsBoolean() => Kind switch
    {
        RuleValueKind.Boolean => (bool)_raw!,
        RuleValueKind.Number => (decimal)_raw! != 0m,
        RuleValueKind.Text => !string.IsNullOrWhiteSpace((string)_raw!)
                              && !string.Equals(((string)_raw!).Trim(), "false", StringComparison.OrdinalIgnoreCase),
        RuleValueKind.Date => true,
        RuleValueKind.List => AsList().Count > 0,
        _ => false
    };

    public bool TryAsNumber(out decimal number)
    {
        switch (Kind)
        {
            case RuleValueKind.Number:
                number = (decimal)_raw!;
                return true;
            case RuleValueKind.Text:
                return decimal.TryParse(
                    ((string)_raw!).Trim(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out number);
            case RuleValueKind.Boolean:
                number = (bool)_raw! ? 1m : 0m;
                return true;
            default:
                number = 0m;
                return false;
        }
    }

    public bool TryAsDate(out DateTime date)
    {
        switch (Kind)
        {
            case RuleValueKind.Date:
                date = (DateTime)_raw!;
                return true;
            case RuleValueKind.Text:
                return DateTime.TryParse(
                    (string)_raw!,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out date);
            default:
                date = default;
                return false;
        }
    }

    public static bool AreEqual(RuleValue left, RuleValue right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.IsNull || right.IsNull)
        {
            return left.IsNull && right.IsNull;
        }

        if (left.Kind == RuleValueKind.List || right.Kind == RuleValueKind.List)
        {
            var leftItems = left.Kind == RuleValueKind.List ? left.AsList() : new[] { left };
            var rightItems = right.Kind == RuleValueKind.List ? right.AsList() : new[] { right };
            return leftItems.Count == rightItems.Count
                   && leftItems.Zip(rightItems).All(pair => AreEqual(pair.First, pair.Second));
        }

        if (left.Kind == RuleValueKind.Boolean || right.Kind == RuleValueKind.Boolean)
        {
            return left.AsBoolean() == right.AsBoolean();
        }

        if (left.Kind == RuleValueKind.Number && right.Kind == RuleValueKind.Number)
        {
            return (decimal)left._raw! == (decimal)right._raw!;
        }

        if (left.Kind == RuleValueKind.Date || right.Kind == RuleValueKind.Date)
        {
            return left.TryAsDate(out var leftDate)
                   && right.TryAsDate(out var rightDate)
                   && leftDate == rightDate;
        }

        // A numeric column compared against a quoted literal should still match.
        if ((left.Kind == RuleValueKind.Number || right.Kind == RuleValueKind.Number)
            && left.TryAsNumber(out var leftNumber)
            && right.TryAsNumber(out var rightNumber))
        {
            return leftNumber == rightNumber;
        }

        return string.Equals(
            (left.AsText() ?? string.Empty).Trim(),
            (right.AsText() ?? string.Empty).Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ordering comparison. Returns false when the values are not comparable.</summary>
    public static bool TryCompare(RuleValue left, RuleValue right, out int comparison)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        comparison = 0;
        if (left.IsNull || right.IsNull || left.Kind == RuleValueKind.List || right.Kind == RuleValueKind.List)
        {
            return false;
        }

        if (left.Kind == RuleValueKind.Date || right.Kind == RuleValueKind.Date)
        {
            if (left.TryAsDate(out var leftDate) && right.TryAsDate(out var rightDate))
            {
                comparison = leftDate.CompareTo(rightDate);
                return true;
            }

            return false;
        }

        if (left.TryAsNumber(out var leftNumber) && right.TryAsNumber(out var rightNumber))
        {
            comparison = leftNumber.CompareTo(rightNumber);
            return true;
        }

        comparison = string.Compare(
            (left.AsText() ?? string.Empty).Trim(),
            (right.AsText() ?? string.Empty).Trim(),
            StringComparison.OrdinalIgnoreCase);
        return true;
    }

    /// <summary>
    /// Membership for <c>IN</c>, and containment for <c>CONTAINS</c>: list membership when the
    /// container is a list, otherwise case-insensitive substring matching.
    /// </summary>
    public static bool Contains(RuleValue container, RuleValue item)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(item);

        if (container.Kind == RuleValueKind.List)
        {
            if (item.Kind == RuleValueKind.List)
            {
                return item.AsList().Any(candidate => Contains(container, candidate));
            }

            return container.AsList().Any(candidate => AreEqual(candidate, item));
        }

        if (container.IsNull || item.IsNull)
        {
            return false;
        }

        var haystack = (container.AsText() ?? string.Empty).Trim();
        var needle = (item.AsText() ?? string.Empty).Trim();
        return needle.Length > 0 && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() => Kind switch
    {
        RuleValueKind.Null => "null",
        RuleValueKind.List => $"[{string.Join(", ", AsList().Select(v => v.ToString()))}]",
        _ => AsText() ?? string.Empty
    };
}
