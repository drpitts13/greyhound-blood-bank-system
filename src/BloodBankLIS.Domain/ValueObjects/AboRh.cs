using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// Immutable ABO group + Rh(D) pairing. Used by patients and units. Compatibility
/// evaluation lives in the rule engine, not on this value object.
/// </summary>
public readonly record struct AboRh(AboGroup Abo, RhType Rh)
{
    public bool IsKnown => Abo != AboGroup.Unknown && Rh != RhType.Unknown;

    public override string ToString()
    {
        var abo = Abo == AboGroup.Unknown ? "?" : Abo.ToString();
        var rh = Rh switch
        {
            RhType.Positive => "+",
            RhType.Negative => "-",
            _ => "?"
        };
        return $"{abo}{rh}";
    }
}
