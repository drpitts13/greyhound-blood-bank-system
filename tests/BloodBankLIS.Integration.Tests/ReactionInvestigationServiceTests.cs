using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ReactionInvestigationServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ReactionInvestigationServiceTests(SqliteContextFactory factory) => _factory = factory;

    private ReactionInvestigationService CreateService(
        BloodBankDbContext context,
        IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<ReactionInvestigation>(context),
            new EfRepository<TransfusionEvent>(context),
            new InventoryRepository(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: permissions);

    [Fact]
    public async Task Update_WithoutReactionInvestigate_IsHardStopped()
    {
        await using var context = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-RXN-PERM",
            LastName = "React",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1970, 1, 1)
        };
        var product = new ProductType { ProductCode = "RBC-RXN-PERM", Name = "RBC" };
        context.Patients.Add(patient);
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = "U-RXN-PERM",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Status = UnitStatus.Issued,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            IssuedUtc = _factory.Clock.UtcNow,
            IssuedBy = "tech-test",
            Status = IssueStatus.Issued
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            ReactionSuspected = true,
            FinalDisposition = TransfusionDisposition.Completed,
            DocumentedBy = "tech-test"
        };
        context.TransfusionEvents.Add(transfusion);
        await context.SaveChangesAsync();

        var investigation = new ReactionInvestigation
        {
            TransfusionEventId = transfusion.Id,
            PatientId = patient.Id,
            BloodProductId = unit.Id,
            ReportedUtc = _factory.Clock.UtcNow,
            ReportedBy = "tech-test",
            Status = ReactionInvestigationStatus.Open
        };
        context.ReactionInvestigations.Add(investigation);
        await context.SaveChangesAsync();

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .UpdateAsync(investigation.Id, new UpdateReactionInvestigationRequest(
                "FNHTR", ReactionSeverity.Mild, null, null, null, null, null, null, null));
        Assert.False(denied.Succeeded);
        Assert.Equal(ReactionAuthorizationRule.EvaluateInvestigate(false).Message, denied.Error);
        Assert.Null((await context.ReactionInvestigations.FindAsync(investigation.Id))!.ReactionType);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.ReactionInvestigate))
            .UpdateAsync(investigation.Id, new UpdateReactionInvestigationRequest(
                "FNHTR", ReactionSeverity.Mild, null, null, null, null, null, null, null));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("FNHTR", allowed.Value!.ReactionType);

        var cberDenied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .RecordCberNotificationAsync(investigation.Id);
        Assert.False(cberDenied.Succeeded);
        Assert.Equal(ReactionAuthorizationRule.EvaluateInvestigate(false).Message, cberDenied.Error);
    }
}
