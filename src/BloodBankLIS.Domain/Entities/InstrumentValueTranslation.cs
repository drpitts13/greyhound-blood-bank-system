using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One instrument's translation of a coded value: an order or result test code, a subtest code, a result value, an
/// interpretation, or an error code (see <c>InstrumentTranslationKeys</c>). A value row can apply to one LIS test
/// (<see cref="Context"/>) or, when blank, to all of them.
/// </summary>
public class InstrumentValueTranslation : BaseEntity
{
    public long InterfaceId { get; set; }

    public InstrumentInterface? Interface { get; set; }

    public string DataItemKey { get; set; } = string.Empty;

    /// <summary>What the instrument sends or expects.</summary>
    public string InstrumentValue { get; set; } = string.Empty;

    /// <summary>What the LIS stores (a test, subtest, value, or for error codes Hold, Warn, or Ignore).</summary>
    public string LocalValue { get; set; } = string.Empty;

    /// <summary>The LIS test code this row is limited to; null for every test.</summary>
    public string? Context { get; set; }

    public InterfaceTranslationDirection Direction { get; set; } = InterfaceTranslationDirection.Inbound;

    public int SortOrder { get; set; }

    /// <summary>What the instrument value means (from the vendor manual), shown with it; used on error codes.</summary>
    public string? Description { get; set; }
}
