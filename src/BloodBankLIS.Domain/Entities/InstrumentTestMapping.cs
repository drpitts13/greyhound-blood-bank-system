using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Retired: the instrument assay/analyte map, copied into <see cref="InstrumentValueTranslation"/> by migration
/// 20261125000000_InstrumentValueTranslations. Kept only so the old rows stay readable until the table is dropped.
/// </summary>
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
