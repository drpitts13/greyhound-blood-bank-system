namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Patient phrase for modification history. The modification record does not store
/// a patient. A missing order does not invent one. Disagreeing orders do not pick one.
/// </summary>
public static class ModificationHistoryPatientCue
{
    public const string NoPatientOrder = "No patient order was recorded.";

    public static string Format(string? patientLabel, bool orderCouldNotBeRead = false, bool linkedOrdersDisagree = false)
    {
        if (linkedOrdersDisagree)
        {
            return "The linked orders do not name one patient.";
        }

        if (!string.IsNullOrWhiteSpace(patientLabel))
        {
            return patientLabel.Trim();
        }

        return orderCouldNotBeRead
            ? "The order could not be read."
            : NoPatientOrder;
    }
}
