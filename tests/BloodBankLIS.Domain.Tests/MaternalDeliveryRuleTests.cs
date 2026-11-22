using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class MaternalDeliveryRuleTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly DateOnly Born = new(2026, 11, 13);

    // TEST-BB-1168: a linked infant born within the look-back whose delivery is not recorded on the mother's chart is
    // named; a pregnancy recorded on or after the birth, an older infant, or none, is not (end-user testing pass 146).
    [Fact]
    public void Unrecorded_NamesALinkedBirthWithNoPregnancyRecorded()
    {
        Assert.Equal(Born, MaternalDeliveryRule.Unrecorded(null, [Born], Today));
        Assert.Equal(Born, MaternalDeliveryRule.Unrecorded(null, [new DateOnly(2024, 1, 5), Born], Today));
        var earlier = DateTime.SpecifyKind(new DateTime(2026, 6, 1), DateTimeKind.Local).ToUniversalTime();
        Assert.Equal(Born, MaternalDeliveryRule.Unrecorded(earlier, [Born], Today));
        var onBirth = RecentPregnancyRule.ToUtc(Born, new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc));
        Assert.Null(MaternalDeliveryRule.Unrecorded(onBirth, [Born], Today));
        var eveningBefore = DateTime.SpecifyKind(new DateTime(2026, 11, 12, 19, 27, 0), DateTimeKind.Local).ToUniversalTime();
        Assert.Null(MaternalDeliveryRule.Unrecorded(eveningBefore, [Born], Today));
        var twoDaysBefore = DateTime.SpecifyKind(new DateTime(2026, 11, 11, 12, 0, 0), DateTimeKind.Local).ToUniversalTime();
        Assert.Equal(Born, MaternalDeliveryRule.Unrecorded(twoDaysBefore, [Born], Today));
        Assert.Null(MaternalDeliveryRule.Unrecorded(null, [new DateOnly(2026, 1, 2)], Today));
        Assert.Null(MaternalDeliveryRule.Unrecorded(null, [], Today));
    }
}
