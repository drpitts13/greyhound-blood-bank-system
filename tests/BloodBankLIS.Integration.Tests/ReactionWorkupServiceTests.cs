using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>Transfusion reaction workup: specimens mapped to Test Groups by the settings, ordered and linked.</summary>
public class ReactionWorkupServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private DateTime Now => _factory.Clock.UtcNow;

    private ReactionWorkupService Workup(BloodBankDbContext c)
    {
        var specimens = new SpecimenService(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, currentUser: _factory.CurrentUser);
        var orders = new OrderService(
            new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c);
        return new ReactionWorkupService(
            new EfRepository<ReactionInvestigation>(c), new EfRepository<TransfusionEvent>(c), new EfRepository<Issue>(c),
            new EfRepository<Specimen>(c), new EfRepository<Order>(c), new EfRepository<Encounter>(c),
            new EfRepository<OrderingLocation>(c), new EfRepository<Crossmatch>(c),
            orders, specimens, new ReactionWorkupSettingsReader(new EfRepository<ReactionWorkupSettings>(c)),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser));
    }

    private ReactionWorkupSettingsAdminService Settings(BloodBankDbContext c)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ReactionWorkupSettingsAdminService(
            new EfRepository<ReactionWorkupSettings>(c), new EfRepository<TestGrouper>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env));
    }

    private sealed record Case(long InvestigationId, long PatientId, long CrossmatchTubeId, long OlderTubeId, long AfterIssueTubeId, long ExpiredTubeId);

    /// <summary>
    /// A transfused unit: issued 4 hours ago, transfusion started 3 hours ago, an open investigation. Tubes: the
    /// crossmatch tube (10 hours ago), an older tube (30 hours ago), an expired pre-transfusion tube, and one drawn
    /// 2 hours ago, after the transfusion began.
    /// </summary>
    private async Task<Case> SeedAsync(string key)
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var patient = new Patient { MedicalRecordNumber = $"MRN-RXW-{key}", LastName = "Workup", FirstName = key, DateOfBirth = new DateOnly(1965, 3, 1) };
        var product = new ProductType { ProductCode = $"RBC-RXW-{key}", Name = "Workup RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.Patients.Add(patient);
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = $"V-RXW-{key}", Status = EncounterStatus.Active, AdmitUtc = Now.AddDays(-2) };
        var unit = new BloodUnit { UnitNumber = $"U-RXW-{key}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = Now.AddDays(10), Status = UnitStatus.Transfused };
        c.Encounters.Add(encounter);
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        Specimen Tube(string suffix, DateTime collected, SpecimenStatus status = SpecimenStatus.Accepted, DateTime? expires = null) => new()
        {
            AccessionNumber = $"ACC-RXW-{key}-{suffix}", PatientId = patient.Id, EncounterId = encounter.Id, SpecimenType = "EDTA",
            CollectedUtc = collected, ExpiresUtc = expires ?? collected.AddDays(3), Status = status
        };
        var crossmatchTube = Tube("XM", Now.AddHours(-10));
        var olderTube = Tube("OLD", Now.AddHours(-30));
        var expiredTube = Tube("EXP", Now.AddDays(-4), SpecimenStatus.Expired, Now.AddDays(-1));
        var afterIssue = Tube("POST", Now.AddHours(-2));
        c.Specimens.AddRange(crossmatchTube, olderTube, expiredTube, afterIssue);
        var issue = new Issue
        {
            BloodProductId = unit.Id, PatientId = patient.Id, EncounterId = encounter.Id, IssuedUtc = Now.AddHours(-4),
            IssuedBy = "tech-test", Status = IssueStatus.Transfused
        };
        c.Issues.Add(issue);
        await c.SaveChangesAsync();
        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit.Id, PatientId = patient.Id, SpecimenId = crossmatchTube.Id, Method = CrossmatchMethod.Serologic,
            Result = CrossmatchResult.Compatible, PerformedUtc = Now.AddHours(-6), PerformedBy = "tech-test"
        });
        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id, BloodProductId = unit.Id, PatientId = patient.Id, StartUtc = Now.AddHours(-3), ReactionSuspected = true, DocumentedBy = "tech-test"
        };
        c.TransfusionEvents.Add(transfusion);
        await c.SaveChangesAsync();
        var inv = new ReactionInvestigation
        {
            TransfusionEventId = transfusion.Id, PatientId = patient.Id, BloodProductId = unit.Id, ReportedUtc = Now, ReportedBy = "tech-test",
            Status = ReactionInvestigationStatus.Open
        };
        c.ReactionInvestigations.Add(inv);
        await c.SaveChangesAsync();
        return new Case(inv.Id, patient.Id, crossmatchTube.Id, olderTube.Id, afterIssue.Id, expiredTube.Id);
    }

    // TEST-BB-442: the settings choose a released Test Group for each specimen, with a reason and history.
    [Fact]
    public async Task Settings_StartAtTheStarterGroups_AndAcceptOnlyReleasedGroupsWithAReason()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);

        var current = await Settings(c).GetAsync();
        Assert.Equal(("TRX-PRE", "TRX-POST"), (current.PreTransfusionTestGroupCode, current.PostTransfusionTestGroupCode));
        Assert.Equal(new[] { "ABORH", "DAT" }, current.TestGroups.Single(g => g.Code == "TRX-PRE").MemberTestCodes);
        Assert.Equal(new[] { "ABORH", "DAT", "ABSC" }, current.TestGroups.Single(g => g.Code == "TRX-POST").MemberTestCodes);

        var noReason = await Settings(c).UpdateAsync(new SaveReactionWorkupSettingsRequest("TNS", "TRX-POST", " "));
        Assert.Contains(noReason.Evaluation!.HardStops, r => r.Code == ReactionWorkupSettingsValidator.ReasonRequiredCode);
        var unknown = await Settings(c).UpdateAsync(new SaveReactionWorkupSettingsRequest("NOPE", "TRX-POST", "Use our own group"));
        Assert.Contains(unknown.Evaluation!.HardStops, r => r.Code == ReactionWorkupSettingsValidator.PreGroupCode);

        var saved = await Settings(c).UpdateAsync(new SaveReactionWorkupSettingsRequest(" tns ", "TRX-POST", "Pre sample gets a full type and screen"));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal("TNS", saved.Value!.PreTransfusionTestGroupCode);
        Assert.Equal(current.Version + 1, saved.Value.Version);
        Assert.True(await c.ConfigurationChangeHistory.AnyAsync(h => h.EntityType == nameof(ReactionWorkupSettings)));
    }

    // TEST-BB-443: the pre-transfusion specimen gets the pre-transfusion group as a Stat reaction workup order.
    [Fact]
    public async Task PreSpecimen_OrdersItsTestGroup_OnTheChosenTube_AndOptionsSuggestTheCrossmatchTube()
    {
        var s = await SeedAsync("PRE");
        await using var c = _factory.Create();

        var options = await Workup(c).ListOptionsAsync(s.InvestigationId);
        Assert.True(options.Succeeded, options.Error);
        Assert.Equal(s.CrossmatchTubeId, Assert.Single(options.Value!.PreCandidates, o => o.Suggested).SpecimenId);
        Assert.Contains(options.Value.PreCandidates, o => o.SpecimenId == s.ExpiredTubeId && o.Expired);
        Assert.DoesNotContain(options.Value.PreCandidates, o => o.SpecimenId == s.AfterIssueTubeId);
        Assert.Equal(new[] { s.AfterIssueTubeId }, options.Value.PostCandidates.Select(o => o.SpecimenId).ToArray());

        var attached = await Workup(c).AttachPreTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.CrossmatchTubeId));
        Assert.True(attached.Succeeded, attached.Error);
        Assert.Equal(s.CrossmatchTubeId, attached.Value!.PreTransfusionSpecimenId);

        var order = await c.Orders.SingleAsync(o => o.Id == attached.Value.PreTransfusionOrderId);
        Assert.Equal($"RXN-{s.InvestigationId}-PRE", order.OrderNumber);
        Assert.Equal(OrderType.TransfusionReactionWorkup, order.OrderType);
        Assert.Equal(OrderPriority.Stat, order.Priority);
        Assert.Equal(new[] { "ABORH", "DAT" }, await c.OrderLines.Where(l => l.OrderId == order.Id).OrderBy(l => l.LineNumber).Select(l => l.TestCode).ToArrayAsync());
        Assert.True(await c.OrderSpecimens.AnyAsync(os => os.OrderId == order.Id && os.SpecimenId == s.CrossmatchTubeId));
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(ReactionInvestigation) && a.EntityId == s.InvestigationId
            && a.Reason!.StartsWith("Pre-transfusion specimen added")));

        // Set once: a second pre-transfusion specimen is refused.
        var again = await Workup(c).AttachPreTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.OlderTubeId));
        Assert.False(again.Succeeded);
        Assert.Contains("already on this investigation", again.Error);
    }

    // TEST-BB-443: the wrong tube, an unconfigured group, or a closed case is refused, and nothing is ordered.
    [Fact]
    public async Task Attach_RefusesTheWrongTube_AnUnchosenGroup_AndAClosedCase()
    {
        var s = await SeedAsync("REF");
        await using var c = _factory.Create();

        var afterIssueAsPre = await Workup(c).AttachPreTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.AfterIssueTubeId));
        Assert.Contains(ReactionWorkupSpecimenRule.PreCode, afterIssueAsPre.Error);
        var beforeStartAsPost = await Workup(c).AttachPostTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.OlderTubeId));
        Assert.Contains(ReactionWorkupSpecimenRule.PostCode, beforeStartAsPost.Error);

        var settings = await c.ReactionWorkupSettings.SingleAsync();
        settings.PostTransfusionTestGroupCode = null;
        await c.SaveChangesAsync();
        var unchosen = await Workup(c).AttachPostTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.AfterIssueTubeId));
        Assert.Contains("No Test Group is chosen for the post-transfusion specimen", unchosen.Error);

        var inv = await c.ReactionInvestigations.SingleAsync(r => r.Id == s.InvestigationId);
        inv.Status = ReactionInvestigationStatus.Closed;
        await c.SaveChangesAsync();
        var closed = await Workup(c).AttachPreTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.CrossmatchTubeId));
        Assert.Contains(ReactionInvestigationLockRule.LockedCode, closed.Error);

        Assert.False(await c.Orders.AnyAsync(o => o.OrderNumber.StartsWith($"RXN-{s.InvestigationId}-")));
    }

    // TEST-BB-448: a cancelled workup order frees its role: the case asks for the specimen again, and a new tube
    // replaces it, with the replaced specimen and order kept in the audit.
    [Fact]
    public async Task CancelledWorkupOrder_FreesTheRole_ForAReplacementTube()
    {
        var s = await SeedAsync("CXL");
        await using var c = _factory.Create();
        var first = await Workup(c).AttachPreTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.OlderTubeId));
        Assert.True(first.Succeeded, first.Error);
        var firstOrderId = first.Value!.PreTransfusionOrderId!.Value;

        var order = await c.Orders.SingleAsync(o => o.Id == firstOrderId);
        order.Status = OrderStatus.Cancelled;
        await c.SaveChangesAsync();

        var inv = await c.ReactionInvestigations.AsNoTracking().SingleAsync(r => r.Id == s.InvestigationId);
        var workups = await ReactionWorkupReader.LoadAsync([inv], new EfRepository<Specimen>(c), new EfRepository<Order>(c),
            new EfRepository<OrderLine>(c), new EfRepository<TestResult>(c), Now, CancellationToken.None);
        var state = ReactionWorkupReader.StateFor(inv, workups.GetValueOrDefault(inv.Id));
        Assert.False(state.PreLinked);
        Assert.Null(workups[inv.Id].Pre);
        Assert.Equal("Choose the pre-transfusion specimen to order its workup tests.",
            ReactionWorkupCompletenessRule.Evaluate(true, true, state, null).Message);

        var replaced = await Workup(c).AttachPreTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.CrossmatchTubeId));
        Assert.True(replaced.Succeeded, replaced.Error);
        Assert.Equal(s.CrossmatchTubeId, replaced.Value!.PreTransfusionSpecimenId);
        Assert.NotEqual(firstOrderId, replaced.Value.PreTransfusionOrderId);
        Assert.Equal($"RXN-{s.InvestigationId}-PRE-2", (await c.Orders.SingleAsync(o => o.Id == replaced.Value.PreTransfusionOrderId)).OrderNumber);
        var events = await c.AuditEvents.Where(a => a.EntityType == nameof(ReactionInvestigation) && a.EntityId == s.InvestigationId)
            .OrderByDescending(a => a.Id).ToListAsync();
        var audit = events.FirstOrDefault(a => a.Reason?.StartsWith("Pre-transfusion specimen replaced") == true)
            ?? throw new Xunit.Sdk.XunitException(string.Join(" | ", events.Select(e => $"{e.EventType}:{e.Reason}")));
        Assert.StartsWith("Pre-transfusion specimen replaced", audit.Reason);
        Assert.Contains(firstOrderId.ToString(), audit.OldValueJson);

        // An order that is still active keeps the role.
        var again = await Workup(c).AttachPreTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.OlderTubeId));
        Assert.Contains("already on this investigation", again.Error);
    }

    // TEST-BB-444: an expired pre-transfusion tube can carry the workup (OCD-058); the post tube is accessioned here.
    [Fact]
    public async Task ExpiredPreTube_IsUsed_AndThePostTubeIsAccessionedWithItsGroup()
    {
        var s = await SeedAsync("EXP");
        await using var c = _factory.Create();

        var pre = await Workup(c).AttachPreTransfusionSpecimenAsync(s.InvestigationId, new AttachWorkupSpecimenRequest(s.ExpiredTubeId));
        Assert.True(pre.Succeeded, pre.Error);
        Assert.True(await c.OrderSpecimens.AnyAsync(os => os.OrderId == pre.Value!.PreTransfusionOrderId && os.SpecimenId == s.ExpiredTubeId));

        var tooEarly = await Workup(c).AccessionPostTransfusionSpecimenAsync(s.InvestigationId,
            new AccessionPostTransfusionSpecimenRequest("ACC-RXW-EXP-NEW0", "EDTA", Now.AddHours(-5)));
        Assert.Contains(ReactionWorkupSpecimenRule.PostCode, tooEarly.Error);
        Assert.False(await c.Specimens.AnyAsync(sp => sp.AccessionNumber == "ACC-RXW-EXP-NEW0"));

        var post = await Workup(c).AccessionPostTransfusionSpecimenAsync(s.InvestigationId,
            new AccessionPostTransfusionSpecimenRequest("ACC-RXW-EXP-NEW", "EDTA", Now.AddMinutes(-20)));
        Assert.True(post.Succeeded, post.Error);
        var tube = await c.Specimens.SingleAsync(sp => sp.AccessionNumber == "ACC-RXW-EXP-NEW");
        Assert.Equal(SpecimenStatus.Accepted, tube.Status);
        Assert.Equal(tube.Id, post.Value!.PostTransfusionSpecimenId);
        var order = await c.Orders.SingleAsync(o => o.Id == post.Value.PostTransfusionOrderId);
        Assert.Equal($"RXN-{s.InvestigationId}-POST", order.OrderNumber);
        Assert.Equal(new[] { "ABORH", "DAT", "ABSC" }, await c.OrderLines.Where(l => l.OrderId == order.Id).OrderBy(l => l.LineNumber).Select(l => l.TestCode).ToArrayAsync());
    }
}
