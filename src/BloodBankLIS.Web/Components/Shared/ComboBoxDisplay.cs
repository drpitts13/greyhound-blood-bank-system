namespace BloodBankLIS.Web.Components.Shared;

/// <summary>
/// What a combo box shows for a value set from code. The value can arrive before its list has loaded, or name a
/// record that is not in the list. That text is "unresolved": it is replaced as soon as the item appears, and it
/// is never committed back as a typed search, which could otherwise pick a different item that happens to match.
/// </summary>
public static class ComboBoxDisplay
{
    public readonly record struct Result(string Text, bool Resolved);

    public static Result Resolve<TItem, TValue>(
        TValue? value,
        IEnumerable<TItem> items,
        Func<TItem, TValue?> itemValue,
        Func<TItem, string> displayText,
        Func<TValue, string>? unresolvedText = null,
        bool customStringValue = false)
    {
        if (value is null)
        {
            return new(string.Empty, true);
        }

        if (customStringValue && value is string s)
        {
            return new(s, true);
        }

        foreach (var item in items)
        {
            if (EqualityComparer<TValue?>.Default.Equals(itemValue(item), value))
            {
                return new(displayText(item), true);
            }
        }

        return new(unresolvedText?.Invoke(value) ?? value.ToString() ?? string.Empty, false);
    }

    /// <summary>Resolve again when the value changed, or while it is still waiting for its item.</summary>
    public static bool ShouldResolve(bool valueChanged, bool lastResolved) => valueChanged || !lastResolved;
}
