using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Maps a catalog data item (e.g. patient_id) to an ASTM path (P.3).</summary>
public class InstrumentFieldMapping : BaseEntity
{
    public long InterfaceId { get; set; }

    public InstrumentInterface? Interface { get; set; }

    public string DataItemKey { get; set; } = string.Empty;

    public string AstmPath { get; set; } = string.Empty;

    public bool IsRequired { get; set; }
}
