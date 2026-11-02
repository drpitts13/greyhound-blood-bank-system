using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The HL7 v2 observation result status (OBX-11, table 0085) for a result sent to the EHR, so a downstream system can
/// tell a first final result from a correction, and a result withdrawn as wrong:
/// F (final) for a verified result, C (corrected) for a verified later version, W (posted as wrong) for a result
/// that had been verified and was then invalidated. Anything else is not sent.
/// </summary>
public static class ResultReleaseRule
{
    public const string Final = "F";
    public const string Corrected = "C";
    public const string Wrong = "W";

    public static string? ObxStatus(ResultStatus status, int version, bool wasVerified) => status switch
    {
        ResultStatus.Verified => version > 1 ? Corrected : Final,
        ResultStatus.Invalidated when wasVerified => Wrong,
        _ => null
    };
}
