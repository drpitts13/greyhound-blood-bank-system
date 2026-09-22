using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Modifications;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class OrderActionTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public OrderActionTests(SqliteContextFactory factory) => _factory = factory;

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c,
            orderActions: new EfRepository<OrderAction>(c),
            actionDefinitions: new EfRepository<ActionDefinition>(c));

    private BillingService Billing(BloodBankDbContext c, CapturingPublisher? publisher = null) =>
        new(new EfRepository<BillingEvent>(c), new EfRepository<ChargeRule>(c), new EfRepository<ChargeCode>(c),
            new EfRepository<TestServiceBilling>(c), new EfRepository<ProductBilling>(c),
            new EfRepository<TestResult>(c), new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            publisher ?? new CapturingPublisher(),
            actionBillings: new EfRepository<ActionBilling>(c),
            orderActions: new EfRepository<OrderAction>(c),
            actionDefinitions: new EfRepository<ActionDefinition>(c),
            orders: new EfRepository<Order>(c));

    private BloodProductModificationService Modifications(BloodBankDbContext c, CapturingPublisher? publisher = null) =>
        new(
            new InventoryRepository(c),
            new EfRepository<ModificationRule>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<ExpirationModificationCode>(c),
            new EfRepository<UnitModification>(c),
            new EfRepository<UnitModificationUnit>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            orderActions: new EfRepository<OrderAction>(c),
            actionDefinitions: new EfRepository<ActionDefinition>(c),
            orders: new EfRepository<Order>(c),
            orderLines: new EfRepository<OrderLine>(c),
            allocations: new EfRepository<Allocation>(c),
            crossmatches: new EfRepository<Crossmatch>(c),
            billing: Billing(c, publisher));

    private IssuingService Issuing(BloodBankDbContext c)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        return new IssuingService(
            new InventoryRepository(c), new EfRepository<Issue>(c), new EfRepository<Allocation>(c),
            new EfRepository<Crossmatch>(c), new EfRepository<Return>(c), new EfRepository<TransfusionEvent>(c),
            new EfRepository<Override>(c), new EfRepository<Patient>(c), new EfRepository<Specimen>(c),
            new EfRepository<ProductType>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<ExceptionDefinition>(c),
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<ProductAttribute>(c),
            new EfRepository<ProductAttributeAssignment>(c),
            new EfRepository<Order>(c),
            new EfRepository<User>(c),
            new EfRepository<InventoryLocation>(c),
            new BloodAttributeCompatLoader(
                new EfRepository<AntibodyHistory>(c),
                new EfRepository<AntigenProfile>(c),
                new EfRepository<UnitBloodAttribute>(c),
                new EfRepository<BloodAttributeDefinition>(c)),
            new AntibodyScreenCompatLoader(
                new EfRepository<TestResult>(c),
                new EfRepository<TestDefinition>(c),
                new EfRepository<AntibodyHistory>(c),
                new EfRepository<SpecialTransfusionRequirement>(c),
                new EfRepository<SpecialRequirementDefinition>(c),
                _factory.Clock),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            new ReactionInvestigationService(
                new EfRepository<ReactionInvestigation>(c),
                new EfRepository<TransfusionEvent>(c),
                new InventoryRepository(c),
                c, _factory.Clock, _factory.CurrentUser, audit),
            new FixedPermissionEvaluator(3),
            c, _factory.Clock, _factory.CurrentUser, audit,
            workups: new EfRepository<AntibodyIdentificationWorkup>(c),
            requirementDefinitions: new EfRepository<SpecialRequirementDefinition>(c),
            antibodies: new EfRepository<AntibodyHistory>(c),
            orderActions: new EfRepository<OrderAction>(c));
    }

    private async Task<(Patient Patient, Encounter Encounter, OrderingLocation Location, ProductType Source, ProductType Target)> SeedOrderContextAsync(
        BloodBankDbContext c,
        string key)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}",
            LastName = "Action",
            FirstName = "Test",
            DateOfBirth = new DateOnly(1985, 3, 3)
        };
        var location = new OrderingLocation { Code = $"LOC-{key}", Name = "Ward", IsActive = true };
        var source = new ProductType { ProductCode = $"SRC-{key}", Name = "RBC", ComponentClass = ComponentClass.RedBloodCells };
        var target = new ProductType { ProductCode = $"TGT-{key}", Name = "Irradiated RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.Patients.Add(patient);
        c.OrderingLocations.Add(location);
        c.ProductTypes.AddRange(source, target);
        await c.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{key}",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();
        return (patient, encounter, location, source, target);
    }

    private async Task<ActionDefinition> SeedActionAsync(BloodBankDbContext c, string code)
    {
        var action = new ActionDefinition
        {
            Code = code,
            Name = "Irradiate",
            Category = ActionCategory.Modification,
            ModificationType = ModificationType.Irradiate,
            IsActive = true
        };
        c.ActionDefinitions.Add(action);
        await c.SaveChangesAsync();
        return action;
    }

    [Fact]
    public async Task AddAction_ToProductOrder_ListsPendingAction()
    {
        await using var c = _factory.Create();
        var (patient, encounter, location, source, _) = await SeedOrderContextAsync(c, "ADD");
        var action = await SeedActionAsync(c, "IRRAD-ADD");
        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-ADD-{Guid.NewGuid():N}"[..16],
            [new OrderLineInputDto(OrderCategory.Product, null, source.Id)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(created.Succeeded, created.Error);

        var added = await Orders(c).AddActionAsync(patient.Id, created.Value!.Id, new AddOrderActionRequest(action.Id));
        Assert.True(added.Succeeded, added.Error);
        Assert.Equal(OrderActionStatus.Pending, added.Value!.Status);

        var listed = await Orders(c).ListByPatientAsync(patient.Id);
        var order = Assert.Single(listed);
        var pending = Assert.Single(order.Actions ?? []);
        Assert.Equal("IRRAD-ADD", pending.ActionCode);
        Assert.Equal(OrderActionStatus.Pending, pending.Status);
    }

    [Fact]
    public async Task AddAction_ToTestOrder_Fails()
    {
        await using var c = _factory.Create();
        var (patient, encounter, location, _, _) = await SeedOrderContextAsync(c, "TESTONLY");
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABORH",
            Name = "ABO/Rh",
            Category = TestCategory.AboRh,
            ResultValueType = ResultValueType.AboRh,
            IsActive = true
        });
        await c.SaveChangesAsync();
        var action = await SeedActionAsync(c, "IRRAD-TEST");
        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-TO-{Guid.NewGuid():N}"[..16],
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(created.Succeeded, created.Error);

        var added = await Orders(c).AddActionAsync(patient.Id, created.Value!.Id, new AddOrderActionRequest(action.Id));
        Assert.False(added.Succeeded);
        Assert.Contains("product line", added.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrderLinkedIrradiate_TransfersAllocation_BillsAction_AndLeavesResultAssigned()
    {
        await using var c = _factory.Create();
        var (patient, encounter, location, source, target) = await SeedOrderContextAsync(c, "LINK");
        var action = await SeedActionAsync(c, "IRRAD-LINK");
        var charge = new ChargeCode { Code = "BB-IRRAD-LINK", Description = "Irradiation", DefaultAmount = 85m };
        c.ChargeCodes.Add(charge);
        await c.SaveChangesAsync();
        c.ActionBillings.Add(new ActionBilling
        {
            ActionDefinitionId = action.Id,
            ChargeCodeId = charge.Id,
            Trigger = BillingTriggerType.ActionCompleted,
            IsActive = true
        });
        var exp = new ExpirationModificationCode
        {
            Code = "24H-LINK",
            OffsetAmount = 24,
            OffsetUnit = ExpirationOffsetUnit.Hours,
            RelativeTo = ExpirationRelativeTo.ModificationDateTime,
            IsActive = true
        };
        c.ExpirationModificationCodes.Add(exp);
        await c.SaveChangesAsync();
        var rule = new ModificationRule
        {
            ModificationCode = "IRR-LINK",
            SourceProductTypeId = source.Id,
            TargetProductTypeId = target.Id,
            ModificationType = ModificationType.Irradiate,
            ExpirationModificationCodeId = exp.Id,
            IsActive = true
        };
        c.ModificationRules.Add(rule);
        var unit = new BloodUnit
        {
            UnitNumber = "U-LINK-SRC",
            ProductTypeId = source.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Assigned
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-LINK-{Guid.NewGuid():N}"[..16],
            [new OrderLineInputDto(OrderCategory.Product, null, source.Id)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null,
            ActionDefinitionIds: [action.Id]));
        Assert.True(created.Succeeded, created.Error);

        var orderAction = await c.OrderActions.SingleAsync(a => a.OrderId == created.Value!.Id);
        c.Allocations.Add(new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderId = created.Value!.Id,
            Status = AllocationStatus.Reserved,
            AllocatedUtc = _factory.Clock.UtcNow,
            AllocatedBy = "tech"
        });
        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            SpecimenId = 0,
            Method = CrossmatchMethod.Serologic,
            Result = CrossmatchResult.Compatible,
            PerformedUtc = _factory.Clock.UtcNow,
            PerformedBy = "tech"
        });
        await c.SaveChangesAsync();

        var publisher = new CapturingPublisher();
        var result = await Modifications(c, publisher).ApplySingleAsync(
            unit.Id,
            new PerformSingleModificationRequest(rule.Id, null, "Order-linked irradiate", orderAction.Id));
        Assert.True(result.Succeeded, result.Error ?? result.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var resultUnit = Assert.Single(result.ResultUnits!);
        Assert.Equal(UnitStatus.Assigned, resultUnit.Status);
        Assert.Equal(target.Id, resultUnit.ProductTypeId);

        var sourceAfter = await c.BloodUnits.FindAsync(unit.Id);
        Assert.Equal(UnitStatus.Modified, sourceAfter!.Status);

        var allocation = await c.Allocations.SingleAsync(a => a.Status == AllocationStatus.Reserved);
        Assert.Equal(resultUnit.Id, allocation.BloodProductId);
        Assert.Equal(created.Value.Id, allocation.OrderId);

        var clonedXm = await c.Crossmatches.SingleAsync(x => x.BloodProductId == resultUnit.Id);
        Assert.Equal(CrossmatchResult.Compatible, clonedXm.Result);

        var order = await c.Orders.FindAsync(created.Value.Id);
        Assert.Equal(target.Id, order!.ProductTypeId);
        var line = await c.OrderLines.SingleAsync(l => l.OrderId == order.Id && l.LineCategory == OrderCategory.Product);
        Assert.Equal(target.Id, line.ProductTypeId);

        var completed = await c.OrderActions.FindAsync(orderAction.Id);
        Assert.Equal(OrderActionStatus.Completed, completed!.Status);
        Assert.Equal(resultUnit.Id, completed.ResultBloodUnitId);

        var billing = await c.BillingEvents.SingleAsync(e => e.TriggerType == BillingTriggerType.ActionCompleted);
        Assert.Equal(BillingChargeSourceKind.Action, billing.SourceKind);
        Assert.Equal(charge.Code, billing.BillingCode);
        Assert.Single(publisher.Published);

        var again = await Billing(c, publisher).CaptureForActionAsync(orderAction.Id);
        Assert.True(again.Succeeded, again.Error);
        Assert.Empty(again.Value!);
    }

    [Fact]
    public async Task StandaloneModify_StillQuarantines_AndDoesNotBill()
    {
        await using var c = _factory.Create();
        var source = new ProductType { ProductCode = "SRC-SOLO", Name = "RBC" };
        var target = new ProductType { ProductCode = "TGT-SOLO", Name = "Irradiated RBC" };
        c.ProductTypes.AddRange(source, target);
        var exp = new ExpirationModificationCode
        {
            Code = "24H-SOLO",
            OffsetAmount = 24,
            OffsetUnit = ExpirationOffsetUnit.Hours,
            RelativeTo = ExpirationRelativeTo.ModificationDateTime,
            IsActive = true
        };
        c.ExpirationModificationCodes.Add(exp);
        await c.SaveChangesAsync();
        var rule = new ModificationRule
        {
            ModificationCode = "IRR-SOLO",
            SourceProductTypeId = source.Id,
            TargetProductTypeId = target.Id,
            ModificationType = ModificationType.Irradiate,
            ExpirationModificationCodeId = exp.Id,
            IsActive = true
        };
        c.ModificationRules.Add(rule);
        var unit = new BloodUnit
        {
            UnitNumber = "U-SOLO",
            ProductTypeId = source.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        var billingBefore = await c.BillingEvents.CountAsync();

        var result = await Modifications(c).ApplySingleAsync(
            unit.Id,
            new PerformSingleModificationRequest(rule.Id, null, "Standalone irradiate"));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(UnitStatus.Quarantine, Assert.Single(result.ResultUnits!).Status);
        Assert.Equal(billingBefore, await c.BillingEvents.CountAsync());
    }

    [Fact]
    public async Task Issue_WithPendingAction_CancelsActionAndDoesNotBill()
    {
        await using var c = _factory.Create();
        var (patient, encounter, location, source, _) = await SeedOrderContextAsync(c, "ISSUE");
        var action = await SeedActionAsync(c, "IRRAD-ISSUE");
        var charge = new ChargeCode { Code = "BB-IRRAD-ISSUE", Description = "Irradiation", DefaultAmount = 85m };
        c.ChargeCodes.Add(charge);
        await c.SaveChangesAsync();
        c.ActionBillings.Add(new ActionBilling
        {
            ActionDefinitionId = action.Id,
            ChargeCodeId = charge.Id,
            Trigger = BillingTriggerType.ActionCompleted,
            IsActive = true
        });
        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Source = BloodTypeSource.TestResult,
                IsCurrent = true
            },
            new PatientBloodTypeHistory
            {
                PatientId = patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Source = BloodTypeSource.HistoricalImport,
                IsCurrent = false
            });
        c.Specimens.Add(new Specimen
        {
            AccessionNumber = "ACC-ISSUE-MOD",
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2),
            ReceivedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        });
        var unit = new BloodUnit
        {
            UnitNumber = "U-ISSUE-SRC",
            ProductTypeId = source.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Assigned
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            encounter.Id, location.Id, $"ORD-ISS-{Guid.NewGuid():N}"[..16],
            [new OrderLineInputDto(OrderCategory.Product, null, source.Id)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null,
            ActionDefinitionIds: [action.Id]));
        Assert.True(created.Succeeded, created.Error);

        c.Allocations.Add(new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderId = created.Value!.Id,
            Status = AllocationStatus.Reserved,
            AllocatedUtc = _factory.Clock.UtcNow,
            AllocatedBy = "tech"
        });
        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            SpecimenId = 0,
            Method = CrossmatchMethod.Serologic,
            Result = CrossmatchResult.Compatible,
            PerformedUtc = _factory.Clock.UtcNow,
            PerformedBy = "tech"
        });
        await c.SaveChangesAsync();

        var issued = await Issuing(c).IssueUnitAsync(new IssueUnitRequest(
            unit.Id,
            patient.Id,
            PatientIdentifier1Value: patient.MedicalRecordNumber,
            PatientIdentifier2Value: patient.DateOfBirth.ToString("yyyy-MM-dd")));
        Assert.True(
            issued.Succeeded,
            issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => $"{r.Code}: {r.Message}") ?? []));

        var orderAction = await c.OrderActions.SingleAsync(a => a.OrderId == created.Value.Id);
        Assert.Equal(OrderActionStatus.Cancelled, orderAction.Status);
        Assert.DoesNotContain(
            c.BillingEvents,
            e => e.BillingCode == "BB-IRRAD-ISSUE"
                 || e.TriggerEntityId == orderAction.Id);
    }

    [Fact]
    public async Task EligibleModifications_FilterBySourceProductType()
    {
        await using var c = _factory.Create();
        var source = new ProductType { ProductCode = "SRC-ELIG", Name = "RBC" };
        var other = new ProductType { ProductCode = "OTH-ELIG", Name = "Plasma" };
        var target = new ProductType { ProductCode = "TGT-ELIG", Name = "Irradiated RBC" };
        c.ProductTypes.AddRange(source, other, target);
        var exp = new ExpirationModificationCode
        {
            Code = "24H-ELIG",
            OffsetAmount = 24,
            OffsetUnit = ExpirationOffsetUnit.Hours,
            RelativeTo = ExpirationRelativeTo.ModificationDateTime,
            IsActive = true
        };
        c.ExpirationModificationCodes.Add(exp);
        await c.SaveChangesAsync();
        c.ModificationRules.AddRange(
            new ModificationRule
            {
                ModificationCode = "IRR-ELIG",
                SourceProductTypeId = source.Id,
                TargetProductTypeId = target.Id,
                ModificationType = ModificationType.Irradiate,
                ExpirationModificationCodeId = exp.Id,
                IsActive = true
            },
            new ModificationRule
            {
                ModificationCode = "THAW-ELIG",
                SourceProductTypeId = other.Id,
                TargetProductTypeId = target.Id,
                ModificationType = ModificationType.Thaw,
                ExpirationModificationCodeId = exp.Id,
                IsActive = true
            });
        var unit = new BloodUnit
        {
            UnitNumber = "U-ELIG",
            ProductTypeId = source.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Assigned
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var eligible = await Modifications(c).GetEligibleModificationsAsync(unit.Id);
        Assert.Contains(eligible, e => e.ModificationCode == "IRR-ELIG");
        Assert.DoesNotContain(eligible, e => e.ModificationCode == "THAW-ELIG");
    }
}

internal sealed class CapturingPublisher : IBillingInterfacePublisher
{
    public List<BillingEvent> Published { get; } = new();

    public Task<long?> PublishChargeAsync(BillingEvent billingEvent, CancellationToken ct = default)
    {
        Published.Add(billingEvent);
        return Task.FromResult<long?>(null);
    }
}

public class ActionDefinitionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ActionDefinitionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ActionDefinitionAdminService Actions(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ActionDefinitionAdminService(
            new EfRepository<ActionDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var request = new SaveActionDefinitionRequest("IRRAD-AUTH", "Irradiate", ActionCategory.Modification, ModificationType.Irradiate, null);

        var denied = await Actions(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ActionDefinitionAuthorizationRule.CreateCode);

        var allowed = await Actions(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("IRRAD-AUTH", allowed.Value!.Code);
    }
}

public class ActionBillingAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ActionBillingAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private ActionBillingAdminService Rows(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new ActionBillingAdminService(
            new EfRepository<ActionBilling>(c),
            new EfRepository<ActionDefinition>(c),
            new EfRepository<ChargeCode>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var action = new ActionDefinition
        {
            Code = "IRRAD-BILL",
            Name = "Irradiate",
            Category = ActionCategory.Modification,
            ModificationType = ModificationType.Irradiate,
            IsActive = true
        };
        var code = new ChargeCode { Code = "BB-IRRAD-AUTH", Description = "Irradiation", DefaultAmount = 1m };
        c.ActionDefinitions.Add(action);
        c.ChargeCodes.Add(code);
        await c.SaveChangesAsync();
        var request = new SaveActionBillingRequest(action.Id, code.Id, null, BillingTriggerType.ActionCompleted);

        var denied = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ActionBillingAuthorizationRule.CreateCode);

        var allowed = await Rows(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }
}
