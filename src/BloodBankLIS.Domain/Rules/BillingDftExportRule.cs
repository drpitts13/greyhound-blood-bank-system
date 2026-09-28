using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Export removes a charge from the review queue. That stays closed while the
/// charge's DFT has not been accepted, so staff do not treat an unsent or
/// rejected DFT as billed.
/// </summary>
public static class BillingDftExportRule
{
    public const string Code = "BILL-DFT-UNACCEPTED";

    public const string Message =
        "The DFT for this charge has not been accepted. Export stays on the queue until that message is sent.";

    public static RuleResult Evaluate(Hl7MessageStatus? status)
    {
        if (status is null || status == Hl7MessageStatus.Acked)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(Code, Message);
    }
}
