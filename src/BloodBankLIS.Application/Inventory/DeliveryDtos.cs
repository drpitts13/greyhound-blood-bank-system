using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record SaveDeliveryLineRequest(ComponentClass ComponentClass, int ExpectedQuantity);

public sealed record CreateDeliveryRequest(
    long BloodSupplierId,
    DateTime OrderedUtc,
    string? PackingListNumber,
    IReadOnlyList<SaveDeliveryLineRequest> Lines);

public sealed record UpdateDeliveryRequest(
    long BloodSupplierId,
    DateTime OrderedUtc,
    string? PackingListNumber,
    IReadOnlyList<SaveDeliveryLineRequest> Lines);

public sealed record DeliveryLineDto(
    long Id,
    ComponentClass ComponentClass,
    string ProductLabel,
    int ExpectedQuantity,
    int ReceivedQuantity)
{
    public int Variance => ReceivedQuantity - ExpectedQuantity;
}

public sealed record OpenDeliveryDto(
    long Id,
    long BloodSupplierId,
    string SupplierCode,
    string SupplierName,
    string? PackingListNumber,
    DateTime OrderedUtc,
    DateTime ReceivedUtc,
    int ExpectedQuantity,
    int ReceivedQuantity)
{
    public string SupplierNameLabel => BloodSupplierLabel.Name(BloodSupplierId, SupplierName);
}

public sealed record DeliveryDetailDto(
    long Id,
    long BloodSupplierId,
    string SupplierCode,
    string SupplierName,
    string? PackingListNumber,
    DateTime OrderedUtc,
    DateTime ReceivedUtc,
    DeliveryStatus Status,
    bool SupplierLocked,
    IReadOnlyList<DeliveryLineDto> Lines,
    IReadOnlyList<BloodUnitDto> Units,
    string? CompletionNote = null,
    DateTime? CompletedUtc = null)
{
    /// <summary>The products short or over, for example "RBC 2 short"; null when everything matches.</summary>
    public string? Differences => DeliveryCompletionRule.Differences(
        Lines.Select(l => new DeliveryCompletionRule.Line(l.ProductLabel, l.ExpectedQuantity, l.ReceivedQuantity)));

    public string SupplierNameLabel => BloodSupplierLabel.Name(BloodSupplierId, SupplierName);
}

/// <summary>Completing a delivery; the note is required when it does not match its packing list (pass 83).</summary>
public sealed record CompleteDeliveryRequest(string? Note = null);

public sealed record DeliveryReceiveContext(long Id, string SupplierName, string? PackingListNumber);
