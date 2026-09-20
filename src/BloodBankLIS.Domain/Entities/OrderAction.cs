using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// An action attached to a product order (for example irradiate after allocating
/// a normal red cell). Completing the action updates the order and captures billing.
/// </summary>
public class OrderAction : BaseEntity
{
    public long OrderId { get; set; }

    public Order? Order { get; set; }

    public long? OrderLineId { get; set; }

    public OrderLine? OrderLine { get; set; }

    public long ActionDefinitionId { get; set; }

    public ActionDefinition? ActionDefinition { get; set; }

    public OrderActionStatus Status { get; set; } = OrderActionStatus.Pending;

    public long? SourceBloodUnitId { get; set; }

    public BloodUnit? SourceBloodUnit { get; set; }

    public long? ResultBloodUnitId { get; set; }

    public BloodUnit? ResultBloodUnit { get; set; }

    public long? UnitModificationId { get; set; }

    public UnitModification? UnitModification { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string? CompletedBy { get; set; }
}
