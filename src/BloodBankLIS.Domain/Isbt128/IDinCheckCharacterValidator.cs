namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// DIN keyboard check-character validation. Implementations must be independently
/// validated against official ICCBBA test vectors.
/// ICCBBA_VALIDATION_REQUIRED: replace placeholder implementation with facility-validated algorithm.
/// </summary>
public interface IDinCheckCharacterValidator
{
    /// <summary>Computes the expected keyboard check character for a 13-character DIN.</summary>
    char ComputeCheckCharacter(string din13);

    /// <summary>Returns true when the provided check matches the computed value.</summary>
    bool IsValid(string din13, char checkCharacter);
}
