using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Patient identity merge (SoftBank / SafeTrace ADT A18/A40). Duplicate records
/// are never deleted; the loser is marked Merged and history is retained
/// (AABB unique identification, 21 CFR 606.160, CAP TRM.30550).
/// </summary>
public static class PatientMergeRule
{
    public const string IdentityCode = "PAT-MERGE-IDENTITY";
    public const string StatusCode = "PAT-MERGE-STATUS";
    public const string AboCode = "PAT-MERGE-ABORH";
    public const string ClinicalUseCode = "PAT-MERGED-INACTIVE";

    /// <summary>
    /// A merged (losing) record must not be used for issue, allocation, or testing.
    /// Inactive patients remain clinically usable pending facility policy (OCD-009).
    /// </summary>
    public static RuleResult EvaluateClinicalUse(PatientStatus status)
    {
        if (status == PatientStatus.Merged)
        {
            return RuleResult.HardStop(
                ClinicalUseCode,
                "This patient record is merged. Continue testing, allocation, and issue on the surviving patient record.");
        }

        return RuleResult.Pass(ClinicalUseCode);
    }

    public static IReadOnlyList<RuleResult> Evaluate(
        long survivorId,
        long duplicateId,
        PatientStatus survivorStatus,
        PatientStatus duplicateStatus,
        long? duplicateMergedIntoId,
        AboGroup survivorAbo,
        RhType survivorRh,
        AboGroup duplicateAbo,
        RhType duplicateRh)
    {
        if (survivorId <= 0 || duplicateId <= 0)
        {
            return [RuleResult.HardStop(IdentityCode, "Survivor and duplicate patient ids are required.")];
        }

        if (survivorId == duplicateId)
        {
            return [RuleResult.HardStop(IdentityCode, "A patient cannot be merged into itself.")];
        }

        var results = new List<RuleResult>();

        if (survivorStatus == PatientStatus.Merged)
        {
            results.Add(RuleResult.HardStop(StatusCode, "The surviving patient is already merged; resolve the survivor first."));
        }
        else
        {
            results.Add(RuleResult.Pass(StatusCode));
        }

        if (duplicateStatus == PatientStatus.Merged)
        {
            if (duplicateMergedIntoId == survivorId)
            {
                results.Add(RuleResult.Pass(IdentityCode, "Duplicate is already merged into this survivor."));
            }
            else
            {
                results.Add(RuleResult.HardStop(
                    IdentityCode,
                    "The duplicate patient is already merged into a different record."));
            }
        }
        else
        {
            results.Add(RuleResult.Pass(IdentityCode));
        }

        results.Add(EvaluateBloodType(survivorAbo, survivorRh, duplicateAbo, duplicateRh));
        return results;
    }

    public static RuleResult EvaluateBloodType(
        AboGroup survivorAbo,
        RhType survivorRh,
        AboGroup duplicateAbo,
        RhType duplicateRh)
    {
        if (survivorAbo != AboGroup.Unknown
            && duplicateAbo != AboGroup.Unknown
            && survivorAbo != duplicateAbo)
        {
            return RuleResult.HardStop(
                AboCode,
                $"Cannot merge patients with discordant ABO ({survivorAbo} vs {duplicateAbo}).");
        }

        if (survivorRh != RhType.Unknown
            && duplicateRh != RhType.Unknown
            && survivorRh != duplicateRh)
        {
            return RuleResult.HardStop(
                AboCode,
                $"Cannot merge patients with discordant Rh ({survivorRh} vs {duplicateRh}).");
        }

        var survivorKnown = survivorAbo != AboGroup.Unknown || survivorRh != RhType.Unknown;
        var duplicateKnown = duplicateAbo != AboGroup.Unknown || duplicateRh != RhType.Unknown;
        if (survivorKnown != duplicateKnown)
        {
            return RuleResult.Warning(
                AboCode,
                "One record has a historical ABO/Rh and the other does not; history will be combined.");
        }

        return RuleResult.Pass(AboCode);
    }
}
