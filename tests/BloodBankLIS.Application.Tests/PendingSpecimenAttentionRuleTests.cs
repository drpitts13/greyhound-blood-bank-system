using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using static BloodBankLIS.Application.Compliance.DashboardQueueCatalog;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-405: expired tubes on the pending-specimen list are named on the dashboard, which puts the card under
// Needs attention; a list with none stays a plain count.
public class PendingSpecimenAttentionRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static PendingSpecimenWorkItemDto Row(long id, DateTime expires, SpecimenStatus status = SpecimenStatus.Collected) =>
        PendingSpecimenWorkItemDto.From(
            new Specimen { Id = id, PatientId = 1, AccessionNumber = $"ACC-{id}", SpecimenType = "EDTA", CollectedUtc = Now.AddHours(-3), ExpiresUtc = expires, Status = status },
            null, Now);

    [Fact]
    public void ExpiredTubes_AreCounted_AndMakeTheCardNeedAttention()
    {
        var rows = new[] { Row(1, Now.AddMinutes(-5)), Row(2, Now.AddDays(2)), Row(3, Now, SpecimenStatus.Received) };
        var expired = PendingSpecimenAttentionRule.CountExpired(rows);
        Assert.Equal(2, expired);

        var note = AttentionNote(expired, "expired, to reject", "expired, to reject");
        Assert.Equal("2 expired, to reject", note);
        var card = Select(_ => true).Single(c => c.Key == PendingSpecimens);
        var layout = DashboardHome.Arrange([new DashboardCardState(card, rows.Length, note)], WorkFocus.Bench);
        Assert.Equal([PendingSpecimens], layout.Attention.Select(c => c.Card.Key));
    }

    [Fact]
    public void NoExpiredTubes_KeepsAPlainCount()
    {
        Assert.Equal(0, PendingSpecimenAttentionRule.CountExpired([Row(1, Now.AddDays(1))]));
        Assert.Null(AttentionNote(0, "expired, to reject", "expired, to reject"));
    }
}
