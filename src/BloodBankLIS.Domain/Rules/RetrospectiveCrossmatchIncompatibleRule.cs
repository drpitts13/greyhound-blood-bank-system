using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// An incompatible crossmatch recorded after a unit was released uncrossmatched (emergency or MTP).
/// The unit may already be at the bedside or in the patient, so the result is raised as a critical
/// alert and returned as a warning. What notification and investigation follow is facility
/// procedure (OCD-046); the system only makes sure someone is told and records who acknowledged it.
/// </summary>
public static class RetrospectiveCrossmatchIncompatibleRule
{
    public const string Code = "ISS-RETRO-XM-INCOMPATIBLE";

    /// <summary>Plain words for where the unit stands, so the reader knows how urgent this is.</summary>
    public static string UnitState(UnitStatus status) => status switch
    {
        UnitStatus.TransfusionStarted => "A transfusion of this unit has started.",
        UnitStatus.Transfused => "This unit has been transfused.",
        UnitStatus.Issued => "The unit is issued and may be about to be transfused.",
        _ => $"Unit status: {status}."
    };

    public static RuleResult Warning(string unitNumber, UnitStatus status) =>
        RuleResult.Warning(Code,
            $"Crossmatch incompatible for emergency-released unit {unitNumber}. {UnitState(status)} " +
            "A critical alert was raised. Follow your facility's procedure and record who was told when you acknowledge it.");

    public static AlertCandidate Alert(long issueId, string? mrn, string? patientName, string unitNumber, UnitStatus status) =>
        new(
            AlertRule.RetrospectiveCrossmatchIncompatible,
            $"RETROXM:ISSUE-{issueId}",
            AlertSeverity.Critical,
            $"Incompatible crossmatch on emergency-released unit {unitNumber}",
            $"Patient {Patient(mrn, patientName)}. {UnitState(status)} Follow your facility's procedure for an incompatible " +
            "crossmatch after emergency release, and record who was told and when in the acknowledgement note.",
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
