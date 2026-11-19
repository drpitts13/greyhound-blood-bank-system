using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ProductRetypeServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ProductRetypeServiceTests(SqliteContextFactory factory) => _factory = factory;

    private InventoryService Inventory(BloodBankDbContext context) =>
        new(
            new InventoryRepository(context),
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            new IsbtLookupCatalog(
                new EfRepository<IsbtAboRhdCode>(context),
                new EfRepository<IsbtProductCode>(context)),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            retype: Retype(context));

    private ProductRetypeService Retype(
        BloodBankDbContext context,
        ICurrentUser? user = null,
        IPermissionEvaluator? permissions = null) =>
        new(
            new InventoryRepository(context),
            new EfRepository<ProductRetypeResult>(context),
            new EfRepository<TestDefinition>(context),
            context,
            _factory.Clock,
            user ?? _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, user ?? _factory.CurrentUser),
            permissions: permissions,
            allocations: new EfRepository<Allocation>(context),
            patients: new EfRepository<Patient>(context));

    private static RecordProductRetypeRequest MatchOPos() => new(
        AboGroup.O,
        null,
        new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "0",
            [AboRhPanelSubtestCodes.AntiB] = "0"
        });

    private static ICurrentUser Verifier => new TestCurrentUser("tech-verify", "WORKSTATION-2");

    private async Task<(long ProductTypeId, long UnitId)> SeedReceivedUnitAsync(
        string unitNumber,
        AboGroup abo,
        RhType rh,
        bool requiresRetype = true)
    {
        await using var context = _factory.Create();
        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"))
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0206",
                Description = "RED BLOOD CELLS",
                ComponentClass = nameof(ComponentClass.RedBloodCells),
                AttributesJson = "[]",
                StandardVersion = UsSupplierProductCodeSeed.StandardVersion,
                IsPlaceholder = true
            });
        }

        var retypeTest = await context.TestDefinitions.FirstOrDefaultAsync(t => t.Code == AboRhRetypeRule.TestCode);
        if (retypeTest is null)
        {
            retypeTest = new TestDefinition
            {
                Code = AboRhRetypeRule.TestCode,
                Name = "ABO/Rh Retype",
                Category = TestCategory.AboRhRetype,
                ResultValueType = ResultValueType.AboRh,
                IsActive = true,
                IsDraft = false,
                Version = 1
            };
            context.TestDefinitions.Add(retypeTest);
            await context.SaveChangesAsync();
        }

        var type = new ProductType
        {
            ProductCode = $"RBC-{unitNumber}",
            Name = "Retype product",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresRetype = requiresRetype,
            RhPositiveRetypeTestId = requiresRetype ? retypeTest.Id : null,
            RhNegativeRetypeTestId = requiresRetype ? retypeTest.Id : null
        };
        context.ProductTypes.Add(type);
        await context.SaveChangesAsync();

        var receive = await Inventory(context).ReceiveUnitAsync(new ReceiveUnitRequest(
            unitNumber, type.Id, abo, rh, _factory.Clock.UtcNow.AddDays(30),
            Isbt128ProductCode: "E0206",
            SecondVerifier: "tech2",
            ReceiveTemperatureCelsius: 4.0m, Appearance: UnitAppearance.Acceptable));
        Assert.True(receive.Succeeded, receive.Error);
        return (type.Id, receive.Unit!.Id);
    }

    // TEST-BB-1007: a scanned ISBT 128 DIN ("=" + 13 characters + 2 flags) is received under its 13-character DIN, so the
    // issue desk and bedside find it; a scan that is not a DIN is refused (end-user testing, 2026-11-12: it was stored
    // with the "=" and the flags).
    [Fact]
    public async Task Receive_ScannedDin_IsStoredAsTheDin()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("=W00002612345600", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        Assert.Equal("W000026123456", (await context.BloodUnits.FindAsync(unitId))!.UnitNumber);

        var type = await context.ProductTypes.FirstAsync();
        var refused = await Inventory(context).ReceiveUnitAsync(new ReceiveUnitRequest(
            "=<E0336V00", type.Id, AboGroup.O, RhType.Positive, _factory.Clock.UtcNow.AddDays(30),
            Isbt128ProductCode: "E0206", SecondVerifier: "tech2", ReceiveTemperatureCelsius: 4.0m, Appearance: UnitAppearance.Acceptable));
        Assert.False(refused.Succeeded);
        Assert.Contains("could not be read as an ISBT 128 DIN", refused.Error);
    }

    [Fact]
    public async Task Receive_CreatesPendingRetype_AndRecordUpdatesSameRow()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-PENDING", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var before = Assert.Single(await context.ProductRetypeResults.Where(r => r.BloodProductId == unitId).ToListAsync());
        Assert.Equal(ResultStatus.Pending, before.Status);
        Assert.Equal(AboRhRetypeRule.TestCode, before.TestCode);

        var recorded = await Retype(context).RecordAsync(unitId, MatchOPos());
        Assert.True(recorded.Succeeded, recorded.Error);
        Assert.Equal(ResultStatus.Verified, recorded.Value!.Latest!.Status);
        Assert.Equal(before.Id, recorded.Value.Latest.Id);
        Assert.Equal("0", recorded.Value.Latest.CellReactions.Single(c => c.Code == AboRhPanelSubtestCodes.AntiA).Grade);
        Assert.Equal("Anti-A", recorded.Value.Latest.CellReactions.Single(c => c.Code == AboRhPanelSubtestCodes.AntiA).Label);
        Assert.Equal("0", recorded.Value.Latest.CellReactions.Single(c => c.Code == AboRhPanelSubtestCodes.AntiB).Grade);
        Assert.DoesNotContain(recorded.Value.Latest.CellReactions, c => c.Code == AboRhPanelSubtestCodes.AntiD);
        Assert.Equal(1, await context.ProductRetypeResults.CountAsync(r => r.BloodProductId == unitId));
    }

    [Fact]
    public async Task MatchingRetype_ReleasesUnitWithoutSecondReviewer()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-MATCH", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var recorded = await Retype(context).RecordAsync(unitId, MatchOPos());

        Assert.True(recorded.Succeeded, recorded.Error);
        Assert.Equal(UnitStatus.Available, recorded.Value!.Status);
        Assert.False(recorded.Value.CanVerify);
        Assert.Equal(ResultStatus.Verified, recorded.Value.Latest!.Status);
        Assert.Equal(_factory.CurrentUser.UserName, recorded.Value.Latest.VerifiedBy);

        var unit = await context.BloodUnits.AsNoTracking().FirstAsync(u => u.Id == unitId);
        Assert.Equal(UnitStatus.Available, unit.Status);
        Assert.Contains(await context.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync(),
            h => h.ToStatus == UnitStatus.Available && h.Reason != null && h.Reason.Contains("retype", StringComparison.OrdinalIgnoreCase));

        var retypeId = recorded.Value.Latest.Id;
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.Result && a.EntityType == nameof(ProductRetypeResult) && a.EntityId == retypeId));
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.Verify && a.EntityType == nameof(ProductRetypeResult) && a.EntityId == retypeId));
        Assert.Contains(
            await context.AuditEvents
                .Where(a => a.EventType == AuditEventType.ProductStatus && a.EntityType == nameof(BloodUnit) && a.EntityId == unitId)
                .ToListAsync(),
            a => a.Reason != null && a.Reason.Contains("retype", StringComparison.OrdinalIgnoreCase));
    }

    // TEST-BB-1172: a unit waiting for its retype that is reserved for a patient says for whom and is listed before units
    // not reserved, whatever their expiry (end-user testing pass 150).
    [Fact]
    public async Task PendingRetype_ReservedUnit_SaysForWhom_AndComesFirst()
    {
        var (_, freeId) = await SeedReceivedUnitAsync("RT-FREE", AboGroup.O, RhType.Positive);
        var (_, reservedId) = await SeedReceivedUnitAsync("RT-RES", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        (await context.BloodUnits.SingleAsync(u => u.Id == freeId)).ExpiresUtc = _factory.Clock.UtcNow.AddDays(2);
        (await context.BloodUnits.SingleAsync(u => u.Id == reservedId)).ExpiresUtc = _factory.Clock.UtcNow.AddDays(30);
        var patient = new Patient { MedicalRecordNumber = $"MRN-RT-{Guid.NewGuid():N}"[..14], LastName = "Waiting", FirstName = "Pat", DateOfBirth = new DateOnly(1970, 1, 1) };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        context.Allocations.Add(new Allocation { BloodProductId = reservedId, PatientId = patient.Id, Status = AllocationStatus.Reserved, AllocatedUtc = _factory.Clock.UtcNow, AllocatedBy = "tech" });
        await context.SaveChangesAsync();

        var pending = (await Retype(context).ListPendingAsync()).ToList();
        var reserved = Assert.Single(pending, p => p.UnitId == reservedId);
        Assert.Equal(($"Waiting, Pat ({patient.MedicalRecordNumber})", patient.Id), (reserved.ReservedFor, reserved.ReservedPatientId));
        Assert.Null(Assert.Single(pending, p => p.UnitId == freeId).ReservedFor);
        Assert.True(pending.FindIndex(p => p.UnitId == reservedId) < pending.FindIndex(p => p.UnitId == freeId));
    }

    [Fact]
    public async Task MatchingRetype_OutdatedUnit_DoesNotBecomeAvailable()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-OUTDATED", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var unit = await context.BloodUnits.SingleAsync(u => u.Id == unitId);
        unit.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
        await context.SaveChangesAsync();

        var waiting = Assert.Single(await Retype(context).ListPendingAsync(), p => p.UnitId == unitId);
        Assert.True(waiting.Expired);

        var recorded = await Retype(context).RecordAsync(unitId, MatchOPos());
        Assert.True(recorded.Succeeded, recorded.Error);
        Assert.Equal(UnitStatus.Expired, recorded.Value!.Status);
        Assert.True(recorded.Value.Latest!.MatchesLabel);
        Assert.Equal(ResultStatus.Verified, recorded.Value.Latest.Status);
        Assert.DoesNotContain(await Retype(context).ListPendingAsync(), p => p.UnitId == unitId);
    }

    [Fact]
    public async Task Verify_WithoutResultVerify_IsHardStopped()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-PERM", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var leftover = await SeedEnteredRetypeAsync(context, unitId);

        var denied = await Retype(
            context,
            Verifier,
            new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter)).VerifyAsync(unitId, leftover.Id);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ResultAuthorizationRule.VerifyCode);
        Assert.Equal(UnitStatus.Received, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await Retype(
            context,
            Verifier,
            new FixedPermissionEvaluator(1, PermissionCodes.ResultVerify)).VerifyAsync(unitId, leftover.Id);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Available, allowed.Value!.Status);
    }

    [Fact]
    public async Task Record_WithoutResultEnter_IsHardStopped()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-ENT", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var denied = await Retype(
            context,
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.ResultVerify)).RecordAsync(unitId, MatchOPos());
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ResultAuthorizationRule.EnterCode);
        Assert.False(await context.ProductRetypeResults.AnyAsync(
            r => r.BloodProductId == unitId && r.Status == ResultStatus.Entered));

        var allowed = await Retype(
            context,
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter)).RecordAsync(unitId, MatchOPos());
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(ResultStatus.Verified, allowed.Value!.Latest!.Status);
        Assert.Equal(UnitStatus.Available, allowed.Value.Status);
    }

    [Fact]
    public async Task MismatchRetype_MovesReceivedToQuarantine()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-MISMATCH", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();
        var result = await Retype(context).RecordAsync(unitId, new RecordProductRetypeRequest(
            AboGroup.A,
            null,
            new Dictionary<string, string>
            {
                [AboRhPanelSubtestCodes.AntiA] = "4+",
                [AboRhPanelSubtestCodes.AntiB] = "0"
            }));

        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.Quarantine, result.Value!.Status);
        Assert.Equal(ResultStatus.Verified, result.Value.Latest!.Status);
        Assert.False(result.Value.Latest.MatchesLabel);
        Assert.NotNull(result.Value.Latest.DiscrepancyDetail);

        var unit = await context.BloodUnits.AsNoTracking().FirstAsync(u => u.Id == unitId);
        Assert.Equal(UnitStatus.Quarantine, unit.Status);
        Assert.Contains("discrepancy", unit.QuarantineReason, StringComparison.OrdinalIgnoreCase);
    }

    // TEST-BB-1311 (end-user testing pass 285): a unit waiting for its retype with no retype row yet names the test Enter
    // will use (the product's retype test for its labelled Rh), marked not started, instead of "—".
    [Fact]
    public async Task PendingList_UnitWithNoRetypeRow_NamesTheTestToUse()
    {
        var (_, unitId) = await SeedReceivedUnitAsync("RT-NOROW", AboGroup.B, RhType.Negative);
        await using var context = _factory.Create();
        context.ProductRetypeResults.RemoveRange(await context.ProductRetypeResults.Where(r => r.BloodProductId == unitId).ToListAsync());
        await context.SaveChangesAsync();

        var row = Assert.Single(await Retype(context).ListPendingAsync(), p => p.UnitId == unitId);
        Assert.Equal(AboRhRetypeRule.TestCode, row.TestCode);
        Assert.Equal("ABO/Rh Retype", row.TestName);
        Assert.True(row.TestNotStarted);
        Assert.Null(row.ResultStatus);

        var (_, startedId) = await SeedReceivedUnitAsync("RT-STARTED", AboGroup.O, RhType.Positive);
        Assert.False(Assert.Single(await Retype(context).ListPendingAsync(), p => p.UnitId == startedId).TestNotStarted);
    }

    [Fact]
    public async Task PendingList_IncludesOnlyReceivedRetypeUnits()
    {
        var (_, pendingId) = await SeedReceivedUnitAsync("RT-PEND", AboGroup.B, RhType.Negative);
        await SeedReceivedUnitAsync("RT-SKIP", AboGroup.O, RhType.Positive, requiresRetype: false);

        await using var context = _factory.Create();
        var pending = await Retype(context).ListPendingAsync();
        Assert.Contains(pending, u => u.UnitId == pendingId);
        Assert.DoesNotContain(pending, u => u.UnitNumber == "RT-SKIP");
    }

    [Fact]
    public async Task IsbtReleaseToAvailable_IgnoredWhenRequiresRetype()
    {
        await using var context = _factory.Create();
        if (!await context.IsbtAboRhdCodes.AnyAsync(c => c.Code == "DEMO"))
        {
            context.IsbtAboRhdCodes.Add(new IsbtAboRhdCode
            {
                Code = "DEMO",
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsPlaceholder = true
            });
        }

        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"))
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0206",
                Description = "PLACEHOLDER RBC",
                ComponentClass = "RedBloodCells",
                AttributesJson = "[]",
                IsPlaceholder = true
            });
        }
        var retypeTest = await context.TestDefinitions.FirstOrDefaultAsync(t => t.Code == AboRhRetypeRule.TestCode);
        if (retypeTest is null)
        {
            retypeTest = new TestDefinition
            {
                Code = AboRhRetypeRule.TestCode,
                Name = "ABO/Rh Retype",
                Category = TestCategory.AboRhRetype,
                ResultValueType = ResultValueType.AboRh,
                IsActive = true,
                IsDraft = false,
                Version = 1
            };
            context.TestDefinitions.Add(retypeTest);
            await context.SaveChangesAsync();
        }

        var type = new ProductType
        {
            ProductCode = "RBC-ISBT-RT",
            Name = "ISBT retype RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresRetype = true,
            RhPositiveRetypeTestId = retypeTest.Id,
            RhNegativeRetypeTestId = retypeTest.Id
        };
        context.ProductTypes.Add(type);
        await context.SaveChangesAsync();

        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        var inventory = Inventory(context);
        var dinCheck = new PlaceholderDinCheckCharacterValidator();
        var manual = new ManualComponentEntryService(lookups, dinCheck, inventory, _factory.Clock, _factory.CurrentUser);
        var check = dinCheck.ComputeCheckCharacter("G123417654399");

        var result = await manual.CreateAsync(new ManualComponentEntryRequest(
            DonationNumber: $"G1234 17 654399 {check}",
            AboRhdCode: "DEMO",
            ProductDescriptionCode: "E0206",
            CollectionTypeCode: "0",
            DivisionCode: "00",
            ExtendedDivisionCode: null,
            ExpirationLocal: _factory.Clock.UtcNow.AddDays(30),
            ExpirationHasExplicitTime: false,
            ProductTypeId: type.Id,
            ReleaseToAvailable: true,
            SecondVerifier: "tech2",
            ReceiveTemperatureCelsius: 4.0m, Appearance: UnitAppearance.Acceptable));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(UnitStatus.Received, result.Unit!.Status);
    }

    [Fact]
    public async Task Record_WriteInterpretedType()
    {
        var key = Guid.NewGuid().ToString("N")[..8];
        var (_, unitId) = await SeedReceivedUnitAsync($"RT-AUD-{key}", AboGroup.O, RhType.Positive);
        await using var context = _factory.Create();

        var recorded = await Retype(context).RecordAsync(unitId, MatchOPos());
        Assert.True(recorded.Succeeded, recorded.Error);
        var retypeId = recorded.Value!.Latest!.Id;
        Assert.Equal(UnitStatus.Available, recorded.Value.Status);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Result
            && e.EntityType == nameof(ProductRetypeResult)
            && e.EntityId == retypeId
            && e.Reason == "Unit ABO/Rh retype entered."
            && e.NewValueJson is not null
            && e.NewValueJson.Contains("O"));
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Verify
            && e.EntityType == nameof(ProductRetypeResult)
            && e.EntityId == retypeId
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.OldValueJson.Contains("O")
            && e.NewValueJson.Contains("O"));
    }

    private static async Task<ProductRetypeResult> SeedEnteredRetypeAsync(BloodBankDbContext context, long unitId)
    {
        var pending = Assert.Single(await context.ProductRetypeResults.Where(r => r.BloodProductId == unitId).ToListAsync());
        pending.Value = AboRhResultValue.FormatPanel(new AboRhPanelResult(
            AboGroup.O,
            RhType.Positive,
            new Dictionary<string, string>
            {
                [AboRhPanelSubtestCodes.AntiA] = "0",
                [AboRhPanelSubtestCodes.AntiB] = "0"
            }));
        pending.InterpretedAbo = AboGroup.O;
        pending.InterpretedRh = RhType.Positive;
        pending.MatchesLabel = true;
        pending.Status = ResultStatus.Entered;
        await context.SaveChangesAsync();
        return pending;
    }
}
