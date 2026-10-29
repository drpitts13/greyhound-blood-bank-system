using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Gates for undoing a patient merge. Unmerge restores only the rows the merge
/// ledger recorded, and only when nothing clinically new has happened since.
/// </summary>
public static class PatientUnmergeRule
{
    public const string PermissionCode = "PAT-UNMERGE-PERM";
    public const string ReasonCode = "PAT-UNMERGE-REASON";
    public const string StatusCode = "PAT-UNMERGE-STATUS";
    public const string LedgerCode = "PAT-UNMERGE-LEDGER";
    public const string ActivityCode = "PAT-UNMERGE-ACTIVITY";
    public const string ChangedCode = "PAT-UNMERGE-CHANGED";
    public const string DriftCode = "PAT-UNMERGE-DRIFT";

    public static RuleResult EvaluatePermission(bool hasPatientMerge) =>
        hasPatientMerge
            ? RuleResult.Pass(PermissionCode)
            : RuleResult.HardStop(
                PermissionCode,
                "Undoing a patient merge requires the patient.merge permission.");

    public static IReadOnlyList<RuleResult> Evaluate(
        string? reason,
        PatientStatus duplicateStatus,
        bool ledgerFound,
        bool ledgerAlreadyUnmerged,
        bool activitySinceMerge,
        bool movedRowChanged,
        bool survivorFieldDrifted)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(reason))
        {
            results.Add(RuleResult.HardStop(ReasonCode, "A reason is required to undo a patient merge."));
        }
        else
        {
            results.Add(RuleResult.Pass(ReasonCode));
        }

        if (ledgerAlreadyUnmerged)
        {
            results.Add(RuleResult.HardStop(StatusCode, "This merge has already been undone."));
        }
        else if (duplicateStatus != PatientStatus.Merged)
        {
            results.Add(RuleResult.HardStop(StatusCode, "This patient record is not merged."));
        }
        else
        {
            results.Add(RuleResult.Pass(StatusCode));
        }

        if (!ledgerFound)
        {
            results.Add(RuleResult.HardStop(
                LedgerCode,
                "This merge was recorded before unmerge was available and cannot be reversed automatically."));
        }
        else if (!ledgerAlreadyUnmerged)
        {
            results.Add(RuleResult.Pass(LedgerCode));
        }

        if (activitySinceMerge)
        {
            results.Add(RuleResult.HardStop(
                ActivityCode,
                "The surviving patient has a new order, specimen, result, crossmatch, allocation, issue, or transfusion since the merge. Unmerge is blocked."));
        }
        else
        {
            results.Add(RuleResult.Pass(ActivityCode));
        }

        if (movedRowChanged)
        {
            results.Add(RuleResult.HardStop(
                ChangedCode,
                "A record moved by this merge was changed afterward, or no longer belongs to the surviving patient. Unmerge is blocked so history is not split by guesswork."));
        }
        else
        {
            results.Add(RuleResult.Pass(ChangedCode));
        }

        if (survivorFieldDrifted)
        {
            results.Add(RuleResult.HardStop(
                DriftCode,
                "The surviving patient's deceased status or pregnancy date changed after the merge. Unmerge is blocked."));
        }
        else
        {
            results.Add(RuleResult.Pass(DriftCode));
        }

        return results;
    }
}
