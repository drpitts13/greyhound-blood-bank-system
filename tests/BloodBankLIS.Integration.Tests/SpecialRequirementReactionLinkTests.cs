using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-403: a special requirement added from a reaction investigation is linked to it, for that patient only.
public class SpecialRequirementReactionLinkTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private SpecialRequirementService Service(BloodBankDbContext c) =>
        new(new EfRepository<SpecialTransfusionRequirement>(c), new EfRepository<Patient>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            definitions: new EfRepository<SpecialRequirementDefinition>(c),
            reactions: new EfRepository<ReactionInvestigation>(c));

    private async Task<(long PatientId, long OtherPatientId, long ReactionId)> SeedAsync()
    {
        await using var c = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = "MRN-SRRX", LastName = "Reaction", FirstName = "Link", DateOfBirth = new DateOnly(1970, 1, 1) };
        var other = new Patient { MedicalRecordNumber = "MRN-SRRX-2", LastName = "Other", FirstName = "Patient", DateOfBirth = new DateOnly(1971, 1, 1) };
        c.Patients.AddRange(patient, other);
        var product = new ProductType { ProductCode = "RBC-SRRX", Name = "Test RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        var unit = new BloodUnit
        {
            UnitNumber = "U-SRRX", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10), Status = UnitStatus.Transfused
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        var issue = new Issue
        {
            BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddHours(-6),
            IssuedBy = "tech-test", Status = IssueStatus.Transfused
        };
        c.Issues.Add(issue);
        await c.SaveChangesAsync();
        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id, BloodProductId = unit.Id, PatientId = patient.Id,
            StartUtc = _factory.Clock.UtcNow.AddHours(-5), StopUtc = _factory.Clock.UtcNow.AddHours(-4),
            FinalDisposition = TransfusionDisposition.Completed, ReactionSuspected = true, DocumentedBy = "tech-test"
        };
        c.TransfusionEvents.Add(transfusion);
        await c.SaveChangesAsync();
        var reaction = new ReactionInvestigation
        {
            PatientId = patient.Id,
            TransfusionEventId = transfusion.Id,
            BloodProductId = unit.Id,
            ReportedUtc = _factory.Clock.UtcNow,
            ReactionType = "Severe allergic",
            Status = ReactionInvestigationStatus.Closed
        };
        c.ReactionInvestigations.Add(reaction);
        await c.SaveChangesAsync();
        return (patient.Id, other.Id, reaction.Id);
    }

    [Fact]
    public async Task Requirement_AddedFromAReaction_IsLinked_AndAudited()
    {
        var (patientId, _, reactionId) = await SeedAsync();
        await using var c = _factory.Create();
        var added = await Service(c).AddAsync(patientId, new AddSpecialRequirementRequest(
            SpecialTransfusionRequirementType.Washed, $"After reaction investigation {reactionId}: severe allergic reaction",
            ReactionInvestigationId: reactionId));
        Assert.True(added.Succeeded, added.Error);
        Assert.Equal(reactionId, added.Value!.ReactionInvestigationId);

        var dto = Assert.Single(await Service(c).ListDtosAsync(patientId));
        Assert.Equal(reactionId, dto.ReactionInvestigationId);
        var audit = await c.AuditEvents.SingleAsync(a => a.EntityType == nameof(SpecialTransfusionRequirement) && a.EntityId == added.Value.Id && a.EventType == AuditEventType.Antibody);
        Assert.Contains($"\"ReactionInvestigationId\":{reactionId}", audit.NewValueJson);
    }

    [Fact]
    public async Task Requirement_CannotLinkAnotherPatientsReaction_OrOneThatDoesNotExist()
    {
        var (_, otherId, reactionId) = await SeedAsync();
        await using var c = _factory.Create();
        var wrongPatient = await Service(c).AddAsync(otherId, new AddSpecialRequirementRequest(
            SpecialTransfusionRequirementType.Washed, "Copied by mistake", ReactionInvestigationId: reactionId));
        Assert.False(wrongPatient.Succeeded);
        Assert.Contains("not one of this patient's investigations", wrongPatient.Error);

        var missing = await Service(c).AddAsync(otherId, new AddSpecialRequirementRequest(
            SpecialTransfusionRequirementType.Washed, "No such case", ReactionInvestigationId: 999999));
        Assert.False(missing.Succeeded);
        Assert.False(await c.SpecialTransfusionRequirements.AnyAsync(r => r.PatientId == otherId));
    }
}
