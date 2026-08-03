namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// PLACEHOLDER — NOT FOR PRODUCTION CLINICAL USE.
/// Uses a deterministic facility-local algorithm so manual-entry workflows and tests
/// can exercise mismatch/exception paths without inventing official ICCBBA vectors.
/// ICCBBA_VALIDATION_REQUIRED: substitute with the official ISBT 128 DIN keyboard
/// check-character algorithm and verified test vectors before go-live.
/// MEDICAL_DIRECTOR_APPROVAL: required before enabling in a clinical environment.
/// </summary>
public sealed class PlaceholderDinCheckCharacterValidator : IDinCheckCharacterValidator
{
    // ISO/IEC 7064 MOD 37-2 character set commonly referenced for DIN check chars.
    // This is a structural placeholder — verify against current ICCBBA documentation.
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
