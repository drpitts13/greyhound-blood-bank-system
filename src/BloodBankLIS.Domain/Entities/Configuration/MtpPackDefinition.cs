using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// One facility-defined massive transfusion protocol pack: how many units of each component class make up a round.
/// None are shipped. Contents are a medical-director decision (OCD-012, REQUIRES SME VERIFICATION). A pack is
/// guidance for the issue desk only: it never blocks or adds a unit, and every unit still passes the issue gate.
/// </summary>
public class MtpPackDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int RedCellUnits { get; set; }

    public int PlasmaUnits { get; set; }

    public int PlateletUnits { get; set; }

    public int CryoUnits { get; set; }

    public int WholeBloodUnits { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
