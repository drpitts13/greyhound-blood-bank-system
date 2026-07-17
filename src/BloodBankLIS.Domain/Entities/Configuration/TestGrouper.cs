using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Named bundle of existing tests (e.g. Type and Screen). Selecting a grouper on an order
/// expands to separate order lines for each member test.
/// </summary>
public class TestGrouper : VersionedConfigEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>JSON array of <see cref="ValueObjects.TestGrouperMember"/>.</summary>
    public string? MemberTestsJson { get; set; }
}
