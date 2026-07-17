using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// Full ABORH panel with forward/reverse subtests and the interpreted ABO/Rh type.
/// </summary>
public sealed record AboRhPanelResult(
    AboGroup Abo,
    RhType Rh,
    IReadOnlyDictionary<string, string> Subtests)
{
    public AboRh InterpretedType => new(Abo, Rh);

    public string? GetSubtest(string code) =>
        Subtests.TryGetValue(code, out var grade) ? grade : null;
}
