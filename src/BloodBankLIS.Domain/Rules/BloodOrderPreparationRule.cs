namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// What a blood (product) order still needs from the bench: each active product line is one unit; a unit reserved on the
/// order or already issued on it counts toward it. The next step is to collect a specimen when the patient has no usable
/// one, otherwise to reserve the units still needed. Nothing here decides compatibility; reserving runs its own checks.
/// </summary>
public static class BloodOrderPreparationRule
{
    public static int Remaining(int requested, int reserved, int issued) => Math.Max(0, requested - reserved - issued);

    /// <summary>"Collect a specimen", "Reserve 2 units", or null when nothing is left to prepare.</summary>
    public static string? NextStep(int remaining, bool hasUsableSpecimen) =>
        remaining <= 0
            ? null
            : !hasUsableSpecimen
                ? "Collect a specimen"
                : $"Reserve {remaining} {(remaining == 1 ? "unit" : "units")}";
}
