namespace BloodBankLIS.Domain.Instruments;

/// <summary>The fields an instrument's translation table covers.</summary>
public static class InstrumentTranslationKeys
{
    public const string OrderTestCode = "Instrument.OrderTestCode";
    public const string ResultTestCode = "Instrument.ResultTestCode";
    public const string SubtestCode = "Instrument.SubtestCode";
    public const string ResultValue = "Instrument.ResultValue";
    public const string Interpretation = "Instrument.Interpretation";
    public const string ErrorCode = "Instrument.ErrorCode";

    /// <summary>Subtest local value: store the reaction under the instrument's own code (for example a control).</summary>
    public const string Keep = "KEEP";

    /// <summary>Subtest local value: the instrument sends it, the LIS does not store it.</summary>
    public const string Ignore = "IGNORE";

    /// <summary>A result test or subtest code with no translation: the assay is not posted.</summary>
    public const string UnknownCode = "INST-CODE";

    /// <summary>The translated test is not ordered on the specimen: the assay is not posted to another line.</summary>
    public const string NotOrderedCode = "INST-NOT-ORDERED";

    /// <summary>An error or flag code translated (or defaulted) to Hold: the assay waits in instrument failures.</summary>
    public const string HeldCode = "INST-HOLD";

    public sealed record Field(string Key, string DisplayName, string Description, bool Outbound, bool AllowsContext);

    public static IReadOnlyList<Field> All { get; } =
    [
        new(ResultTestCode, "Result test code", "The instrument's assay code on a result (O.5.4) and the LIS test it posts to. A code with no translation is not posted.", false, false),
        new(SubtestCode, "Subtest code", "The instrument's analyte code (R.3.4) and the LIS subtest, phase, or panel cell it fills. An unknown analyte stops its assay from posting.", false, false),
        new(OrderTestCode, "Order test code", "The assay code sent to the instrument for an LIS test when it asks for a specimen's orders.", true, false),
        new(ResultValue, "Result value", "A reaction or value the instrument sends (R.4.1) and the LIS value; can be limited to one test.", false, true),
        new(Interpretation, "Interpretation", "The instrument's assay interpretation and the LIS interpretation (for example B Pos to B|Positive); can be limited to one test.", false, true),
        new(ErrorCode, "Error or flag code", "An instrument error (L.3) or result flag (R.7) and what the LIS does: Hold, Warn, or Ignore. A code with no translation is held.", false, true)
    ];

    public static bool IsKnown(string? key) => All.Any(f => string.Equals(f.Key, key, StringComparison.Ordinal));

    public static Field? Find(string? key) => All.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.Ordinal));
}

/// <summary>What the LIS does with a result that carries an instrument error or flag code.</summary>
public enum InstrumentErrorAction
{
    /// <summary>The result is not posted; the message waits in instrument failures for review.</summary>
    Hold,

    /// <summary>The result is posted with the code noted for the verifier.</summary>
    Warn,

    /// <summary>The code has no effect.</summary>
    Ignore
}
