namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A change to a patient's date of birth or sex from the EHR (ADT update) on a chart the blood bank already holds. The
/// date of birth is the second identifier checked on every tube label and at issue, so a tube drawn before the change
/// carries the old one: the blood bank is told, with the patient's current tubes named. Information only: nothing is
/// changed, held or blocked. End-user testing pass 42: a date of birth changed by ADT^A08 with no word to anyone.
/// </summary>
public static class PatientIdentityChangeRule
{
    public static IReadOnlyList<string> Changes(DateOnly oldBirth, DateOnly newBirth, string oldSex, string newSex)
    {
        var changes = new List<string>();
        if (oldBirth != default && oldBirth != newBirth)
        {
            changes.Add($"date of birth {oldBirth:yyyy-MM-dd} → {newBirth:yyyy-MM-dd}");
        }

        if (!string.Equals(oldSex, newSex, StringComparison.Ordinal) && !string.Equals(oldSex, "Unknown", StringComparison.Ordinal))
        {
            changes.Add($"sex {oldSex} → {newSex}");
        }

        return changes;
    }

    public static AlertCandidate Alert(
        long patientId, string mrn, string name, IReadOnlyList<string> changes, IReadOnlyList<string> currentTubes, string messageControlId)
    {
        var tubes = currentTubes.Count == 0
            ? "No current tube."
            : $"Current {(currentTubes.Count == 1 ? "tube" : "tubes")} {string.Join(", ", currentTubes)} {(currentTubes.Count == 1 ? "was" : "were")} labelled before the change: check {(currentTubes.Count == 1 ? "its label" : "their labels")} against the chart before testing or issue.";
        return new AlertCandidate(
            AlertRule.PatientIdentityChanged,
            $"IDCHANGE:{patientId}:{messageControlId}",
            AlertSeverity.Warning,
            $"EHR changed {name} (MRN {mrn})",
            $"The EHR changed {string.Join(" and ", changes)}. {tubes}",
            $"/patients/{patientId}");
    }
}
