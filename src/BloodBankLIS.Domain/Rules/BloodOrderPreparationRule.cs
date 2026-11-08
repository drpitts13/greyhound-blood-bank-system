namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// What a blood (product) order still needs from the bench: each active product line is one unit; a unit reserved on the
/// order or already issued on it counts toward it. The next step is to collect a specimen when the patient has no usable
/// one, otherwise to reserve the units still needed. Nothing here decides compatibility; reserving runs its own checks.
/// </summary>
public static class BloodOrderPreparationRule
{
    public static int Remaining(int requested, int reserved, int issued) => Math.Max(0, requested - reserved - issued);

    /// <summary>
    /// Where the next step opens on the patient's chart: the specimens to collect one, or the reserve form with the order
    /// (and the usable specimen) already chosen.
    /// </summary>
    public static string NextStepHref(long patientId, long orderId, long? usableSpecimenId, bool typeAndScreenComplete = true) =>
        usableSpecimenId is not long specimenId
            ? $"/patients/{patientId}?tab=specimens"
            : typeAndScreenComplete
                ? $"/patients/{patientId}?tab=products&reserveOrder={orderId}&reserveSpecimen={specimenId}"
                : $"/patients/{patientId}?tab=tests";

    /// <summary>
    /// "Collect a specimen", "Finish type and screen" (a usable specimen whose ABO/Rh and antibody screen are not both
    /// verified yet: units are reserved on a specimen with a completed type and screen), "Reserve 2 units", or null when
    /// nothing is left to prepare.
    /// </summary>
    public static string? NextStep(int remaining, bool hasUsableSpecimen, bool typeAndScreenComplete = true) =>
        remaining <= 0
            ? null
            : !hasUsableSpecimen
                ? "Collect a specimen"
                : !typeAndScreenComplete
                    ? "Finish type and screen"
                    : $"Reserve {remaining} {(remaining == 1 ? "unit" : "units")}";
}
