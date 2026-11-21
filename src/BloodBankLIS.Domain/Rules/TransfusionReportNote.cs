using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Cycle 701 (OCD-083): how a transfusion the EHR reported over a unit's status reads in the patient's product history
/// and timeline: the unit's status at the report, and whether a supervisor has reviewed it (by whom, and what they found).
/// Null for an ordinary transfusion.
/// </summary>
public static class TransfusionReportNote
{
    public static string? For(UnitStatus? unitStatusAtReport, string? reviewedBy, string? reviewNote)
    {
        if (unitStatusAtReport is not UnitStatus status)
        {
            return null;
        }

        var reported = $"Reported by the EHR while the unit was {status} in the blood bank";
        return string.IsNullOrWhiteSpace(reviewedBy)
            ? $"{reported}; review pending on the supervisor queue"
            : $"{reported}; reviewed by {reviewedBy.Trim()}" + (string.IsNullOrWhiteSpace(reviewNote) ? "" : $": {reviewNote.Trim()}");
    }
}
