using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Which specimens a transfusion reaction workup may use. The pre-transfusion specimen was collected before the unit
/// was issued (the sample the unit was selected on); the post-transfusion specimen was collected after the transfusion
/// began. Both belong to the investigation's patient, and one tube cannot be both.
/// </summary>
public static class ReactionWorkupSpecimenRule
{
    public const string PreCode = "RXN-PRE-SPECIMEN";
    public const string PostCode = "RXN-POST-SPECIMEN";

    /// <summary>
    /// OCD-058: the pre-transfusion tube is usually past its routine expiry when a reaction is reported. Reaction workup
    /// orders may be linked to, and resulted on, an expired specimen; every other order keeps the expiry block.
    /// </summary>
    public static bool AllowsExpiredSpecimen(OrderType orderType) => orderType == OrderType.TransfusionReactionWorkup;

    public static RuleResult EvaluatePre(
        long investigationPatientId,
        long specimenPatientId,
        SpecimenStatus status,
        DateTime collectedUtc,
        DateTime issuedUtc,
        string accessionNumber)
    {
        var common = Common(PreCode, investigationPatientId, specimenPatientId, status, accessionNumber, allowExpired: true);
        if (common is not null)
        {
            return common;
        }

        return CrossmatchSpecimenRule.IsPreTransfusionSpecimen(collectedUtc, issuedUtc)
            ? RuleResult.Pass(PreCode)
            : RuleResult.HardStop(PreCode,
                $"Specimen {accessionNumber} was collected at {collectedUtc:yyyy-MM-dd HH:mm} UTC, after the unit was issued at {issuedUtc:yyyy-MM-dd HH:mm} UTC; it is not a pre-transfusion specimen.");
    }

    public static RuleResult EvaluatePost(
        long investigationPatientId,
        long specimenPatientId,
        SpecimenStatus status,
        DateTime collectedUtc,
        DateTime transfusionStartUtc,
        long specimenId,
        long? preTransfusionSpecimenId,
        string accessionNumber)
    {
        if (preTransfusionSpecimenId == specimenId)
        {
            return RuleResult.HardStop(PostCode,
                $"Specimen {accessionNumber} is already the pre-transfusion specimen; the post-transfusion specimen must be a new collection.");
        }

        var common = Common(PostCode, investigationPatientId, specimenPatientId, status, accessionNumber, allowExpired: false);
        if (common is not null)
        {
            return common;
        }

        return collectedUtc >= transfusionStartUtc
            ? RuleResult.Pass(PostCode)
            : RuleResult.HardStop(PostCode,
                $"Specimen {accessionNumber} was collected at {collectedUtc:yyyy-MM-dd HH:mm} UTC, before the transfusion began at {transfusionStartUtc:yyyy-MM-dd HH:mm} UTC; it is not a post-transfusion specimen.");
    }

    private static RuleResult? Common(
        string code, long investigationPatientId, long specimenPatientId, SpecimenStatus status, string accessionNumber, bool allowExpired)
    {
        if (specimenPatientId != investigationPatientId)
        {
            return RuleResult.HardStop(code, $"Specimen {accessionNumber} belongs to another patient.");
        }

        var usable = status == SpecimenStatus.Accepted || (allowExpired && status == SpecimenStatus.Expired);
        return usable
            ? null
            : RuleResult.HardStop(code, $"Specimen {accessionNumber} is {status}; it cannot be used for the workup.");
    }
}
