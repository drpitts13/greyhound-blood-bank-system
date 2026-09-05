using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AdminCatalogTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AdminCatalogTests(SqliteContextFactory factory) => _factory = factory;

    private OrderingProviderAdminService CreateProviderService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new OrderingProviderAdminService(
            new EfRepository<OrderingProvider>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private BloodAttributeAdminService CreateBloodAttributeService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new BloodAttributeAdminService(
            new EfRepository<BloodAttributeDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private SpecimenTypeAdminService CreateSpecimenTypeService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new SpecimenTypeAdminService(
            new EfRepository<SpecimenTypeDefinition>(context),
            new EfRepository<TestDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private OrderingLocationAdminService CreateLocationService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new OrderingLocationAdminService(
            new EfRepository<OrderingLocation>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private ExceptionDefinitionAdminService CreateExceptionService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new ExceptionDefinitionAdminService(
            new EfRepository<ExceptionDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private PhaseDefinitionAdminService CreatePhaseService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new PhaseDefinitionAdminService(
            new EfRepository<PhaseDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    [Fact]
    public async Task CreateProvider_PersistsAndLists()
    {
        await using var c = _factory.Create();
        var svc = CreateProviderService(c);

        var result = await svc.CreateAsync(new SaveOrderingProviderRequest("PROV-NEW", "Dr. New", null, null));
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal("PROV-NEW", result.Value!.ProviderId);

        var row = await c.OrderingProviders.FirstOrDefaultAsync(p => p.ProviderId == "PROV-NEW");
        Assert.NotNull(row);
        Assert.Equal("Dr. New", row!.Name);
    }

    [Fact]
    public async Task CreateBloodAttribute_DraftActivate_BlocksDuplicateCode()
    {
        await using var c = _factory.Create();
        var svc = CreateBloodAttributeService(c);

        var first = await svc.CreateAsync(new SaveBloodAttributeDefinitionRequest("ZZK", "Test Kell", "anti-ZZK", true, 1, null));
        var second = await svc.CreateAsync(new SaveBloodAttributeDefinitionRequest("ZZK", "Test Kell v2", "anti-ZZK", true, 2, null));
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(BloodAttributeDefinition) && a.EntityId == first.Value!.Id && a.EventType == AuditEventType.TestChange));

        var activated = await svc.ActivateAsync(first.Value!.Id, "Initial activation");
        Assert.True(activated.Succeeded);

        var dupActivate = await svc.ActivateAsync(second.Value!.Id, "Try duplicate");
        Assert.False(dupActivate.Succeeded);
        Assert.True(dupActivate.Evaluation!.IsHardStopped);
    }

    [Fact]
    public async Task CreateSpecimenType_WithExcludedTest_Activates()
    {
        await using var c = _factory.Create();
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABSC",
            Name = "Antibody Screen",
            ResultValueType = ResultValueType.Coded,
            AllowedResultValues = "Negative\nPositive",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = DateTime.UtcNow,
            Version = 1
        });
        await c.SaveChangesAsync();

        var svc = CreateSpecimenTypeService(c);
        var created = await svc.CreateAsync(new SaveSpecimenTypeDefinitionRequest(
            "PLASMA", "Plasma", ["ABSC"], 3, null));
        Assert.True(created.Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(SpecimenTypeDefinition)
            && a.EntityId == created.Value!.Id
            && a.EventType == AuditEventType.TestChange));

        var activated = await svc.ActivateAsync(created.Value!.Id, "Initial");
        Assert.True(activated.Succeeded);
        Assert.Contains("ABSC", activated.Value!.ExcludedTestCodes);
    }

    [Fact]
    public async Task SpecimenType_CreateAndUpdate_WriteTestChange()
    {
        await using var c = _factory.Create();
        var testCode = "ABSC-RA13-" + Guid.NewGuid().ToString("N")[..6];
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = testCode,
            Name = "Antibody Screen",
            ResultValueType = ResultValueType.Coded,
            AllowedResultValues = "Negative\nPositive",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = DateTime.UtcNow,
            Version = 1
        });
        await c.SaveChangesAsync();

        var svc = CreateSpecimenTypeService(c);
        var code = "ST-RA13-" + Guid.NewGuid().ToString("N")[..8];
        var created = await svc.CreateAsync(new SaveSpecimenTypeDefinitionRequest(
            code, "Result-audit specimen type", [testCode], 90, "draft type"));
        Assert.True(created.Succeeded, created.Error ?? created.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var updated = await svc.UpdateAsync(created.Value!.Id, new SaveSpecimenTypeDefinitionRequest(
            code, "Result-audit specimen type renamed", [testCode], 91, "rename type"));
        Assert.True(updated.Succeeded);

        var events = await c.AuditEvents
            .Where(a => a.EntityType == nameof(SpecimenTypeDefinition) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, events.Count(a => a.EventType == AuditEventType.TestChange));
    }

    [Fact]
    public async Task CreateLocation_CodeOnly_UsesCodeAsName()
    {
        await using var c = _factory.Create();
        var svc = CreateLocationService(c);

        var result = await svc.CreateAsync(new SaveOrderingLocationRequest("WARD-9", null, "Med/Surg", null));
        Assert.True(result.Succeeded);

        var row = await c.OrderingLocations.FirstAsync(l => l.Code == "WARD-9");
        Assert.Equal("WARD-9", row.Name);
        Assert.Equal("Med/Surg", row.Department);
    }

    private ChargeCodeAdminService CreateChargeCodeService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new ChargeCodeAdminService(
            new EfRepository<ChargeCode>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private ChargeRuleAdminService CreateChargeRuleService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new ChargeRuleAdminService(
            new EfRepository<ChargeRule>(context),
            new EfRepository<ChargeCode>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    [Fact]
    public async Task CreateChargeCode_PersistsListsAndWritesHistory()
    {
        long id;
        await using (var c = _factory.Create())
        {
            var svc = CreateChargeCodeService(c);
            var result = await svc.CreateAsync(new SaveChargeCodeRequest("bb-new", "New charge", 12.5m, "86999"));
            Assert.True(result.Succeeded);
            Assert.Equal("BB-NEW", result.Value!.Code);
            Assert.Equal(12.5m, result.Value.DefaultAmount);
            Assert.True(result.Value.IsActive);
            id = result.Value.Id;
        }

        await using (var verify = _factory.Create())
        {
            var row = await verify.ChargeCodes.SingleAsync(c => c.Id == id);
            Assert.Equal("BB-NEW", row.Code);
            Assert.Equal("86999", row.CptCode);

            var history = await verify.ConfigurationChangeHistory
                .SingleAsync(h => h.EntityType == nameof(ChargeCode) && h.EntityId == id);
            Assert.Equal(ConfigChangeAction.Create, history.Action);
            Assert.Contains("BB-NEW", history.NewValueJson!);

            var listed = await CreateChargeCodeService(verify).ListAsync(includeInactive: false);
            Assert.Contains(listed, c => c.Code == "BB-NEW");
        }
    }

    [Fact]
    public async Task CreateChargeCode_BlocksDuplicateAndNegativeAmount()
    {
        await using var c = _factory.Create();
        var svc = CreateChargeCodeService(c);

        var first = await svc.CreateAsync(new SaveChargeCodeRequest("BB-DUP", "One", 1m, null));
        Assert.True(first.Succeeded);

        var dup = await svc.CreateAsync(new SaveChargeCodeRequest("bb-dup", "Two", 2m, null));
        Assert.False(dup.Succeeded);
        Assert.True(dup.Evaluation!.IsHardStopped);
        Assert.Contains(dup.Evaluation.HardStops, r => r.Code == "CHARGE.CODE.DUPLICATE");

        var negative = await svc.CreateAsync(new SaveChargeCodeRequest("BB-NEG", "Neg", -5m, null));
        Assert.False(negative.Succeeded);
        Assert.Contains(negative.Evaluation!.HardStops, r => r.Code == "CHARGE.AMOUNT.NEGATIVE");
    }

    [Fact]
    public async Task ChargeCode_DeactivateAndActivate()
    {
        await using var c = _factory.Create();
        var svc = CreateChargeCodeService(c);
        var created = await svc.CreateAsync(new SaveChargeCodeRequest("BB-OFF", "Off", 1m, null));
        Assert.True(created.Succeeded);

        var off = await svc.SetActiveAsync(created.Value!.Id, false);
        Assert.True(off.Succeeded);
        Assert.False(off.Value!.IsActive);

        var activeOnly = await svc.ListAsync(includeInactive: false);
        Assert.DoesNotContain(activeOnly, x => x.Code == "BB-OFF");

        var on = await svc.SetActiveAsync(created.Value.Id, true);
        Assert.True(on.Succeeded);
        Assert.True(on.Value!.IsActive);
    }

    [Fact]
    public async Task CreateChargeRule_MapsCodeAndBlocksMissingOrDuplicate()
    {
        await using var c = _factory.Create();
        var codes = CreateChargeCodeService(c);
        var rules = CreateChargeRuleService(c);

        var code = await codes.CreateAsync(new SaveChargeCodeRequest("BB-RULE", "Rule charge", 10m, null));
        Assert.True(code.Succeeded);

        var missing = await rules.CreateAsync(new SaveChargeRuleRequest(BillingTriggerType.TestVerified, "ABORH", 99999));
        Assert.False(missing.Succeeded);
        Assert.Contains(missing.Evaluation!.HardStops, r => r.Code == "CHARGE.RULE.CODE.REQUIRED");

        var created = await rules.CreateAsync(new SaveChargeRuleRequest(BillingTriggerType.TestVerified, "ABORH", code.Value!.Id));
        Assert.True(created.Succeeded);
        Assert.Equal("BB-RULE", created.Value!.ChargeCode);
        Assert.Equal("ABORH", created.Value.TriggerKey);

        var dup = await rules.CreateAsync(new SaveChargeRuleRequest(BillingTriggerType.TestVerified, "ABORH", code.Value.Id));
        Assert.False(dup.Succeeded);
        Assert.Contains(dup.Evaluation!.HardStops, r => r.Code == "CHARGE.RULE.DUPLICATE");

        var catchAll = await rules.CreateAsync(new SaveChargeRuleRequest(BillingTriggerType.TestVerified, null, code.Value.Id));
        Assert.True(catchAll.Succeeded);
        Assert.Null(catchAll.Value!.TriggerKey);

        var history = await c.ConfigurationChangeHistory
            .Where(h => h.EntityType == nameof(ChargeRule) && h.EntityId == created.Value.Id)
            .SingleAsync();
        Assert.Equal(ConfigChangeAction.Create, history.Action);
    }

    [Fact]
    public async Task CreateChargeCode_PersistsRevenueAndModifier()
    {
        await using var c = _factory.Create();
        var svc = CreateChargeCodeService(c);
        var result = await svc.CreateAsync(new SaveChargeCodeRequest("BB-REV", "RBC issue", 250m, "P9021", "0381", "BL"));
        Assert.True(result.Succeeded);
        Assert.Equal("0381", result.Value!.RevenueCode);
        Assert.Equal("BL", result.Value.Modifier);
        Assert.Equal("P9021", result.Value.CptCode);
    }

    private InventoryLocationAdminService CreateInventoryLocationService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new InventoryLocationAdminService(
            new EfRepository<InventoryLocation>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    [Fact]
    public async Task CreateInventoryLocation_AppliesSatelliteDefaultsAndWritesHistory()
    {
        long id;
        await using (var c = _factory.Create())
        {
            var svc = CreateInventoryLocationService(c);
            var result = await svc.CreateAsync(new SaveInventoryLocationRequest(
                "or-fridge-2", "OR Satellite 2", LocationType.SatelliteRefrigerator, ApplyTypeDefaults: true));
            Assert.True(result.Succeeded);
            Assert.Equal("OR-FRIDGE-2", result.Value!.Code);
            Assert.True(result.Value.IsSatellite);
            Assert.True(result.Value.AllowsRemoteIssue);
            Assert.False(result.Value.AllowsIssue);
            Assert.True(result.Value.AllowsRbc);
            Assert.False(result.Value.AllowsPlatelets);
            id = result.Value.Id;
        }

        await using (var verify = _factory.Create())
        {
            var row = await verify.InventoryLocations.SingleAsync(l => l.Id == id);
            Assert.Equal(LocationType.SatelliteRefrigerator, row.LocationType);
            Assert.True(row.AllowsRemoteIssue);

            var history = await verify.ConfigurationChangeHistory
                .SingleAsync(h => h.EntityType == nameof(InventoryLocation) && h.EntityId == id);
            Assert.Equal(ConfigChangeAction.Create, history.Action);
        }
    }

    [Fact]
    public async Task CreateInventoryLocation_BlocksDuplicateAndBadTemperatureRange()
    {
        await using var c = _factory.Create();
        var svc = CreateInventoryLocationService(c);

        var first = await svc.CreateAsync(new SaveInventoryLocationRequest("BB-FR", "Fridge", LocationType.Refrigerator));
        Assert.True(first.Succeeded);

        var dup = await svc.CreateAsync(new SaveInventoryLocationRequest("bb-fr", "Other", LocationType.Refrigerator));
        Assert.False(dup.Succeeded);
        Assert.Contains(dup.Evaluation!.HardStops, r => r.Code == "INVLOC.CODE.DUPLICATE");

        var badTemp = await svc.CreateAsync(new SaveInventoryLocationRequest(
            "BB-HOT", "Bad range", LocationType.Refrigerator, StorageTempMinC: 10, StorageTempMaxC: 1));
        Assert.False(badTemp.Succeeded);
        Assert.Contains(badTemp.Evaluation!.HardStops, r => r.Code == "INVLOC.TEMP.RANGE");
    }

    private FacilityPolicyAdminService CreateFacilityPolicyService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new FacilityPolicyAdminService(
            new EfRepository<SystemSetting>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    [Fact]
    public async Task FacilityPolicy_ListCreatesCatalogAndUpdateRequiresReason()
    {
        await using var c = _factory.Create();
        var svc = CreateFacilityPolicyService(c);

        var listed = await svc.ListAsync();
        Assert.Contains(listed, p => p.Key == FacilityPolicyKeys.AllowElectronicCrossmatch);
        Assert.Contains(listed, p => p.Key == FacilityPolicyKeys.RequireSecondAboForCellularIssue);
        Assert.Contains(listed, p => p.Key == FacilityPolicyKeys.UncrossmatchedCellularMustBeGroupO);
        Assert.Contains(listed, p => p.Key == FacilityPolicyKeys.UncrossmatchedONegForChildbearing);
        Assert.Contains(listed, p => p.Key == FacilityPolicyKeys.RetentionYears);

        var retention = listed.Single(p => p.Key == FacilityPolicyKeys.RetentionYears);
        var missingReason = await svc.UpdateAsync(retention.Id, new SaveFacilityPolicyRequest("12", "no"));
        Assert.False(missingReason.Succeeded);
        Assert.Contains(missingReason.Evaluation!.HardStops, r => r.Code == FacilityPolicyValidator.ReasonCode);

        var saved = await svc.UpdateAsync(retention.Id, new SaveFacilityPolicyRequest("12", "Extend retention to twelve years."));
        Assert.True(saved.Succeeded);
        Assert.Equal("12", saved.Value!.Value);

        var history = await c.ConfigurationChangeHistory
            .SingleAsync(h => h.EntityType == nameof(SystemSetting) && h.EntityId == retention.Id);
        Assert.Equal(ConfigChangeAction.Update, history.Action);
        Assert.Contains("twelve years", history.ChangeReason);
    }

    [Fact]
    public async Task FacilityPolicy_BlocksOutOfRangeAlloHours()
    {
        await using var c = _factory.Create();
        var svc = CreateFacilityPolicyService(c);
        var listed = await svc.ListAsync();
        var allo = listed.Single(p => p.Key == FacilityPolicyKeys.SpecimenAlloimmunizationHours);

        var tooLong = await svc.UpdateAsync(allo.Id, new SaveFacilityPolicyRequest("96", "Weekend coverage needs a longer window."));
        Assert.False(tooLong.Succeeded);
        Assert.Contains(tooLong.Evaluation!.HardStops, r => r.Code == FacilityPolicyValidator.RangeCode);
    }

    [Fact]
    public async Task ExceptionDefinition_CreateAndUpdate_WriteConfigure()
    {
        await using var c = _factory.Create();
        var svc = CreateExceptionService(c);
        var code = "EXC-RA11-" + Guid.NewGuid().ToString("N")[..8];

        var created = await svc.CreateAsync(new SaveExceptionDefinitionRequest(
            code, "Result-audit exception", "Override gate for tests", 2, true));
        Assert.True(created.Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(ExceptionDefinition)
            && a.EntityId == created.Value!.Id
            && a.EventType == AuditEventType.Configure));

        var updated = await svc.UpdateAsync(created.Value!.Id, new SaveExceptionDefinitionRequest(
            code, "Result-audit exception", "Raised security level", 3, false));
        Assert.True(updated.Succeeded);

        var configure = await c.AuditEvents
            .Where(a => a.EntityType == nameof(ExceptionDefinition) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, configure.Count(a => a.EventType == AuditEventType.Configure));

        var deactivated = await svc.SetActiveAsync(created.Value.Id, false);
        Assert.True(deactivated.Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(ExceptionDefinition)
            && a.EntityId == created.Value.Id
            && a.EventType == AuditEventType.Deactivate));
    }

    [Fact]
    public async Task PhaseDefinition_CreateAndUpdate_WriteTestChange()
    {
        await using var c = _factory.Create();
        var svc = CreatePhaseService(c);
        var code = "PH-RA11-" + Guid.NewGuid().ToString("N")[..8];

        var created = await svc.CreateAsync(new SavePhaseDefinitionRequest(
            code, "Result-audit phase", 90, true, false, null, "draft phase"));
        Assert.True(created.Succeeded, created.Error ?? created.Evaluation?.HardStops.FirstOrDefault()?.Message);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(PhaseDefinition)
            && a.EntityId == created.Value!.Id
            && a.EventType == AuditEventType.TestChange));

        var updated = await svc.UpdateAsync(created.Value!.Id, new SavePhaseDefinitionRequest(
            code, "Result-audit phase", 91, false, false, null, "exclude from interpretation"));
        Assert.True(updated.Succeeded);

        var events = await c.AuditEvents
            .Where(a => a.EntityType == nameof(PhaseDefinition) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, events.Count(a => a.EventType == AuditEventType.TestChange));

        var activated = await svc.ActivateAsync(created.Value.Id, "activate phase");
        Assert.True(activated.Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(PhaseDefinition)
            && a.EntityId == created.Value.Id
            && a.EventType == AuditEventType.Activate));
    }
}
