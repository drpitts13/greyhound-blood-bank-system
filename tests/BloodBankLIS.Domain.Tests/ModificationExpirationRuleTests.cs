using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class ModificationExpirationRuleTests
{
    [Fact]
    public void ComputeNewExpiresUtc_OffsetBeforeOriginal_UsesOffset()
    {
        var modificationUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var offset = new ExpirationOffsetCode(24, ExpirationOffsetUnit.Hours);
        var originalExpiresUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = ModificationExpirationRule.ComputeNewExpiresUtc(modificationUtc, offset, originalExpiresUtc);

        Assert.Equal(new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void ComputeNewExpiresUtc_OffsetPastOriginal_IsCappedAtOriginal()
    {
        var modificationUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var offset = new ExpirationOffsetCode(5, ExpirationOffsetUnit.Days);
        var originalExpiresUtc = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

        var result = ModificationExpirationRule.ComputeNewExpiresUtc(modificationUtc, offset, originalExpiresUtc);

        Assert.Equal(originalExpiresUtc, result);
    }

    [Fact]
    public void ComputeNewExpiresUtc_ExactlyEqualToOriginal_ReturnsOriginal()
    {
        var modificationUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var offset = new ExpirationOffsetCode(24, ExpirationOffsetUnit.Hours);
        var originalExpiresUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var result = ModificationExpirationRule.ComputeNewExpiresUtc(modificationUtc, offset, originalExpiresUtc);

        Assert.Equal(originalExpiresUtc, result);
    }

    [Fact]
    public void EarliestExpiration_ReturnsMinimum()
    {
        var dates = new[]
        {
            new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = ModificationExpirationRule.EarliestExpiration(dates);

        Assert.Equal(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void ComputeNewExpiresUtc_PoolUsesEarliestSourceAsCeiling()
    {
        var modificationUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var offset = new ExpirationOffsetCode(10, ExpirationOffsetUnit.Days);
        var sourceExpirations = new[]
        {
            new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)
        };
        var earliest = ModificationExpirationRule.EarliestExpiration(sourceExpirations);

        var result = ModificationExpirationRule.ComputeNewExpiresUtc(modificationUtc, offset, earliest);

        Assert.Equal(earliest, result);
    }
}
