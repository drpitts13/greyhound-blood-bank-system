using BloodBankLIS.Domain.Common;

using BloodBankLIS.Domain.Enums;



namespace BloodBankLIS.Domain.Entities;



/// <summary>

/// A blood bank order. <see cref="OrderNumber"/> preserves the placer/source order

/// identifier and is unique; it is distinct from the surrogate <see cref="BaseEntity.Id"/>.

/// Every order requires a visit (<see cref="EncounterId"/>) and ordering location.

/// </summary>

public class Order : BaseEntity

{

    public string OrderNumber { get; set; } = string.Empty;



    public long PatientId { get; set; }



    public Patient? Patient { get; set; }



    public long EncounterId { get; set; }



    public Encounter? Encounter { get; set; }



    public long OrderingLocationId { get; set; }



    public OrderingLocation? OrderingLocation { get; set; }



    public OrderCategory OrderCategory { get; set; } = OrderCategory.Test;



    public string OrderName { get; set; } = string.Empty;



    public OrderType OrderType { get; set; } = OrderType.TypeAndScreen;



    public string? TestCode { get; set; }



    public long? ProductTypeId { get; set; }



    public ProductType? ProductType { get; set; }



    public OrderPriority Priority { get; set; } = OrderPriority.Routine;



    public OrderStatus Status { get; set; } = OrderStatus.New;



    public OrderSource Source { get; set; } = OrderSource.Manual;



    public long? OrderingProviderId { get; set; }

    public OrderingProvider? OrderingProviderRef { get; set; }

    /// <summary>Display name at time of order (denormalized from provider or HL7 text).</summary>
    public string? OrderingProvider { get; set; }

    public DateTime OrderedUtc { get; set; }



    public string? FillerOrderNumber { get; set; }



    public string? SourceSystem { get; set; }



    public string? OrderedByUser { get; set; }



    public string? CancellationReason { get; set; }



    public ResultStatus? ResultStatus { get; set; }



    public FulfillmentStatus? FulfillmentStatus { get; set; }



    public ICollection<OrderSpecimen> OrderSpecimens { get; set; } = new List<OrderSpecimen>();

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();

}

