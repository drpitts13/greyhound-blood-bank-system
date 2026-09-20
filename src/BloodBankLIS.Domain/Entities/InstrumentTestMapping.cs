using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Maps an instrument assay/analyte code to an application test or subtest code.</summary>
public class InstrumentTestMapping : BaseEntity
{
    public long InterfaceId { get; set; }

    public InstrumentInterface? Interface { get; set; }

    public string InstrumentCode { get; set; } = string.Empty;

    public string ApplicationCode { get; set; } = string.Empty;

    public InstrumentMappingKind Kind { get; set; } = InstrumentMappingKind.Assay;

    public InterfaceTranslationDirection Direction { get; set; } = InterfaceTranslationDirection.Both;

    public int SortOrder { get; set; }
}
