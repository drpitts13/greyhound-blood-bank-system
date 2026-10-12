using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Collection, receipt, and acceptance are distinct statuses. Result entry and
/// order linking still require Accepted. An expired tube is rejected, not received.
/// </summary>
public static class SpecimenReceiptRule
{
    public const string ReceiveStatusCode = "SPEC-RCV-STATUS";
    public const string ReceiveExpiredCode = "SPEC-RCV-EXPIRED";
    public const string AcceptStatusCode = "SPEC-ACCEPT-STATUS";
    public const string AcceptExpiredCode = "SPEC-ACCEPT-EXPIRED";

    public static RuleResult EvaluateReceive(SpecimenStatus status, DateTime? expiresUtc, DateTime now)
    {
        if (status != SpecimenStatus.Collected)
        {
            return RuleResult.HardStop(
                ReceiveStatusCode,
                $"A specimen with status {status} cannot be received. Only a Collected specimen can be received into the blood bank.");
        }

        if (expiresUtc is DateTime expires && expires <= now)
        {
            return RuleResult.HardStop(
                ReceiveExpiredCode,
                "An expired specimen cannot be received. Reject it instead.");
        }

        return RuleResult.Pass(ReceiveStatusCode);
    }

    public static RuleResult EvaluateAccept(SpecimenStatus status, DateTime? expiresUtc, DateTime now)
    {
        if (status != SpecimenStatus.Received)
        {
            return RuleResult.HardStop(
                AcceptStatusCode,
                $"A specimen with status {status} cannot be accepted. Receive it first.");
        }

        if (expiresUtc is DateTime expires && expires <= now)
        {
            return RuleResult.HardStop(
                AcceptExpiredCode,
                "An expired specimen cannot be accepted. Reject it instead.");
        }

        return RuleResult.Pass(AcceptStatusCode);
    }

    public static bool IsPendingReceipt(SpecimenStatus status) =>
        status is SpecimenStatus.Collected or SpecimenStatus.Received;
}
