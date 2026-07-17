using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Links an order to one or more specimens (many-to-many junction).
/// </summary>
public class OrderSpecimen : BaseEntity
{
    public long OrderId { get; set; }

    public Order? Order { get; set; }

    public long SpecimenId { get; set; }

    public Specimen? Specimen { get; set; }

    public bool IsPrimary { get; set; } = true;
}
