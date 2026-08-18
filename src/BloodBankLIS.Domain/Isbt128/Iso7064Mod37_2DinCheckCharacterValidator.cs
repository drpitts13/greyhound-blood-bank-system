namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// ISBT 128 DIN keyboard check character using ISO/IEC 7064 MOD 37-2, the algorithm
/// specified by ICCBBA for the 13-character DIN. Product-code and ABO lookup tables
/// remain separately licensed from ICCBBA and are not included here.
/// </summary>
public sealed class Iso7064Mod37_2DinCheckCharacterValidator : IDinCheckCharacterValidator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ*";

    public char ComputeCheckCharacter(string din13)
    {
        if (string.IsNullOrEmpty(din13) || din13.Length != 13)
            throw new ArgumentException("DIN must be exactly 13 characters.", nameof(din13));

        var sum = 0;
        for (var i = 0; i < din13.Length; i++)
        {
            var c = char.ToUpperInvariant(din13[i]);
            var idx = Alphabet.IndexOf(c);
            if (idx < 0)
                idx = 0;
            sum = (sum + idx) * 2 % 37;
        }

        var checkIndex = (38 - sum % 37) % 37;
        return Alphabet[checkIndex];
    }

    public bool IsValid(string din13, char checkCharacter)
    {
        if (string.IsNullOrEmpty(din13) || din13.Length != 13)
            return false;

        var expected = ComputeCheckCharacter(din13);
        return char.ToUpperInvariant(checkCharacter) == expected;
    }
}
