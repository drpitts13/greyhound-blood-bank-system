using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A value an interface sent or needed that had no translation: an inbound order code with no test or grouper, or a
/// coded field (race, patient type…) for which translations exist but none matched. One row per interface, field,
/// direction, and value, counted each time it is seen, until a translation is saved for it or it is dismissed.
/// </summary>
public class InterfaceUnmappedValue : BaseEntity
{
    public long? EndpointId { get; set; }

    public string DataItemKey { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public InterfaceTranslationDirection Direction { get; set; }

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public int Count { get; set; }

    public string? LastMessageControlId { get; set; }

    public DateTime? ResolvedUtc { get; set; }

    public string? ResolvedBy { get; set; }

    /// <summary>"Translation saved" or the reason it was dismissed.</summary>
    public string? ResolvedNote { get; set; }
}
