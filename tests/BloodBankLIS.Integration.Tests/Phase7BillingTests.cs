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

    private BillingService Billing(BloodBankDbContext c) =>
        new(new EfRepository<BillingEvent>(c), new EfRepository<ChargeRule>(c), new EfRepository<ChargeCode>(c),
            new EfRepository<TestResult>(c), new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private async Task<long> SeedChargeRuleAsync(BloodBankDbContext c, BillingTriggerType trigger, string? key, string code, decimal amount)
    {
        var chargeCode = new ChargeCode { Code = code, Description = code, DefaultAmount = amount };
        c.ChargeCodes.Add(chargeCode);
        await c.SaveChangesAsync();
        c.ChargeRules.Add(new ChargeRule { TriggerType = trigger, TriggerKey = key, ChargeCodeId = chargeCode.Id });
        await c.SaveChangesAsync();
        return chargeCode.Id;
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
        var result = await Billing(context).CaptureForResultAsync(resultId);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
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
}
