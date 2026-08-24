using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// Not a shared class fixture: charge rules are global matchers, so each test needs an
// isolated database to assert exact charge counts.
public class Phase7BillingTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private BillingService Billing(BloodBankDbContext c, CapturingPublisher? publisher = null) =>
        new(new EfRepository<BillingEvent>(c), new EfRepository<ChargeRule>(c), new EfRepository<ChargeCode>(c),
            new EfRepository<TestServiceBilling>(c), new EfRepository<ProductBilling>(c),
            new EfRepository<TestResult>(c), new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            publisher ?? new CapturingPublisher());

    private async Task<long> SeedChargeCodeAsync(BloodBankDbContext c, string code, decimal amount)
    {
        var chargeCode = new ChargeCode { Code = code, Description = code, DefaultAmount = amount };
        c.ChargeCodes.Add(chargeCode);
        await c.SaveChangesAsync();
        return chargeCode.Id;
    }

    private async Task<long> SeedChargeRuleAsync(BloodBankDbContext c, BillingTriggerType trigger, string? key, string code, decimal amount)
    {
        var chargeCodeId = await SeedChargeCodeAsync(c, code, amount);
        c.ChargeRules.Add(new ChargeRule { TriggerType = trigger, TriggerKey = key, ChargeCodeId = chargeCodeId });
        await c.SaveChangesAsync();
        return chargeCodeId;
    }

    private async Task<long> CreateVerifiedResultAsync(BloodBankDbContext c, string mrn, string testCode)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = mrn, LastName = "Bill", FirstName = "Test",
            DateOfBirth = new DateOnly(1981, 2, 3), Sex = Sex.Male
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{mrn}", PatientId = patient.Id, SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1), Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();

        var result = new TestResult
        {
            SpecimenId = specimen.Id, PatientId = patient.Id, TestCode = testCode, Value = "O POS",
            Status = ResultStatus.Verified, VerifiedBy = "tech", VerifiedUtc = _factory.Clock.UtcNow
        };
        c.TestResults.Add(result);
        await c.SaveChangesAsync();
        return result.Id;
    }

    private async Task<long> CreateIssuedUnitAsync(BloodBankDbContext c, string mrn, string productCode, string? isbtProductCode)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = mrn, LastName = "Bill", FirstName = "Issue",
            DateOfBirth = new DateOnly(1979, 6, 8), Sex = Sex.Female
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();

        var product = new ProductType
        {
            ProductCode = productCode,
            Name = productCode,
            Isbt128ProductCode = isbtProductCode
        };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = $"U-{mrn}",
            ProductTypeId = product.Id,
            ProductDescriptionCode = isbtProductCode,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Issued
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var issue = new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            IssuedUtc = _factory.Clock.UtcNow,
            IssuedBy = "tech"
        };
        c.Issues.Add(issue);
        await c.SaveChangesAsync();
        return issue.Id;
    }

    [Fact]
    public async Task CaptureForResult_CreatesPendingChargeFromMatchingRule()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            await SeedChargeRuleAsync(setup, BillingTriggerType.TestVerified, "ABORH", "BILL-ABORH-1", 35m);
            resultId = await CreateVerifiedResultAsync(setup, "BILL-100", "ABORH");
        }

        await using var context = _factory.Create();
        var result = await Billing(context).CaptureForResultAsync(resultId);

        Assert.True(result.Succeeded);
        var charge = Assert.Single(result.Value!);
        Assert.Equal(BillingEventStatus.Pending, charge.Status);
        Assert.Equal(35m, charge.Amount);
        Assert.Equal("BILL-ABORH-1", charge.BillingCode);
        Assert.Equal(BillingChargeSourceKind.ChargeRule, charge.SourceKind);
        Assert.Equal(nameof(TestResult), charge.TriggerEntityType);
    }

    [Fact]
    public async Task CaptureForResult_IsIdempotentOnRepeatedTrigger()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            await SeedChargeRuleAsync(setup, BillingTriggerType.TestVerified, "ABORH", "BILL-ABORH-2", 35m);
            resultId = await CreateVerifiedResultAsync(setup, "BILL-200", "ABORH");
        }

        await using (var c1 = _factory.Create())
        {
            await Billing(c1).CaptureForResultAsync(resultId);
        }

        await using (var c2 = _factory.Create())
        {
            var second = await Billing(c2).CaptureForResultAsync(resultId);
            Assert.True(second.Succeeded);
            Assert.Empty(second.Value!); // dedupe prevents a second charge
        }

        await using var verify = _factory.Create();
        var count = await verify.BillingEvents.CountAsync(e => e.TriggerEntityId == resultId && e.TriggerEntityType == nameof(TestResult));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CaptureForResult_NoMatchingRule_CreatesNothing()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            await SeedChargeRuleAsync(setup, BillingTriggerType.TestVerified, "ABORH", "BILL-ABORH-3", 35m);
            resultId = await CreateVerifiedResultAsync(setup, "BILL-300", "SOMETHING-ELSE");
        }

        await using var context = _factory.Create();
        var publisher = new CapturingPublisher();
        var result = await Billing(context, publisher).CaptureForResultAsync(resultId);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task CaptureForResult_CatchAllRule_MatchesAnyKey()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            await SeedChargeRuleAsync(setup, BillingTriggerType.TestVerified, null, "BILL-ANY-TEST", 10m);
            resultId = await CreateVerifiedResultAsync(setup, "BILL-350", "WHATEVER");
        }

        await using var context = _factory.Create();
        var result = await Billing(context).CaptureForResultAsync(resultId);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task CaptureForResult_TestCatalogMatch_CreatesChargeAndQueuesDft()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            var chargeCodeId = await SeedChargeCodeAsync(setup, "CAT-ABORH", 12.50m);
            setup.TestServiceBillings.Add(new TestServiceBilling
            {
                ChargeCodeId = chargeCodeId,
                TestCode = "ABORH",
                Trigger = BillingTriggerType.TestVerified
            });
            await setup.SaveChangesAsync();
            resultId = await CreateVerifiedResultAsync(setup, "BILL-CAT-1", "ABORH");
        }

        await using var context = _factory.Create();
        var publisher = new CapturingPublisher();
        var result = await Billing(context, publisher).CaptureForResultAsync(resultId);

        Assert.True(result.Succeeded);
        var charge = Assert.Single(result.Value!);
        Assert.Equal("CAT-ABORH", charge.BillingCode);
        Assert.Equal(12.50m, charge.Amount);
        Assert.Equal(BillingChargeSourceKind.TestService, charge.SourceKind);
        Assert.NotNull(charge.ChargeCodeId);
        Assert.Single(publisher.Published);
        Assert.Equal(charge.Id, publisher.Published[0].Id);
    }

    [Fact]
    public async Task CaptureForResult_CatalogSnapshotsChargeCodeAmount()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            var chargeCodeId = await SeedChargeCodeAsync(setup, "CAT-DAT", 18.00m);
            setup.TestServiceBillings.Add(new TestServiceBilling
            {
                ChargeCodeId = chargeCodeId,
                TestCode = "DAT",
                Trigger = BillingTriggerType.TestVerified
            });
            await setup.SaveChangesAsync();
            resultId = await CreateVerifiedResultAsync(setup, "BILL-CAT-2", "DAT");
        }

        await using var context = _factory.Create();
        var publisher = new CapturingPublisher();
        var result = await Billing(context, publisher).CaptureForResultAsync(resultId);

        Assert.True(result.Succeeded);
        var charge = Assert.Single(result.Value!);
        Assert.Equal(18.00m, charge.Amount);
        Assert.Equal("CAT-DAT", charge.BillingCode);
        Assert.Single(publisher.Published);
    }

    [Fact]
    public async Task CaptureForResult_ChargeRuleAndCatalog_BothDrop()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            await SeedChargeRuleAsync(setup, BillingTriggerType.TestVerified, "ABORH", "RULE-ABORH", 35m);
            var catalogCodeId = await SeedChargeCodeAsync(setup, "CAT-ABORH-2", 40m);
            setup.TestServiceBillings.Add(new TestServiceBilling
            {
                ChargeCodeId = catalogCodeId,
                TestCode = "ABORH",
                Trigger = BillingTriggerType.TestVerified
            });
            await setup.SaveChangesAsync();
            resultId = await CreateVerifiedResultAsync(setup, "BILL-BOTH", "ABORH");
        }

        await using var context = _factory.Create();
        var publisher = new CapturingPublisher();
        var result = await Billing(context, publisher).CaptureForResultAsync(resultId);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, e => e.SourceKind == BillingChargeSourceKind.ChargeRule && e.BillingCode == "RULE-ABORH");
        Assert.Contains(result.Value, e => e.SourceKind == BillingChargeSourceKind.TestService && e.BillingCode == "CAT-ABORH-2");
        Assert.Equal(2, publisher.Published.Count);
    }

    [Fact]
    public async Task CaptureForIssue_ProductCatalogMatchesIsbtNotInternalCode()
    {
        long issueId;
        await using (var setup = _factory.Create())
        {
            var chargeCodeId = await SeedChargeCodeAsync(setup, "CAT-E0336", 250m);
            setup.ProductBillings.Add(new ProductBilling
            {
                ChargeCodeId = chargeCodeId,
                IsbtProductCode = "E0336",
                Trigger = BillingTriggerType.UnitIssued
            });
            await setup.SaveChangesAsync();
            issueId = await CreateIssuedUnitAsync(setup, "BILL-ISSUE-1", "RBC-LR", "E0336");
        }

        await using var context = _factory.Create();
        var publisher = new CapturingPublisher();
        var result = await Billing(context, publisher).CaptureForIssueAsync(issueId);

        Assert.True(result.Succeeded);
        var charge = Assert.Single(result.Value!);
        Assert.Equal("CAT-E0336", charge.BillingCode);
        Assert.Equal(BillingChargeSourceKind.Product, charge.SourceKind);
        Assert.Equal(250m, charge.Amount);
        Assert.Single(publisher.Published);
    }

    [Fact]
    public async Task CaptureForIssue_ChargeRuleStillMatchesInternalProductCode()
    {
        long issueId;
        await using (var setup = _factory.Create())
        {
            await SeedChargeRuleAsync(setup, BillingTriggerType.UnitIssued, "RBC-LR", "RULE-RBC", 200m);
            issueId = await CreateIssuedUnitAsync(setup, "BILL-ISSUE-2", "RBC-LR", "E0336");
        }

        await using var context = _factory.Create();
        var result = await Billing(context).CaptureForIssueAsync(issueId);

        Assert.True(result.Succeeded);
        var charge = Assert.Single(result.Value!);
        Assert.Equal("RULE-RBC", charge.BillingCode);
        Assert.Equal(BillingChargeSourceKind.ChargeRule, charge.SourceKind);
    }

    [Fact]
    public async Task CaptureForIssue_NoIsbtMatch_CreatesNothingFromCatalog()
    {
        long issueId;
        await using (var setup = _factory.Create())
        {
            var chargeCodeId = await SeedChargeCodeAsync(setup, "CAT-E9999", 10m);
            setup.ProductBillings.Add(new ProductBilling
            {
                ChargeCodeId = chargeCodeId,
                IsbtProductCode = "E9999",
                Trigger = BillingTriggerType.UnitIssued
            });
            await setup.SaveChangesAsync();
            issueId = await CreateIssuedUnitAsync(setup, "BILL-ISSUE-3", "FFP", "E0701");
        }

        await using var context = _factory.Create();
        var publisher = new CapturingPublisher();
        var result = await Billing(context, publisher).CaptureForIssueAsync(issueId);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task CaptureForResult_TestCatalog_IsIdempotent()
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            var chargeCodeId = await SeedChargeCodeAsync(setup, "CAT-IDEM", 20m);
            setup.TestServiceBillings.Add(new TestServiceBilling
            {
                ChargeCodeId = chargeCodeId,
                TestCode = "ABSC",
                Trigger = BillingTriggerType.TestVerified
            });
            await setup.SaveChangesAsync();
            resultId = await CreateVerifiedResultAsync(setup, "BILL-IDEM", "ABSC");
        }

        await using (var c1 = _factory.Create())
        {
            var first = await Billing(c1).CaptureForResultAsync(resultId);
            Assert.Single(first.Value!);
        }

        await using (var c2 = _factory.Create())
        {
            var second = await Billing(c2).CaptureForResultAsync(resultId);
            Assert.Empty(second.Value!);
        }
    }

    [Fact]
    public async Task ReviewThenExport_AdvancesStatus()
    {
        long chargeId = await CaptureSingleChargeAsync("BILL-400", "ABORH", "BILL-ABORH-4");

        await using var context = _factory.Create();
        var billing = Billing(context);

        var reviewed = await billing.ReviewAsync(chargeId);
        Assert.True(reviewed.Succeeded);
        Assert.Equal(BillingEventStatus.Reviewed, reviewed.Value!.Status);
        Assert.Equal(_factory.CurrentUser.UserName, reviewed.Value.ReviewedBy);

        var exported = await billing.ExportAsync(chargeId);
        Assert.True(exported.Succeeded);
        Assert.Equal(BillingEventStatus.Exported, exported.Value!.Status);
        Assert.NotNull(exported.Value.ExportedUtc);
    }

    [Fact]
    public async Task Cancel_RequiresReason_AndWritesAuditEvent()
    {
        long chargeId = await CaptureSingleChargeAsync("BILL-500", "ABORH", "BILL-ABORH-5");

        await using (var context = _factory.Create())
        {
            var noReason = await Billing(context).CancelAsync(chargeId, "  ");
            Assert.False(noReason.Succeeded);
        }

        await using (var context = _factory.Create())
        {
            var cancelled = await Billing(context).CancelAsync(chargeId, "Duplicate order entered in error");
            Assert.True(cancelled.Succeeded);
            Assert.Equal(BillingEventStatus.Cancelled, cancelled.Value!.Status);
        }

        await using var verify = _factory.Create();
        var auditEvent = await verify.AuditEvents.FirstOrDefaultAsync(
            e => e.EntityType == nameof(BillingEvent) && e.EntityId == chargeId && e.Reason != null);
        Assert.NotNull(auditEvent);
        Assert.Contains("Duplicate", auditEvent!.Reason!);
    }

    [Fact]
    public async Task Review_NonPendingCharge_IsRejected()
    {
        long chargeId = await CaptureSingleChargeAsync("BILL-600", "ABORH", "BILL-ABORH-6");

        await using var context = _factory.Create();
        var billing = Billing(context);
        await billing.ReviewAsync(chargeId);

        var second = await billing.ReviewAsync(chargeId);
        Assert.False(second.Succeeded);
    }

    private async Task<long> CaptureSingleChargeAsync(string mrn, string testCode, string chargeCode)
    {
        long resultId;
        await using (var setup = _factory.Create())
        {
            await SeedChargeRuleAsync(setup, BillingTriggerType.TestVerified, testCode, chargeCode, 35m);
            resultId = await CreateVerifiedResultAsync(setup, mrn, testCode);
        }

        await using var context = _factory.Create();
        var result = await Billing(context).CaptureForResultAsync(resultId);
        return Assert.Single(result.Value!).Id;
    }

    private sealed class CapturingPublisher : IBillingInterfacePublisher
    {
        public List<BillingEvent> Published { get; } = new();

        public Task<long?> PublishChargeAsync(BillingEvent billingEvent, CancellationToken ct = default)
        {
            Published.Add(billingEvent);
            return Task.FromResult<long?>(null);
        }
    }
}
