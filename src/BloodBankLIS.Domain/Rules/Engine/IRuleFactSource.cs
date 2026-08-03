namespace BloodBankLIS.Domain.Rules.Engine;

/// <summary>
/// Supplies attribute values to the expression evaluator. Implementations are built
/// per evaluation from already-loaded entities so the evaluator itself stays pure and
/// synchronous. Unknown paths return false rather than throwing.
/// </summary>
public interface IRuleFactSource
{
    bool TryGetAttribute(string path, out RuleValue value);

    bool TryInvoke(string function, IReadOnlyList<RuleValue> arguments, out RuleValue value);
}

/// <summary>
/// Dictionary-backed fact source. Attribute paths and function names are matched
/// case-insensitively.
/// </summary>
public sealed class RuleFactBag : IRuleFactSource
{
    private readonly Dictionary<string, RuleValue> _attributes = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Func<IReadOnlyList<RuleValue>, RuleValue>> _functions =
        new(StringComparer.OrdinalIgnoreCase);

    public RuleFactBag Set(string path, RuleValue value)
    {
        _attributes[path] = value;
        return this;
    }

    public RuleFactBag Set(string path, string? value) => Set(path, RuleValue.FromText(value));

    public RuleFactBag Set(string path, int value) => Set(path, RuleValue.FromNumber(value));

    public RuleFactBag Set(string path, DateTime? value) => Set(path, RuleValue.FromDate(value));

    public RuleFactBag SetList(string path, IEnumerable<string?> values) =>
        Set(path, RuleValue.FromTextList(values));

    public RuleFactBag SetFunction(string name, Func<IReadOnlyList<RuleValue>, RuleValue> implementation)
    {
        _functions[name] = implementation;
        return this;
    }

    /// <summary>Registers a single-argument function under its canonical name and every alias.</summary>
    public RuleFactBag SetFunction(
        IEnumerable<string> names,
        Func<IReadOnlyList<RuleValue>, RuleValue> implementation)
    {
        foreach (var name in names)
        {
            _functions[name] = implementation;
        }

        return this;
    }

    public bool TryGetAttribute(string path, out RuleValue value) =>
        _attributes.TryGetValue(path, out value!);

    public bool TryInvoke(string function, IReadOnlyList<RuleValue> arguments, out RuleValue value)
    {
        if (_functions.TryGetValue(function, out var implementation))
        {
            value = implementation(arguments) ?? RuleValue.Null;
            return true;
        }

        value = RuleValue.Null;
        return false;
    }
}
