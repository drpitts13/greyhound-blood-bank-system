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

    public InstrumentConnection? Connection { get; set; }

    public ICollection<InstrumentFieldMapping> FieldMappings { get; set; } = new List<InstrumentFieldMapping>();

    public ICollection<InstrumentTestMapping> TestMappings { get; set; } = new List<InstrumentTestMapping>();

    public ICollection<InstrumentInterfaceRule> Rules { get; set; } = new List<InstrumentInterfaceRule>();
}
