namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>Canonical subtest codes for an ABORH typing panel.</summary>
public static class AboRhPanelSubtestCodes
{
    public const string AntiA = "Anti-A";
    public const string AntiB = "Anti-B";
    public const string AntiD = "Anti-D";
    public const string ACells = "A-Cells";
    public const string BCells = "B-Cells";
    public const string Control = "Control";
    public const string WeakD = "Weak-D";

    public static readonly IReadOnlyList<string> Required =
    [
        AntiA, AntiB, AntiD, ACells, BCells
    ];

    public static readonly IReadOnlyList<string> Optional =
    [
        Control, WeakD
    ];

    public static readonly IReadOnlyList<string> All = Required.Concat(Optional).ToArray();
}
