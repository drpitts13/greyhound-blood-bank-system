namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A patient's death as the EHR sends it (end-user testing pass 107: PID-29 and PID-30 were not read, so a chart never
/// showed that the patient had died and units stayed reserved for them). PID-30 "Y" or a PID-29 date records the death;
/// an explicit "N" clears it (the EHR correcting an error); an empty field changes nothing. The blood bank is told, with
/// the units still reserved for the patient named so they can be released. Information only: nothing is released,
/// held or blocked by it.
/// </summary>
public static class PatientDeathRule
{
    /// <summary>True for "Y", false for "N", null for anything else or empty; a death date alone means deceased.</summary>
    public static bool? Indicator(string? deathIndicator, DateTime? deathUtc)
    {
        var value = deathIndicator?.Trim();
        if (string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "N", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return deathUtc is null ? null : true;
    }

    public static AlertCandidate Alert(long patientId, string mrn, string name, DateTime? deathUtc, IReadOnlyList<string> reservedUnits, string messageControlId)
    {
        var units = reservedUnits.Count == 0
            ? "No unit is reserved for the patient."
            : $"{reservedUnits.Count} {(reservedUnits.Count == 1 ? "unit is" : "units are")} still reserved for the patient ({string.Join(", ", reservedUnits)}): release {(reservedUnits.Count == 1 ? "it" : "them")} to stock if no longer needed.";
        return new AlertCandidate(
            AlertRule.PatientDeceased,
            $"DECEASED:{patientId}:{messageControlId}",
            reservedUnits.Count == 0 ? AlertSeverity.Info : AlertSeverity.Warning,
            $"EHR recorded {name} (MRN {mrn}) as deceased",
            $"The EHR recorded the patient's death{(deathUtc is DateTime at ? $" at {at:yyyy-MM-dd HH:mm} UTC" : "")}. {units}",
            $"/patients/{patientId}");
    }
}
