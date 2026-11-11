using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class InterfaceTransfusionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InterfaceTransfusionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private InterfaceTransfusionService Service(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        return new InterfaceTransfusionService(
            new EfRepository<Patient>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<Issue>(c),
            new EfRepository<TransfusionEvent>(c),
            new InventoryRepository(c),
            new ReactionInvestigationService(
                new EfRepository<ReactionInvestigation>(c),
                new EfRepository<TransfusionEvent>(c),
                new InventoryRepository(c),
                c,
                _factory.Clock,
                _factory.CurrentUser,
                audit),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            permissions: permissions);
    }

    private static InterfaceTransfusionRequest DummyRequest() =>
        new("MRN-IFACE-PERM", "UNIT-IFACE", null, null, null, null, null, null, false);

    [Fact]
    public async Task DocumentAsync_WithoutTransfusionDocument_IsRejected()
    {
        await using var c = _factory.Create();
        var request = DummyRequest();
        // The class shares one database; another test records a transfusion, so count rather than expect none.
        var before = await c.TransfusionEvents.CountAsync();

        var denied = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
            .DocumentAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains("transfusion.document", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await c.TransfusionEvents.CountAsync());

        var allowed = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.TransfusionDocument))
            .DocumentAsync(request);
        Assert.False(allowed.Succeeded);
        Assert.DoesNotContain("transfusion.document", allowed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No patient found", allowed.Error, StringComparison.OrdinalIgnoreCase);
    }

    // TEST-BB-929 (Cycle 695): a transfusion message for a unit the blood bank recalled (or quarantined, or recorded as
    // damaged or missing) after it was issued is held for review, naming the status and the recall, and records nothing;
    // the same message for a unit still issued is recorded as before.
    [Theory]
    [InlineData(UnitStatus.Recalled)]
    [InlineData(UnitStatus.Quarantine)]
    [InlineData(UnitStatus.Damaged)]
    [InlineData(UnitStatus.Issued)]
    public async Task Document_UnitNoLongerIssued_IsHeldForReview(UnitStatus status)
    {
        var key = $"{status}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        long unitId, issueId;
        await using (var c = _factory.Create())
        {
            var productType = new ProductType { ProductCode = $"RBC-IF-{key}"[..20], Name = "Interface RBC", ComponentClass = ComponentClass.RedBloodCells };
            var patient = new Patient { MedicalRecordNumber = $"MRN-{key}", LastName = "Interface", FirstName = "Held", DateOfBirth = new DateOnly(1970, 1, 1) };
            c.ProductTypes.Add(productType);
            c.Patients.Add(patient);
            await c.SaveChangesAsync();
            var unit = new BloodUnit
            {
                UnitNumber = $"U-{key}", ProductTypeId = productType.Id, Abo = AboGroup.O, RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10), Status = status,
                RecallReason = status == UnitStatus.Recalled ? "Donor subsequently reactive" : null
            };
            c.BloodUnits.Add(unit);
            await c.SaveChangesAsync();
            var issue = new Issue
            {
                BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddHours(-2),
                IssuedBy = "tech-test", Status = IssueStatus.Issued
            };
            c.Issues.Add(issue);
            await c.SaveChangesAsync();
            (unitId, issueId) = (unit.Id, issue.Id);
        }

        await using (var c = _factory.Create())
        {
            var result = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.TransfusionDocument)).DocumentAsync(
                new InterfaceTransfusionRequest($"MRN-{key}", $"U-{key}", null, _factory.Clock.UtcNow.AddHours(-1), _factory.Clock.UtcNow,
                    300m, "4W", "RN Smith", false));
            if (status == UnitStatus.Issued)
            {
                Assert.True(result.Succeeded, result.Error);
            }
            else
            {
                Assert.False(result.Succeeded);
                Assert.Contains($"is {status} in the blood bank", result.Error);
                Assert.Contains("held for review and not recorded (OCD-083)", result.Error);
                if (status == UnitStatus.Recalled)
                {
                    Assert.Contains("(recalled: Donor subsequently reactive)", result.Error);
                }
            }
        }

        await using var verify = _factory.Create();
        var after = (await verify.BloodUnits.FindAsync(unitId))!;
        var issueAfter = (await verify.Issues.FindAsync(issueId))!;
        if (status == UnitStatus.Issued)
        {
            Assert.Equal((UnitStatus.Transfused, IssueStatus.Transfused), (after.Status, issueAfter.Status));
        }
        else
        {
            Assert.Equal((status, IssueStatus.Issued), (after.Status, issueAfter.Status));
            Assert.False(await verify.TransfusionEvents.AnyAsync(t => t.IssueId == issueId));
        }
    }

    [Fact]
    public async Task DocumentFromHl7Async_DoesNotRequireTransfusionDocument()
    {
        await using var c = _factory.Create();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
                .DocumentFromHl7Async(DummyRequest()));
        Assert.Contains("No patient found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
