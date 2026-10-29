using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class DashboardQueueCatalogTests
{
    [Fact]
    public void Select_IssueCreate_IncludesIssueBoards_AndOmitsReactions()
    {
        var cards = DashboardQueueCatalog.Select(Has(PermissionCodes.IssueCreate));

        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Retrospective && card.Href == "/issuing?desk=retro");
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.InTransit);
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Ready);
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.SecondAbo);
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Outstanding);
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Disposition);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.Reactions);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.Handoff);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.PendingTests);
    }

    [Fact]
    public void Select_TransfusionDocumentWithoutIssue_SeesIssuedBoardsOnly()
    {
        var cards = DashboardQueueCatalog.Select(Has(PermissionCodes.TransfusionDocument));

        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Outstanding && card.Href == "/issuing?desk=issued");
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Disposition);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.InTransit);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.Retrospective);
    }

    [Fact]
    public void Select_ResultEnterAndSpecimenAccession_OpensThoseWorklists()
    {
        var cards = DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter, PermissionCodes.SpecimenAccession));

        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.PendingTests && card.Href == "/test-worklist");
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.PendingSpecimens && card.Href == "/test-worklist?tab=specimens");
    }

    [Fact]
    public void Select_SupervisorPermissions_IncludeTheQueue_AndOmitItOtherwise()
    {
        Assert.Contains(
            DashboardQueueCatalog.Select(Has(PermissionCodes.ResultVerify)),
            card => card.Key == DashboardQueueCatalog.Supervisor && card.Href == "/supervisor");
        Assert.Contains(
            DashboardQueueCatalog.Select(Has(PermissionCodes.InventoryRelease)),
            card => card.Key == DashboardQueueCatalog.Supervisor);
        Assert.DoesNotContain(
            DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter)),
            card => card.Key == DashboardQueueCatalog.Supervisor);
    }

    [Fact]
    public void Select_ReactionAndAudit_IncludeThoseCards()
    {
        var cards = DashboardQueueCatalog.Select(Has(PermissionCodes.ReactionInvestigate, PermissionCodes.AuditRead));

        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Reactions && card.Href == "/reactions");
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Handoff && card.Href == "/handoff");
    }

    [Fact]
    public void Select_NoPermissions_OmitsEveryWorkCard()
    {
        Assert.Empty(DashboardQueueCatalog.Select(Has()));
    }

    [Fact]
    public void AttentionNote_StaysSilentAtZero_AndNamesAPositiveCount()
    {
        Assert.Null(DashboardQueueCatalog.AttentionNote(0, "past due", "past due"));
        Assert.Equal("1 past due", DashboardQueueCatalog.AttentionNote(1, "past due", "past due"));
        Assert.Equal("2 fatality follow-ups", DashboardQueueCatalog.AttentionNote(2, "fatality follow-up", "fatality follow-ups"));
        Assert.Null(DashboardQueueCatalog.HandoffNote(false));
        Assert.Equal("Needs acknowledgment", DashboardQueueCatalog.HandoffNote(true));
    }

    [Fact]
    public void IssuingDesk_OpensTheNamedBoard_AndKeepsIssueWhenAPatientIsPrefilled()
    {
        Assert.Equal("retro", IssuingDeskQuery.Resolve("retro", canIssue: true, canSeeIssued: true, hasPatientOrUnit: false));
        Assert.Equal("issued", IssuingDeskQuery.Resolve("issued", canIssue: false, canSeeIssued: true, hasPatientOrUnit: false));
        Assert.Equal("ready", IssuingDeskQuery.Resolve("retro", canIssue: false, canSeeIssued: true, hasPatientOrUnit: false));
        Assert.Equal("issue", IssuingDeskQuery.Resolve("retro", canIssue: true, canSeeIssued: true, hasPatientOrUnit: true));
        Assert.Equal("ready", IssuingDeskQuery.Resolve("unknown", canIssue: true, canSeeIssued: true, hasPatientOrUnit: false));
        Assert.Equal("ready", IssuingDeskQuery.Resolve(null, canIssue: true, canSeeIssued: true, hasPatientOrUnit: false));
    }

    private static Func<string, bool> Has(params string[] codes) =>
        code => codes.Contains(code, StringComparer.Ordinal);
}
