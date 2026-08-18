namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// Obsolete alias kept so existing tests compile during the ICCBBA algorithm cutover.
/// Use <see cref="Iso7064Mod37_2DinCheckCharacterValidator"/>.
/// </summary>
[Obsolete("Use Iso7064Mod37_2DinCheckCharacterValidator (ISO/IEC 7064 MOD 37-2).")]
public sealed class PlaceholderDinCheckCharacterValidator : IDinCheckCharacterValidator
{
    private readonly Iso7064Mod37_2DinCheckCharacterValidator _inner = new();

    public char ComputeCheckCharacter(string din13) => _inner.ComputeCheckCharacter(din13);

    public bool IsValid(string din13, char checkCharacter) => _inner.IsValid(din13, checkCharacter);
}
