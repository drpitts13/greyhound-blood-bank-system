using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Admin-defined ASTM instrument interface (connection, maps, and rules).</summary>
public class InstrumentInterface : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public string? Environment { get; set; }

    public int Version { get; set; } = 1;

    public InstrumentInterpretationMatch InterpretationMatch { get; set; } =
        InstrumentInterpretationMatch.AnalyteEqualsAssay;

    public string? InterpretationAnalyteCodes { get; set; }

    /// <summary>
    /// The sample ID prefixes this instrument's QC samples carry (comma-separated, for example QC-, CTRL), so a daily QC
    /// run is not taken for an unknown patient specimen (end-user testing pass 84). Blank: none; the IDs are the
    /// facility's, from how its analyzer is set up.
    /// </summary>
    public string? QcSampleIdPrefixes { get; set; }

    public InstrumentConnection? Connection { get; set; }

    public ICollection<InstrumentFieldMapping> FieldMappings { get; set; } = new List<InstrumentFieldMapping>();

    /// <summary>Code and value translations for this instrument (replaces the retired test mapping table).</summary>
    public ICollection<InstrumentValueTranslation> Translations { get; set; } = new List<InstrumentValueTranslation>();

    public ICollection<InstrumentInterfaceRule> Rules { get; set; } = new List<InstrumentInterfaceRule>();
}
