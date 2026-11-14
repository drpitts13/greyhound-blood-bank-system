using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// An incompatible crossmatch recorded after a unit was released uncrossmatched (emergency or MTP).
/// The unit may already be at the bedside or in the patient, so the result is raised as a critical
/// alert and returned as a warning, and a critical deviation is opened for it (OCD-046, decided
/// 2026-11-04: a deviation is required). Who is notified and what clinical follow-up happens stays
/// facility procedure; the system records who acknowledged the alert.
/// </summary>
public static class RetrospectiveCrossmatchIncompatibleRule
{
    public const string Code = "ISS-RETRO-XM-INCOMPATIBLE";

    /// <summary>Deviation context for the issue the incompatible crossmatch belongs to; one deviation per issue.</summary>
    public const string DeviationContextType = "Issue";

    public const DeviationSeverity DeviationSeverityLevel = DeviationSeverity.Critical;

    public static string DeviationTitle(string unitNumber) =>
        $"Incompatible retrospective crossmatch on emergency-released unit {unitNumber}";

    public static string DeviationDescription(long issueId, string? mrn, string? patientName, string unitNumber, UnitStatus status, long crossmatchId) =>
        $"Issue {issueId}, unit {unitNumber}, patient {Patient(mrn, patientName)}. The crossmatch recorded after emergency or " +
        $"massive-transfusion release (crossmatch {crossmatchId}) was incompatible. {UnitState(status)} " +
        "Opened automatically (OCD-046). Record the notifications made and the clinical follow-up as the corrective action.";

    private static string DeviationText(long? deviationId) =>
        deviationId is { } id ? $" Deviation {id} was opened." : string.Empty;

    /// <summary>Plain words for where the unit stands, so the reader knows how urgent this is.</summary>
    public static string UnitState(UnitStatus status) => status switch
    {
        UnitStatus.TransfusionStarted => "A transfusion of this unit has started.",
        UnitStatus.Transfused => "This unit has been transfused.",
        UnitStatus.Issued => "The unit is issued and may be about to be transfused.",
        _ => $"Unit status: {status}."
    };

    public static RuleResult Warning(string unitNumber, UnitStatus status, long? deviationId = null) =>
        RuleResult.Warning(Code,
            $"Crossmatch incompatible for emergency-released unit {unitNumber}. {UnitState(status)} " +
            $"A critical alert was raised.{DeviationText(deviationId)} Follow your facility's procedure and record who was told when you acknowledge it.");

    public static AlertCandidate Alert(long issueId, string? mrn, string? patientName, string unitNumber, UnitStatus status, long? deviationId = null) =>
        new(
            AlertRule.RetrospectiveCrossmatchIncompatible,
            $"RETROXM:ISSUE-{issueId}",
            AlertSeverity.Critical,
            $"Incompatible crossmatch on emergency-released unit {unitNumber}",
            $"Patient {Patient(mrn, patientName)}. {UnitState(status)} Follow your facility's procedure for an incompatible " +
            $"crossmatch after emergency release, and record who was told and when in the acknowledgement note.{DeviationText(deviationId)}",
            "/issuing?desk=retro");

    private static string Patient(string? mrn, string? name) =>
        (string.IsNullOrWhiteSpace(name), string.IsNullOrWhiteSpace(mrn)) switch
        {
            (false, false) => $"{name!.Trim()} (MRN {mrn!.Trim()})",
            (true, false) => $"MRN {mrn!.Trim()}",
            (false, true) => name!.Trim(),
            _ => "not recorded"
        };
}
