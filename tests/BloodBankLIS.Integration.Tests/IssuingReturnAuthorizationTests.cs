using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class IssuingReturnAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public IssuingReturnAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private IssuingService CreateService(BloodBankDbContext context, IPermissionEvaluator permissions)
    {
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser);
        return new IssuingService(
            inventory: new InventoryRepository(context),
            issues: new EfRepository<Issue>(context),
            allocations: new EfRepository<Allocation>(context),
            crossmatches: new EfRepository<Crossmatch>(context),
            returns: new EfRepository<Return>(context),
            transfusions: new EfRepository<TransfusionEvent>(context),
            overrides: new EfRepository<Override>(context),
            patients: new EfRepository<Patient>(context),
            specimens: new EfRepository<Specimen>(context),
            productTypes: new EfRepository<ProductType>(context),
            bloodTypes: new EfRepository<PatientBloodTypeHistory>(context),
            exceptionDefinitions: new EfRepository<ExceptionDefinition>(context),
            specialRequirements: new EfRepository<SpecialTransfusionRequirement>(context),
            productAttributes: new EfRepository<ProductAttribute>(context),
            productAttributeAssignments: new EfRepository<ProductAttributeAssignment>(context),
            orders: new EfRepository<Order>(context),
            users: new EfRepository<User>(context),
            bloodAttributeCompat: new BloodAttributeCompatLoader(
                new EfRepository<AntibodyHistory>(context),
                new EfRepository<AntigenProfile>(context),
                new EfRepository<UnitBloodAttribute>(context),
                new EfRepository<BloodAttributeDefinition>(context)),
            policy: new FacilityPolicyService(new EfRepository<SystemSetting>(context)),
            reactions: new ReactionInvestigationService(
                new EfRepository<ReactionInvestigation>(context),
                new EfRepository<TransfusionEvent>(context),
                new InventoryRepository(context),
                context, _factory.Clock, _factory.CurrentUser, audit),
            permissions: permissions,
            unitOfWork: context,
            clock: _factory.Clock,
            currentUser: _factory.CurrentUser,
            audit: audit);
    }

    [Fact]
    public async Task Return_WithoutIssueReturn_IsHardStopped()
    {
        await using var context = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-RET-PERM",
            LastName = "Return",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1970, 1, 1)
        };
        var product = new ProductType { ProductCode = "RBC-RET-PERM", Name = "RBC" };
        context.Patients.Add(patient);
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = "U-RET-PERM",
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

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
            .ReturnUnitAsync(issue.Id, new ReturnUnitRequest("Not needed; cooler intact"));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == IssueAuthorizationRule.ReturnCode);
        Assert.Equal(UnitStatus.Issued, (await context.BloodUnits.FindAsync(unit.Id))!.Status);
        Assert.Equal(IssueStatus.Issued, (await context.Issues.FindAsync(issue.Id))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.IssueReturn))
            .ReturnUnitAsync(issue.Id, new ReturnUnitRequest("Not needed; cooler intact"));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(unit.Id))!.Status);
        Assert.Equal(IssueStatus.Returned, (await context.Issues.FindAsync(issue.Id))!.Status);
    }
}
