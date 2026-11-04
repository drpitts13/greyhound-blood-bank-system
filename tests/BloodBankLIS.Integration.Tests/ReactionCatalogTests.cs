using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-409 (OCD-057): reactions and treatments come from admin lists that always offer No reaction and
// No treatment; closing an investigation records both, as the list read at the time.
public class ReactionCatalogTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private ReactionCatalogAdminService Admin(BloodBankDbContext c)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ReactionCatalogAdminService(
            new EfRepository<ReactionCatalogEntry>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env));
    }

    private ReactionInvestigationService Reactions(BloodBankDbContext c) =>
        new(new EfRepository<ReactionInvestigation>(c), new EfRepository<TransfusionEvent>(c), new InventoryRepository(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            catalog: new EfRepository<ReactionCatalogEntry>(c));

    private async Task<long> SeedInvestigationAsync()
    {
        await using var c = _factory.Create();
        await DatabaseSeeder.SeedAsync(c);
        var patient = new Patient { MedicalRecordNumber = "MRN-RXCAT", LastName = "Catalog", FirstName = "Test", DateOfBirth = new DateOnly(1970, 1, 1) };
        var product = new ProductType { ProductCode = "RBC-RXCAT", Name = "Test RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.Patients.Add(patient);
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        var unit = new BloodUnit { UnitNumber = "U-RXCAT", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = _factory.Clock.UtcNow.AddDays(5), Status = UnitStatus.Transfused };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        var issue = new Issue { BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddHours(-6), IssuedBy = "tech-test", Status = IssueStatus.Transfused };
        c.Issues.Add(issue);
        await c.SaveChangesAsync();
        var tx = new TransfusionEvent { IssueId = issue.Id, BloodProductId = unit.Id, PatientId = patient.Id, StartUtc = _factory.Clock.UtcNow.AddHours(-5), ReactionSuspected = true, DocumentedBy = "tech-test" };
        c.TransfusionEvents.Add(tx);
        await c.SaveChangesAsync();
        var inv = new ReactionInvestigation
        {
            TransfusionEventId = tx.Id, PatientId = patient.Id, BloodProductId = unit.Id, ReportedUtc = _factory.Clock.UtcNow,
            ClericalCheckCompleted = true, VisualInspectionCompleted = true, DatResult = DatWorkupResult.Negative, RemainderQuarantined = true
        };
        c.ReactionInvestigations.Add(inv);
        await c.SaveChangesAsync();
        return inv.Id;
    }

    private static UpdateReactionInvestigationRequest Close(string? reaction, IReadOnlyList<string>? treatments) =>
        new(null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Closed, ClosedSignatureId: 5,
            ConcludedReactionCode: reaction, TreatmentCodes: treatments);

    [Fact]
    public async Task Seeding_GivesNoneOnBothLists_AndTheyCannotBeRemovedOrRecoded()
    {
        await SeedInvestigationAsync();
        await using var c = _factory.Create();
        var entries = await Admin(c).ListAsync(null, includeInactive: true);
        var noReaction = Assert.Single(entries, e => e.Kind == ReactionCatalogKind.ReactionType && e.IsNoneOption);
        var noTreatment = Assert.Single(entries, e => e.Kind == ReactionCatalogKind.Treatment && e.IsNoneOption);
        Assert.Equal(("NONE", "No reaction"), (noReaction.Code, noReaction.Text));
        Assert.Equal("No treatment", noTreatment.Text);
        Assert.Contains(entries, e => e.Code == "FNHTR");

        Assert.False((await Admin(c).SetActiveAsync(noTreatment.Id, false)).Succeeded);
        Assert.False((await Admin(c).UpdateAsync(noReaction.Id, new SaveReactionCatalogEntryRequest(ReactionCatalogKind.ReactionType, "NIL", "Nothing", 0))).Succeeded);
        Assert.False((await Admin(c).CreateAsync(new SaveReactionCatalogEntryRequest(ReactionCatalogKind.Treatment, "none", "Another none", 5))).Succeeded);
        Assert.False((await Admin(c).CreateAsync(new SaveReactionCatalogEntryRequest(ReactionCatalogKind.ReactionType, "FNHTR", "Duplicate", 5))).Succeeded);
    }

    [Fact]
    public async Task Closing_RecordsTheConclusion_FromTheLists_AsTheyReadThen()
    {
        var id = await SeedInvestigationAsync();
        long washedId;
        await using (var c = _factory.Create())
        {
            var washed = await Admin(c).CreateAsync(new SaveReactionCatalogEntryRequest(ReactionCatalogKind.Treatment, "washed", "Washed cellular products", 10));
            Assert.True(washed.Succeeded, washed.Error);
            washedId = washed.Value!.Id;
            Assert.Equal("WASHED", washed.Value.Code);
            Assert.True(await c.ConfigurationChangeHistory.AnyAsync(h => h.EntityType == nameof(ReactionCatalogEntry) && h.EntityId == washedId));

            Assert.Contains(ReactionConclusionRule.Code, (await Reactions(c).UpdateAsync(id, Close(null, ["WASHED"]))).Error);
            Assert.Contains(ReactionConclusionRule.Code, (await Reactions(c).UpdateAsync(id, Close("FNHTR", null))).Error);
            Assert.Contains("not on the active", (await Reactions(c).UpdateAsync(id, Close("MADEUP", ["WASHED"]))).Error);
            Assert.Contains("cannot be chosen together", (await Reactions(c).UpdateAsync(id, Close("FNHTR", ["NONE", "WASHED"]))).Error);
            Assert.Equal(ReactionInvestigationStatus.Open, (await c.ReactionInvestigations.AsNoTracking().SingleAsync(r => r.Id == id)).Status);

            var closed = await Reactions(c).UpdateAsync(id, Close("fnhtr", ["WASHED"]));
            Assert.True(closed.Succeeded, closed.Error);
        }

        // Renaming the list entry later does not rewrite what the investigation recorded.
        await using (var c = _factory.Create())
        {
            Assert.True((await Admin(c).UpdateAsync(washedId, new SaveReactionCatalogEntryRequest(ReactionCatalogKind.Treatment, "WASHED", "Washed (renamed)", 10))).Succeeded);
        }

        await using var verify = _factory.Create();
        var row = await verify.ReactionInvestigations.SingleAsync(r => r.Id == id);
        Assert.Equal(ReactionInvestigationStatus.Closed, row.Status);
        Assert.Equal(("FNHTR", "Febrile non-hemolytic transfusion reaction"), (row.ConcludedReactionCode, row.ConcludedReactionText));
        Assert.Equal(("WASHED", "Washed cellular products"), (row.TreatmentCodes, row.TreatmentTexts));
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EntityType == nameof(ReactionInvestigation) && a.EntityId == id
            && a.NewValueJson!.Contains("\"ConcludedReactionCode\":\"FNHTR\"")));
    }

    [Fact]
    public async Task NoReaction_AndNoTreatment_CloseAnInvestigation()
    {
        var id = await SeedInvestigationAsync();
        await using var c = _factory.Create();
        var closed = await Reactions(c).UpdateAsync(id, Close("NONE", ["NONE"]));
        Assert.True(closed.Succeeded, closed.Error);
        Assert.Equal(("No reaction", "No treatment"), (closed.Value!.ConcludedReactionText, closed.Value.TreatmentTexts));
    }
}
