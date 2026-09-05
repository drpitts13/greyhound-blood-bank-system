using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Entry-form wording for stored <see cref="ResultSource"/>. Catalog-matching
/// panel entry is Calculated, not a typed Manual ABO. This is UX for an already
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
        source == ResultSource.Calculated
            ? "Result entered as Calculated (catalog interpretation)."
            : $"Result entered as {source}.";
}
