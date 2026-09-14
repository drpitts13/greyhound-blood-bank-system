using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Entry-form wording for stored <see cref="ResultSource"/>. Catalog-matching
/// panel entry is Calculated, not a typed Manual ABO. Verify, correct, submit,
/// and invalidate feedback also name that source. This is UX for an already
/// implemented provenance rule, not a new regulatory claim.
/// </summary>
public static class ResultSourceEntryCopy
{
    public const string PanelHint =
        "When panel reactions are recorded, the stored source is Calculated (catalog interpretation), not a typed Manual ABO/Rh. Instrument and Interface observations keep their source.";

    public const string TypedHint =
        "Typed test code and value (no panel) are stored as Manual. Instrument and Interface observations keep their source.";

    public static bool ShowsPanelHint(bool catalogLogicApplied) => catalogLogicApplied;

    public static string PanelEntered(ResultSource source) =>
        source == ResultSource.Calculated
            ? "Panel entered as Calculated (catalog interpretation)."
            : $"Panel entered as {source}.";

    public static string Saved(ResultSource source) =>
        source == ResultSource.Calculated
            ? "Result saved as Calculated (catalog interpretation)."
            : $"Result saved as {source}.";

    public static string Entered(ResultSource source) =>
        Named("Result entered", source);

    public static string Verified(ResultSource source) =>
        Named("Result verified", source);

    public static string VerifiedWithOverride(ResultSource source) =>
        Named("Result verified with ABO/Rh override", source);

    public static string Corrected(ResultSource source) =>
        Named("Result corrected", source);

    public static string Submitted(ResultSource source) =>
        Named("Submitted for verification", source);

    public static string Invalidated(ResultSource source) =>
        Named("Result invalidated", source);

    private static string Named(string action, ResultSource source) =>
        source == ResultSource.Calculated
            ? $"{action} as Calculated (catalog interpretation)."
            : $"{action} as {source}.";
}
