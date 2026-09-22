using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

public sealed record SaveDeliveryLineRequest(long ProductTypeId, int ExpectedQuantity);

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
    long ProductTypeId,
    string ProductCode,
    string ProductName,
    int ExpectedQuantity,
    int ReceivedQuantity)
{
    public int Variance => ReceivedQuantity - ExpectedQuantity;
}

public sealed record OpenDeliveryDto(
    long Id,
    string SupplierCode,
    string SupplierName,
    string? PackingListNumber,
    DateTime OrderedUtc,
    DateTime ReceivedUtc,
    int ExpectedQuantity,
    int ReceivedQuantity);

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
    IReadOnlyList<BloodUnitDto> Units);

public sealed record DeliveryReceiveContext(long Id, string SupplierName, string? PackingListNumber);
