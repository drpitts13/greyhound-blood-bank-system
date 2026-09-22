namespace BloodBankLIS.Application.Inventory;

public sealed record UnitActivityDto(
    DateTime WhenUtc,
    string EventType,
    string Summary,
    string? ChangedBy,
    string? RelatedEntityType,
    long? RelatedEntityId);
