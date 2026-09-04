using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class InventoryServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InventoryServiceTests(SqliteContextFactory factory) => _factory = factory;

    private InventoryService CreateService(BloodBankDbContext context, IPermissionEvaluator? permissions = null)
    {
        var repository = new InventoryRepository(context);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser);
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        return new InventoryService(
            repository,
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            lookups,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            new EfRepository<User>(context),
            new FacilityPolicyService(new EfRepository<SystemSetting>(context)),
            new EfRepository<Patient>(context),
            permissions: permissions);
    }

    private async Task EnsureSecondVerifierAsync(string userName = "tech2")
    {
        await using var context = _factory.Create();
        if (await context.Users.AnyAsync(u => u.UserName == userName))
        {
            return;
        }

        context.Users.Add(new User
        {
            UserName = userName,
            DisplayName = "Tech Two",
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    private async Task EnsureProductCodesAsync()
    {
        await using var context = _factory.Create();
        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"))
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0206",
                Description = "RED BLOOD CELLS|CPDA-1/450mL/refg|Irradiated",
                ComponentClass = nameof(ComponentClass.RedBloodCells),
                AttributesJson = "[]",
                StandardVersion = UsSupplierProductCodeSeed.StandardVersion,
                IsPlaceholder = true
            });
        }

        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0336"))
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0336",
                Description = "RED BLOOD CELLS|CPD>AS1/500mL/refg|ResLeu:<5E6",
                ComponentClass = nameof(ComponentClass.RedBloodCells),
                AttributesJson = "[]",
                StandardVersion = UsSupplierProductCodeSeed.StandardVersion,
                IsPlaceholder = true
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task<long> EnsureProductTypeAsync()
    {
        await EnsureSecondVerifierAsync();
        await EnsureProductCodesAsync();
        await using var context = _factory.Create();
        var existing = await context.ProductTypes.FirstOrDefaultAsync(t => t.ProductCode == "RBC-TEST");
        if (existing is not null)
        {
            return existing.Id;
        }

        var type = new ProductType
        {
            ProductCode = "RBC-TEST",
            Name = "Test RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        context.ProductTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
    }

    private async Task<long> EnsureAttributeDefinitionAsync(BloodBankDbContext context)
    {
        var existing = await context.BloodAttributeDefinitions.FirstOrDefaultAsync(d => d.Code == "K");
        if (existing is not null)
        {
            return existing.Id;
        }

        var kell = new BloodAttributeDefinition
        {
            Code = "K",
            Name = "Kell",
            AntibodyName = "anti-K",
            IsClinicallySignificant = true,
            SortOrder = 1,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = _factory.Clock.UtcNow,
            Version = 1
        };
        context.BloodAttributeDefinitions.Add(kell);
        await context.SaveChangesAsync();
        return kell.Id;
    }

    private ReceiveUnitRequest NewUnitRequest(
        string unitNumber,
        long productTypeId,
        DateTime? expires = null,
        string? productCode = "E0206") =>
        new(unitNumber, productTypeId, AboGroup.O, RhType.Positive,
            expires ?? _factory.Clock.UtcNow.AddDays(30),
            Isbt128ProductCode: productCode,
            SecondVerifier: "tech2",
            ReceiveTemperatureCelsius: 4.0m);

    [Fact]
    public async Task ReceiveUnit_CreatesQuarantineUnit_WithInitialHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.ReceiveUnitAsync(NewUnitRequest("U-INTAKE-1", productTypeId));

            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Quarantine, result.Unit!.Status);
            Assert.Equal(UnitQuarantineReason.PendingRelease, result.Unit.QuarantineReasonCode);
            Assert.Equal("E0206", result.Unit.ProductDescriptionCode);
            unitId = result.Unit.Id;
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            var initial = Assert.Single(history);
            Assert.Null(initial.FromStatus);
            Assert.Equal(UnitStatus.Quarantine, initial.ToStatus);
        }
    }

    [Fact]
    public async Task ListNearExpiry_IncludesOnHandUnitsInsideWindow()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var soon = await service.ReceiveUnitAsync(
            NewUnitRequest("U-NEAR-YES", productTypeId, _factory.Clock.UtcNow.AddHours(12)));
        Assert.True(soon.Succeeded, soon.Error);
        var later = await service.ReceiveUnitAsync(NewUnitRequest("U-NEAR-NO", productTypeId));
        Assert.True(later.Succeeded, later.Error);

        var list = await service.ListNearExpiryAsync();
        Assert.Contains(list, i => i.UnitNumber == "U-NEAR-YES");
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-NEAR-NO");
    }

    [Fact]
    public async Task ListNearExpiry_ExcludesExpectedAndExpired()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var expected = await service.ExpectUnitAsync(
            NewUnitRequest("U-NEAR-ASN", productTypeId, _factory.Clock.UtcNow.AddHours(6)));
        Assert.True(expected.Succeeded, expected.Error);

        var expired = await service.ReceiveUnitAsync(
            NewUnitRequest("U-NEAR-PAST", productTypeId, _factory.Clock.UtcNow.AddHours(1)));
        Assert.True(expired.Succeeded, expired.Error);
        expired.Unit!.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
        await context.SaveChangesAsync();

        var list = await service.ListNearExpiryAsync();
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-NEAR-ASN");
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-NEAR-PAST");
    }

    [Fact]
    public async Task ListQuarantine_IncludesReceivedUnits()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-LIST", productTypeId));
        Assert.True(received.Succeeded, received.Error);
        var expected = await service.ExpectUnitAsync(NewUnitRequest("U-Q-ASN", productTypeId));
        Assert.True(expected.Succeeded, expected.Error);

        var list = await service.ListQuarantineAsync();
        Assert.Contains(list, i => i.UnitNumber == "U-Q-LIST" && i.ReasonCode == UnitQuarantineReason.PendingRelease);
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-Q-ASN");
    }

    [Fact]
    public async Task Quarantine_Unspecified_IsHardStop()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-NONE", productTypeId));
        Assert.True(received.Succeeded, received.Error);
        var released = await service.ReleaseFromQuarantineAsync(received.Unit!.Id, "tech2");
        Assert.True(released.Succeeded, released.Error);

        var result = await service.QuarantineAsync(received.Unit.Id, UnitQuarantineReason.Unspecified);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == QuarantineReasonRule.Code);
    }

    [Fact]
    public async Task ReceiveUnit_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .ReceiveUnitAsync(NewUnitRequest("U-RCV-PERM", productTypeId));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.ReceiveCode);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-RCV-PERM"));

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .ReceiveUnitAsync(NewUnitRequest("U-RCV-PERM", productTypeId));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("U-RCV-PERM", allowed.Unit!.UnitNumber);
    }

    [Fact]
    public async Task SaveBloodAttribute_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        long definitionId;
        await using (var setup = _factory.Create())
        {
            unitId = (await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-ATTR-PERM", productTypeId))).Unit!.Id;
            definitionId = await EnsureAttributeDefinitionAsync(setup);
        }

        await using var context = _factory.Create();
        var request = new SaveUnitBloodAttributeRequest(definitionId, BloodAttributeKind.Antigen, AntigenResult.Negative);

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .SaveBloodAttributeAsync(unitId, request);
        Assert.False(denied.Succeeded);
        Assert.Equal(InventoryAuthorizationRule.EvaluateSaveAttribute(false).Message, denied.Error);
        Assert.False(await context.UnitBloodAttributes.AnyAsync(a => a.BloodProductId == unitId));

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .SaveBloodAttributeAsync(unitId, request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(AntigenResult.Negative, allowed.Value!.Result);
        Assert.Equal(definitionId, allowed.Value.BloodAttributeDefinitionId);
    }

    [Fact]
    public async Task ReleaseFromQuarantine_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive));
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-PERM", productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var released = await service.ReleaseFromQuarantineAsync(received.Unit!.Id, "tech2");
        Assert.False(released.Succeeded);
        Assert.Contains(released.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.QuarantineReleaseCode);
        Assert.Equal(UnitStatus.Quarantine, (await context.BloodUnits.FindAsync(received.Unit.Id))!.Status);

        var allowed = CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease));
        var ok = await allowed.ReleaseFromQuarantineAsync(received.Unit.Id, "tech2");
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal(UnitStatus.Available, ok.Unit!.Status);
    }

    [Fact]
    public async Task Quarantine_OtherWithoutNotes_IsHardStop()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-OTHER", productTypeId));
        Assert.True(received.Succeeded, received.Error);
        var released = await service.ReleaseFromQuarantineAsync(received.Unit!.Id, "tech2");
        Assert.True(released.Succeeded, released.Error);

        var result = await service.QuarantineAsync(received.Unit.Id, UnitQuarantineReason.Other, "  ");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == QuarantineReasonRule.Code);
    }

    [Fact]
    public async Task Quarantine_WithCode_ThenRelease_ClearsDisposition()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-CODE", productTypeId));
        Assert.True(received.Succeeded, received.Error);
        var released = await service.ReleaseFromQuarantineAsync(received.Unit!.Id, "tech2");
        Assert.True(released.Succeeded, released.Error);
        Assert.Equal(UnitQuarantineReason.Unspecified, released.Unit!.QuarantineReasonCode);

        var quarantined = await service.QuarantineAsync(
            received.Unit.Id, UnitQuarantineReason.LookbackRecall, "Donor notified");
        Assert.True(quarantined.Succeeded, quarantined.Error);
        Assert.Equal(UnitStatus.Quarantine, quarantined.Unit!.Status);
        Assert.Equal(UnitQuarantineReason.LookbackRecall, quarantined.Unit.QuarantineReasonCode);
        Assert.Equal("Donor notified", quarantined.Unit.QuarantineReason);

        var list = await service.ListQuarantineAsync();
        Assert.Contains(list, i => i.UnitNumber == "U-Q-CODE" && i.ReasonCode == UnitQuarantineReason.LookbackRecall);

        var available = await service.ReleaseFromQuarantineAsync(received.Unit.Id, "tech2");
        Assert.True(available.Succeeded, available.Error);
        Assert.Equal(UnitQuarantineReason.Unspecified, available.Unit!.QuarantineReasonCode);
        Assert.Null(available.Unit.QuarantineReason);
    }

    [Fact]
    public async Task ReceiveUnit_MissingProductCode_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-NOPDC", productTypeId, productCode: null));

        Assert.False(result.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnknownProductCode, result.Error);
    }

    [Fact]
    public async Task ReceiveUnit_UnknownProductCode_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-BADPDC", productTypeId, productCode: "EXXXX"));

        Assert.False(result.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnknownProductCode, result.Error);
    }

    [Fact]
    public async Task ReceiveUnit_EightCharProductCodeData_StoresComponents()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-8CHAR", productTypeId, productCode: "E0336000"));

        Assert.True(result.Succeeded);
        Assert.Equal("E0336", result.Unit!.ProductDescriptionCode);
        Assert.Equal("E0336000", result.Unit.ProductCodeData);
        Assert.Equal("0", result.Unit.CollectionTypeCode);
        Assert.Equal("00", result.Unit.DivisionCode);
    }

    [Fact]
    public async Task ReceiveUnit_DuplicateUnitNumber_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        await service.ReceiveUnitAsync(NewUnitRequest("U-DUP", productTypeId));

        var second = await service.ReceiveUnitAsync(NewUnitRequest("U-DUP", productTypeId));
        Assert.False(second.Succeeded);
        Assert.Contains("already exists", second.Error);
    }

    [Fact]
    public async Task ReceiveUnit_PastExpiration_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(
            NewUnitRequest("U-PASTEXP", productTypeId, _factory.Clock.UtcNow.AddHours(-1)));

        Assert.False(result.Succeeded);
        Assert.Contains("future", result.Error);
    }

    [Fact]
    public async Task ReceiveUnit_VisualFail_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-VISFAIL", productTypeId) with { VisualInspectionAcceptable = false });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveVisualInspectionRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-VISFAIL"));
    }

    [Fact]
    public async Task ReceiveUnit_HemolysisAppearance_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-HEMOLYSIS", productTypeId) with { Appearance = UnitAppearance.Hemolysis });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveAppearanceRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-HEMOLYSIS"));
    }

    [Fact]
    public async Task ReceiveUnit_VisualPass_StoresInspection()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-VISOK", productTypeId) with { VisualInspectionNotes = "Clear, no clots" });
        Assert.True(result.Succeeded);
        Assert.True(result.Unit!.ReceiveVisualAcceptable);
        Assert.Equal("Clear, no clots", result.Unit.ReceiveVisualNotes);
        Assert.Equal(UnitAppearance.Acceptable, result.Unit.ReceiveAppearance);
        Assert.Equal(4.0m, result.Unit.ReceiveTemperatureCelsius);
    }

    [Fact]
    public async Task ReceiveUnit_AutologousWithoutRecipient_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-AUTO-MISS", productTypeId) with { DonationRestriction = DonationRestriction.Autologous });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == AutologousDirectedRule.ReceiveCode);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-AUTO-MISS"));
    }

    [Fact]
    public async Task ReceiveUnit_AutologousWithRecipient_StoresRestriction()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long patientId;
        await using (var context = _factory.Create())
        {
            var patient = new Patient
            {
                MedicalRecordNumber = "MRN-AUTO-1",
                LastName = "Auto",
                FirstName = "Donor",
                DateOfBirth = new DateOnly(1980, 1, 1)
            };
            context.Patients.Add(patient);
            await context.SaveChangesAsync();
            patientId = patient.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReceiveUnitAsync(
            NewUnitRequest("U-AUTO-OK", productTypeId) with
            {
                DonationRestriction = DonationRestriction.Autologous,
                ReservedPatientId = patientId
            });
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(DonationRestriction.Autologous, result.Unit!.DonationRestriction);
        Assert.Equal(patientId, result.Unit.ReservedPatientId);
    }

    [Fact]
    public async Task ConvertDirected_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var (unitId, _) = await ReceiveDirectedAsync("U-DIR-NOREASON", productTypeId);
        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(unitId, "  ", "tech2");
        Assert.False(result.Succeeded);
        Assert.Equal("A reason is required to convert a directed unit to allogeneic inventory.", result.Error);
    }

    [Fact]
    public async Task ConvertDirected_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var (unitId, _) = await ReceiveDirectedAsync("U-DIR-PERM", productTypeId);
        await using var act = _factory.Create();
        var denied = CreateService(act, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive));
        var result = await denied.ConvertDirectedToAllogeneicAsync(
            unitId, "Intended recipient discharged", "tech2");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.DirectedConversionCode);
        Assert.Equal(DonationRestriction.Directed, (await act.BloodUnits.FindAsync(unitId))!.DonationRestriction);

        var allowed = CreateService(act, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease));
        var ok = await allowed.ConvertDirectedToAllogeneicAsync(
            unitId, "Intended recipient discharged", "tech2");
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal(DonationRestriction.Allogeneic, ok.Unit!.DonationRestriction);
        Assert.Null(ok.Unit.ReservedPatientId);
    }

    [Fact]
    public async Task ConvertDirected_WithoutVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var (unitId, _) = await ReceiveDirectedAsync("U-DIR-NO2", productTypeId);
        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(unitId, "Intended recipient discharged", null);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == DirectedConversionVerifierRule.Code);
    }

    [Fact]
    public async Task ConvertDirected_Autologous_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var patient = new Patient
            {
                MedicalRecordNumber = "MRN-DIR-AUTO",
                LastName = "Auto",
                FirstName = "Keep",
                DateOfBirth = new DateOnly(1975, 3, 3)
            };
            context.Patients.Add(patient);
            await context.SaveChangesAsync();
            var received = await CreateService(context).ReceiveUnitAsync(
                NewUnitRequest("U-DIR-AUTO", productTypeId) with
                {
                    DonationRestriction = DonationRestriction.Autologous,
                    ReservedPatientId = patient.Id
                });
            Assert.True(received.Succeeded, received.Error);
            unitId = received.Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(unitId, "Should not convert autologous", "tech2");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == AutologousDirectedRule.ConvertCode);
    }

    [Fact]
    public async Task ConvertDirected_FromQuarantine_ClearsReservation()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var (unitId, patientId) = await ReceiveDirectedAsync("U-DIR-OK", productTypeId);
        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(
            unitId, "Intended recipient no longer needs the unit", "tech2");
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(DonationRestriction.Allogeneic, result.Unit!.DonationRestriction);
        Assert.Null(result.Unit.ReservedPatientId);
        Assert.Equal("Intended recipient no longer needs the unit", result.Unit.DirectedConversionReason);
        Assert.Equal("tech-test", result.Unit.DirectedConvertedBy);
        Assert.NotNull(result.Unit.DirectedConvertedUtc);
        Assert.NotEqual(patientId, result.Unit.ReservedPatientId);
    }

    [Fact]
    public async Task ConvertDirected_FromAllocated_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var (unitId, _) = await ReceiveDirectedAsync("U-DIR-ALLOC", productTypeId);
        await using (var setup = _factory.Create())
        {
            var unit = await setup.BloodUnits.FindAsync(unitId);
            unit!.Status = UnitStatus.Allocated;
            await setup.SaveChangesAsync();
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(unitId, "Still reserved", "tech2");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == AutologousDirectedRule.ConvertCode);
    }

    private async Task<(long UnitId, long PatientId)> ReceiveDirectedAsync(string unitNumber, long productTypeId)
    {
        await using var context = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{unitNumber}",
            LastName = "Directed",
            FirstName = "Recipient",
            DateOfBirth = new DateOnly(1988, 4, 4)
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var received = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest(unitNumber, productTypeId) with
            {
                DonationRestriction = DonationRestriction.Directed,
                ReservedPatientId = patient.Id
            });
        Assert.True(received.Succeeded, received.Error);
        return (received.Unit!.Id, patient.Id);
    }

    [Fact]
    public async Task ReceiveUnit_MissingTemperature_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-TEMP-MISS", productTypeId) with { ReceiveTemperatureCelsius = null });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveTemperatureRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-TEMP-MISS"));
    }

    [Fact]
    public async Task ReceiveUnit_TooCold_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-TEMP-COLD", productTypeId) with { ReceiveTemperatureCelsius = 0.0m });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveTemperatureRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-TEMP-COLD"));
    }

    [Fact]
    public async Task ReceiveUnit_TooWarm_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-TEMP-WARM", productTypeId) with { ReceiveTemperatureCelsius = 15.0m });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveTemperatureRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-TEMP-WARM"));
    }

    [Fact]
    public async Task ReceiveUnit_MissingSecondVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-RCV-NO2", productTypeId) with { SecondVerifier = null });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveVerifierRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-RCV-NO2"));
    }

    [Fact]
    public async Task ReceiveUnit_SameUserVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-RCV-SELF", productTypeId) with { SecondVerifier = "tech-test" });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveVerifierRule.Code);
    }

    [Fact]
    public async Task ReceiveUnit_UnknownVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-RCV-UNK", productTypeId) with { SecondVerifier = "not-a-user" });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == SecondVerifierDirectoryRule.Code);
    }

    [Fact]
    public async Task ExpectUnit_CreatesExpected_WithoutVisualGate()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ExpectUnitAsync(
            NewUnitRequest("U-EXPECT-1", productTypeId) with { ShipmentId = "ASN-77" });

        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.Expected, result.Unit!.Status);
        Assert.Equal("ASN-77", result.Unit.ShipmentId);
        Assert.Equal("E0206", result.Unit.ProductDescriptionCode);
        Assert.Equal(_factory.Clock.UtcNow.AddHours(24), result.Unit.ExpectedArrivalDueUtc);
        var history = await context.InventoryStatusHistory.Where(h => h.BloodProductId == result.Unit.Id).ToListAsync();
        var initial = Assert.Single(history);
        Assert.Equal(UnitStatus.Expected, initial.ToStatus);
        Assert.Contains("ASN-77", initial.Reason);
    }

    [Fact]
    public async Task ListExpected_FlagsOverdueWhenPastDue()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var original = _factory.Clock.UtcNow;
        try
        {
            await using (var context = _factory.Create())
            {
                var created = await CreateService(context).ExpectUnitAsync(
                    NewUnitRequest("U-EXPECT-OVERDUE", productTypeId) with { ShipmentId = "ASN-LATE" });
                Assert.True(created.Succeeded, created.Error);
            }

            _factory.Clock.UtcNow = original.AddHours(25);
            await using var act = _factory.Create();
            var list = await CreateService(act).ListExpectedAsync();
            var row = Assert.Single(list, i => i.UnitNumber == "U-EXPECT-OVERDUE");
            Assert.True(row.IsOverdue);
            Assert.Equal("ASN-LATE", row.ShipmentId);
        }
        finally
        {
            _factory.Clock.UtcNow = original;
        }
    }

    [Fact]
    public async Task ReceiveExpected_WhenOverdue_StillSucceedsAndAuditsLate()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var original = _factory.Clock.UtcNow;
        long unitId;
        try
        {
            await using (var context = _factory.Create())
            {
                unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-LATE-OK", productTypeId))).Unit!.Id;
            }

            _factory.Clock.UtcNow = original.AddHours(25);
            await using var act = _factory.Create();
            var result = await CreateService(act).ReceiveExpectedUnitAsync(
                unitId,
                new ReceiveExpectedUnitRequest(
                    SecondVerifier: "tech2",
                    ReceiveTemperatureCelsius: 4.0m));
            Assert.True(result.Succeeded, result.Error);
            Assert.NotEqual(UnitStatus.Expected, result.Unit!.Status);

            var history = await act.InventoryStatusHistory
                .Where(h => h.BloodProductId == unitId && h.ToStatus != UnitStatus.Expected)
                .ToListAsync();
            Assert.Contains(history, h => h.Reason != null && h.Reason.Contains("late arrival"));
        }
        finally
        {
            _factory.Clock.UtcNow = original;
        }
    }

    [Fact]
    public async Task ExpectUnit_DuplicateNumber_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        await service.ExpectUnitAsync(NewUnitRequest("U-EXPECT-DUP", productTypeId));
        var second = await service.ExpectUnitAsync(NewUnitRequest("U-EXPECT-DUP", productTypeId));
        Assert.False(second.Succeeded);
        Assert.Contains("already exists", second.Error);
    }

    [Fact]
    public async Task ReceiveExpected_VisualFail_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-VIS", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).ReceiveExpectedUnitAsync(
                unitId, new ReceiveExpectedUnitRequest(VisualInspectionAcceptable: false));
            Assert.False(result.Succeeded);
            Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveVisualInspectionRule.Code);
        }

        await using var verify = _factory.Create();
        var unit = await verify.BloodUnits.SingleAsync(u => u.Id == unitId);
        Assert.Equal(UnitStatus.Expected, unit.Status);
    }

    [Fact]
    public async Task ReceiveExpected_VisualPass_LandsInQuarantine()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-OK", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReceiveExpectedUnitAsync(
            unitId, new ReceiveExpectedUnitRequest(
                VisualInspectionNotes: "Bag intact",
                SecondVerifier: "tech2",
                ReceiveTemperatureCelsius: 4.0m));
        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.Quarantine, result.Unit!.Status);
        Assert.True(result.Unit.ReceiveVisualAcceptable);
        Assert.Equal("Bag intact", result.Unit.ReceiveVisualNotes);
        Assert.Equal(4.0m, result.Unit.ReceiveTemperatureCelsius);
    }

    [Fact]
    public async Task ReceiveExpected_MissingTemperature_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-TEMP", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReceiveExpectedUnitAsync(
            unitId, new ReceiveExpectedUnitRequest(SecondVerifier: "tech2"));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveTemperatureRule.Code);

        await using var verify = _factory.Create();
        var unit = await verify.BloodUnits.SingleAsync(u => u.Id == unitId);
        Assert.Equal(UnitStatus.Expected, unit.Status);
    }

    [Fact]
    public async Task ReceiveExpected_RetypeProduct_LandsInReceived()
    {
        await EnsureProductCodesAsync();
        long productTypeId;
        await using (var context = _factory.Create())
        {
            var type = new ProductType
            {
                ProductCode = "RBC-EXPECT-RETYPE",
                Name = "Expect Retype RBC",
                ComponentClass = ComponentClass.RedBloodCells,
                RequiresRetype = true
            };
            context.ProductTypes.Add(type);
            await context.SaveChangesAsync();
            productTypeId = type.Id;
        }

        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-RET", productTypeId))).Unit!.Id;
        }

        await using var receive = _factory.Create();
        var result = await CreateService(receive).ReceiveExpectedUnitAsync(
            unitId, new ReceiveExpectedUnitRequest(SecondVerifier: "tech2", ReceiveTemperatureCelsius: 4.0m));
        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.Received, result.Unit!.Status);
    }

    [Fact]
    public async Task CancelExpected_MovesToCancelledAssignment()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-CXL", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).CancelExpectedUnitAsync(unitId, "Supplier cancelled ASN");
        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.CancelledAssignment, result.Unit!.Status);
    }

    [Fact]
    public async Task ExpectUnit_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .ExpectUnitAsync(NewUnitRequest("U-EXPECT-PERM", productTypeId));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.ExpectCode);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-EXPECT-PERM"));

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .ExpectUnitAsync(NewUnitRequest("U-EXPECT-PERM", productTypeId));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Expected, allowed.Unit!.Status);
    }

    [Fact]
    public async Task CancelExpected_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            unitId = (await CreateService(setup).ExpectUnitAsync(NewUnitRequest("U-EXPECT-CXL-PERM", productTypeId))).Unit!.Id;
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .CancelExpectedUnitAsync(unitId, "Supplier cancelled ASN");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.CancelExpectedCode);
        Assert.Equal(UnitStatus.Expected, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .CancelExpectedUnitAsync(unitId, "Supplier cancelled ASN");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.CancelledAssignment, allowed.Unit!.Status);
    }

    [Fact]
    public async Task ReceiveExpected_NotExpected_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-WALKIN", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReceiveExpectedUnitAsync(unitId, new ReceiveExpectedUnitRequest());
        Assert.False(result.Succeeded);
        Assert.Contains("expected", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Release_QuarantineToAvailable_AppendsHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var received = await service.ReceiveUnitAsync(NewUnitRequest("U-REL", productTypeId));
            unitId = received.Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.ReleaseFromQuarantineAsync(unitId, "tech2");
            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Available, result.Unit!.Status);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Equal(2, history.Count);
            Assert.Contains(history, h => h.FromStatus == UnitStatus.Quarantine && h.ToStatus == UnitStatus.Available);
            Assert.Contains(history, h => h.Reason != null && h.Reason.Contains("tech2"));
        }
    }

    [Fact]
    public async Task Release_WithoutSecondVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-REL-NO2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var result = await CreateService(ctx).ReleaseFromQuarantineAsync(unitId);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == QuarantineReleaseVerifierRule.Code);
    }

    [Fact]
    public async Task Release_SameUserOrUnknownVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-REL-BAD2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var service = CreateService(ctx);
        var same = await service.ReleaseFromQuarantineAsync(unitId, "tech-test");
        Assert.Contains(same.Evaluation!.HardStops, r => r.Code == QuarantineReleaseVerifierRule.Code);

        var unknown = await service.ReleaseFromQuarantineAsync(unitId, "not-a-user");
        Assert.Contains(unknown.Evaluation!.HardStops, r => r.Code == SecondVerifierDirectoryRule.Code);
    }

    [Fact]
    public async Task Hold_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-NR", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).HoldAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("hold reason is required", result.Error);
        }
    }

    [Fact]
    public async Task Hold_ThenRelease_ReturnsAvailable_AndClearsReason()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-REL", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var held = await CreateService(context).HoldAsync(unitId, "Pending packing slip");
            Assert.True(held.Succeeded);
            Assert.Equal(UnitStatus.OnHold, held.Unit!.Status);
            Assert.Equal("Pending packing slip", held.Unit.HoldReason);
        }

        await using (var context = _factory.Create())
        {
            var released = await CreateService(context).ReleaseFromHoldAsync(unitId);
            Assert.True(released.Succeeded);
            Assert.Equal(UnitStatus.Available, released.Unit!.Status);
            Assert.Null(released.Unit.HoldReason);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Contains(history, h => h.FromStatus == UnitStatus.Available && h.ToStatus == UnitStatus.OnHold);
            Assert.Contains(history, h => h.FromStatus == UnitStatus.OnHold && h.ToStatus == UnitStatus.Available);
        }
    }

    [Fact]
    public async Task ReleaseFromHold_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-PERM", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
            await CreateService(context).HoldAsync(unitId, "Pending packing slip");
        }

        await using var act = _factory.Create();
        var denied = CreateService(act, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive));
        var result = await denied.ReleaseFromHoldAsync(unitId);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.HoldReleaseCode);
        Assert.Equal(UnitStatus.OnHold, (await act.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = CreateService(act, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease));
        var ok = await allowed.ReleaseFromHoldAsync(unitId);
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal(UnitStatus.Available, ok.Unit!.Status);
        Assert.Null(ok.Unit.HoldReason);
    }

    [Fact]
    public async Task Hold_FromQuarantine_IsBlocked()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-Q", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).HoldAsync(unitId, "Should not bypass quarantine");
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Evaluation);
            Assert.True(result.Evaluation!.IsHardStopped);
        }
    }

    [Fact]
    public async Task ReleaseFromHold_WhenNotOnHold_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-NA", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).ReleaseFromHoldAsync(unitId);
            Assert.False(result.Succeeded);
            Assert.Contains("operational hold", result.Error);
        }
    }

    [Fact]
    public async Task MarkMissing_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-MISS-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).MarkMissingAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task MarkMissing_ThenLocate_EntersQuarantine()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-MISS-LOC", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var missing = await CreateService(context).MarkMissingAsync(unitId, "Not on shelf at physical inventory");
            Assert.True(missing.Succeeded);
            Assert.Equal(UnitStatus.Missing, missing.Unit!.Status);
            Assert.Equal("Not on shelf at physical inventory", missing.Unit.MissingReason);
        }

        await using (var context = _factory.Create())
        {
            var located = await CreateService(context).LocateMissingAsync(unitId);
            Assert.True(located.Succeeded);
            Assert.Equal(UnitStatus.Quarantine, located.Unit!.Status);
            Assert.Null(located.Unit.MissingReason);
            Assert.Contains("Located after missing", located.Unit.QuarantineReason);
            Assert.Equal(UnitQuarantineReason.LocatedAfterMissing, located.Unit.QuarantineReasonCode);
        }
    }

    [Fact]
    public async Task Locate_WhenNotMissing_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-MISS-NA", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).LocateMissingAsync(unitId);
            Assert.False(result.Succeeded);
            Assert.Contains("missing unit", result.Error);
        }
    }

    [Fact]
    public async Task LocateMissing_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-LOC-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
            Assert.True((await CreateService(setup).MarkMissingAsync(unitId, "Not on shelf")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .LocateMissingAsync(unitId);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.LocateMissingCode);
        Assert.Equal(UnitStatus.Missing, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .LocateMissingAsync(unitId);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Quarantine, allowed.Unit!.Status);
    }

    [Fact]
    public async Task MarkDamaged_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DMG-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).MarkDamagedAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task MarkDamaged_ThenInspect_EntersQuarantine()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DMG-INSP", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var damaged = await CreateService(context).MarkDamagedAsync(unitId, "Bag leaking in refrigerator");
            Assert.True(damaged.Succeeded);
            Assert.Equal(UnitStatus.Damaged, damaged.Unit!.Status);
            Assert.Equal("Bag leaking in refrigerator", damaged.Unit.DamagedReason);
        }

        await using (var context = _factory.Create())
        {
            var inspected = await CreateService(context).InspectDamagedAsync(unitId);
            Assert.True(inspected.Succeeded);
            Assert.Equal(UnitStatus.Quarantine, inspected.Unit!.Status);
            Assert.Null(inspected.Unit.DamagedReason);
            Assert.Contains("Inspected after damage", inspected.Unit.QuarantineReason);
            Assert.Equal(UnitQuarantineReason.InspectedAfterDamage, inspected.Unit.QuarantineReasonCode);
        }
    }

    [Fact]
    public async Task InspectDamaged_WhenNotDamaged_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DMG-NA", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).InspectDamagedAsync(unitId);
            Assert.False(result.Succeeded);
            Assert.Contains("damaged unit", result.Error);
        }
    }

    [Fact]
    public async Task InspectDamaged_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-INSP-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
            Assert.True((await CreateService(setup).MarkDamagedAsync(unitId, "Leaking bag")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .InspectDamagedAsync(unitId);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.InspectDamagedCode);
        Assert.Equal(UnitStatus.Damaged, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .InspectDamagedAsync(unitId);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Quarantine, allowed.Unit!.Status);
    }

    [Fact]
    public async Task Quarantine_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-Q-SET-PERM", productTypeId));
            Assert.True(received.Succeeded, received.Error);
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .QuarantineAsync(unitId, UnitQuarantineReason.LookbackRecall, "Donor notified");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.QuarantineCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .QuarantineAsync(unitId, UnitQuarantineReason.LookbackRecall, "Donor notified");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Quarantine, allowed.Unit!.Status);
    }

    [Fact]
    public async Task Hold_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-HOLD-SET-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .HoldAsync(unitId, "Pending packing slip");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.PlaceHoldCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .HoldAsync(unitId, "Pending packing slip");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.OnHold, allowed.Unit!.Status);
    }

    [Fact]
    public async Task MarkMissing_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-MISS-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .MarkMissingAsync(unitId, "Not on shelf");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.MarkMissingCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .MarkMissingAsync(unitId, "Not on shelf");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Missing, allowed.Unit!.Status);
    }

    [Fact]
    public async Task MarkDamaged_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-DMG-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .MarkDamagedAsync(unitId, "Bag leaking");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.MarkDamagedCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .MarkDamagedAsync(unitId, "Bag leaking");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Damaged, allowed.Unit!.Status);
    }

    [Fact]
    public async Task ListDiscrepancy_IncludesMissingAndDamaged_ExcludesAvailable()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);

        var missing = await service.ReceiveUnitAsync(NewUnitRequest("U-DISC-MISS", productTypeId));
        Assert.True(missing.Succeeded, missing.Error);
        var releasedMissing = await service.ReleaseFromQuarantineAsync(missing.Unit!.Id, "tech2");
        Assert.True(releasedMissing.Succeeded, releasedMissing.Error);
        var markedMissing = await service.MarkMissingAsync(missing.Unit.Id, "Not on shelf");
        Assert.True(markedMissing.Succeeded, markedMissing.Error);

        var damaged = await service.ReceiveUnitAsync(NewUnitRequest("U-DISC-DMG", productTypeId));
        Assert.True(damaged.Succeeded, damaged.Error);
        var releasedDamaged = await service.ReleaseFromQuarantineAsync(damaged.Unit!.Id, "tech2");
        Assert.True(releasedDamaged.Succeeded, releasedDamaged.Error);
        var markedDamaged = await service.MarkDamagedAsync(damaged.Unit.Id, "Leaking bag");
        Assert.True(markedDamaged.Succeeded, markedDamaged.Error);

        var available = await service.ReceiveUnitAsync(NewUnitRequest("U-DISC-OK", productTypeId));
        Assert.True(available.Succeeded, available.Error);
        var releasedOk = await service.ReleaseFromQuarantineAsync(available.Unit!.Id, "tech2");
        Assert.True(releasedOk.Succeeded, releasedOk.Error);

        var list = await service.ListDiscrepancyAsync();
        Assert.Contains(list, i => i.UnitNumber == "U-DISC-MISS" && i.Status == UnitStatus.Missing && i.Reason == "Not on shelf");
        Assert.Contains(list, i => i.UnitNumber == "U-DISC-DMG" && i.Status == UnitStatus.Damaged && i.Reason == "Leaking bag");
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-DISC-OK");
    }

    [Fact]
    public async Task ReturnToSupplier_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RTS-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).ReturnToSupplierAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task ReturnToSupplier_FromExpected_IsTerminal()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-RTS-EXP", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReturnToSupplierAsync(unitId, "Hemolysis at consignee receipt");
        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.ReturnedToSupplier, result.Unit!.Status);
        Assert.Equal("Hemolysis at consignee receipt", result.Unit.SupplierReturnReason);
    }

    [Fact]
    public async Task ReturnToSupplier_FromQuarantine_StoresReason()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RTS-Q", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReturnToSupplierAsync(unitId, "Unused stock credit");
        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.ReturnedToSupplier, result.Unit!.Status);
        Assert.Equal("Unused stock credit", result.Unit.SupplierReturnReason);
    }

    [Fact]
    public async Task ReturnToSupplier_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            unitId = (await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-RTS-PERM", productTypeId))).Unit!.Id;
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .ReturnToSupplierAsync(unitId, "Unused stock credit");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.ReturnToSupplierCode);
        Assert.NotEqual(UnitStatus.ReturnedToSupplier, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .ReturnToSupplierAsync(unitId, "Unused stock credit");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.ReturnedToSupplier, allowed.Unit!.Status);
    }

    [Fact]
    public async Task ReturnToSupplier_FromIssued_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RTS-ISS", productTypeId))).Unit!.Id;
            var unit = await context.BloodUnits.SingleAsync(u => u.Id == unitId);
            unit.Status = UnitStatus.Issued;
            await context.SaveChangesAsync();
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReturnToSupplierAsync(unitId, "Should not leave the ward this way");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == InventoryStatusTransition.IllegalTransitionCode);
    }

    [Fact]
    public async Task Discard_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task Discard_WithoutInventoryDiscard_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-PERM", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var denied = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .DiscardAsync(unitId, "Bag integrity compromised", "tech2");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.DiscardCode);
        Assert.NotEqual(UnitStatus.Discarded, (await ctx.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryDiscard))
            .DiscardAsync(unitId, "Bag integrity compromised", "tech2");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Discarded, allowed.Unit!.Status);
    }

    [Fact]
    public async Task Discard_SetsStatus_AndWritesDiscardAudit()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "Bag integrity compromised", "tech2");
            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Discarded, result.Unit!.Status);
        }

        await using (var verify = _factory.Create())
        {
            var discardAudit = await verify.AuditEvents
                .Where(a => a.EntityType == nameof(BloodUnit) && a.EntityId == unitId && a.EventType == AuditEventType.Discard)
                .SingleAsync();
            Assert.Contains("Bag integrity compromised", discardAudit.Reason);
            Assert.Contains("tech2", discardAudit.Reason);
        }
    }

    [Fact]
    public async Task Discard_WithoutSecondVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-NO2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var result = await CreateService(ctx).DiscardAsync(unitId, "Bag integrity compromised");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == DiscardVerifierRule.Code);
    }

    [Fact]
    public async Task Discard_SameUserOrUnknownVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-BAD2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var service = CreateService(ctx);
        var same = await service.DiscardAsync(unitId, "attempt", "tech-test");
        Assert.Contains(same.Evaluation!.HardStops, r => r.Code == DiscardVerifierRule.Code);

        var unknown = await service.DiscardAsync(unitId, "attempt", "not-a-user");
        Assert.Contains(unknown.Evaluation!.HardStops, r => r.Code == SecondVerifierDirectoryRule.Code);
    }

    [Fact]
    public async Task Discard_TransfusedUnit_IsBlockedByTransitionGuard()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var unit = new BloodUnit
            {
                UnitNumber = "U-TX",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
                Status = UnitStatus.Transfused
            };
            context.BloodUnits.Add(unit);
            await context.SaveChangesAsync();
            unitId = unit.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "attempt");
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Evaluation);
            Assert.True(result.Evaluation!.IsHardStopped);
        }
    }

    [Fact]
    public async Task Transfer_ChangesLocation_AndAppendsHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        long toLocationId;

        await using (var context = _factory.Create())
        {
            var location = new InventoryLocation { Code = "LOC-XFER", Name = "Transfer Target", LocationType = LocationType.Refrigerator };
            context.InventoryLocations.Add(location);
            await context.SaveChangesAsync();
            toLocationId = location.Id;

            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-XFER", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).TransferAsync(unitId, toLocationId, "Move to issue fridge");
            Assert.True(result.Succeeded);
            Assert.Equal(toLocationId, result.Unit!.CurrentLocationId);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Contains(history, h => h.ToLocationId == toLocationId && h.FromStatus == h.ToStatus);
        }
    }

    [Fact]
    public async Task Transfer_WithoutInventoryTransfer_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        long toLocationId;

        await using (var context = _factory.Create())
        {
            var location = new InventoryLocation { Code = "LOC-XFER-PERM", Name = "Transfer Target", LocationType = LocationType.Refrigerator };
            context.InventoryLocations.Add(location);
            await context.SaveChangesAsync();
            toLocationId = location.Id;
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-XFER-PERM", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var priorLocation = (await ctx.BloodUnits.FindAsync(unitId))!.CurrentLocationId;
        var denied = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .TransferAsync(unitId, toLocationId, "Move to issue fridge");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.TransferCode);
        Assert.Equal(priorLocation, (await ctx.BloodUnits.FindAsync(unitId))!.CurrentLocationId);

        var allowed = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryTransfer))
            .TransferAsync(unitId, toLocationId, "Move to issue fridge");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(toLocationId, allowed.Unit!.CurrentLocationId);
    }

    [Fact]
    public async Task Recall_WithoutInventoryRecall_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RCL-PERM", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var denied = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .RecallAsync(unitId, "Donor subsequently reactive");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.RecallCode);
        Assert.NotEqual(UnitStatus.Recalled, (await ctx.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRecall))
            .RecallAsync(unitId, "Donor subsequently reactive");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Recalled, allowed.Unit!.Status);
    }

    [Fact]
    public async Task RecallForLookback_DoesNotRequireInventoryRecall()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RCL-LB", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var result = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .RecallForLookbackAsync(unitId, "Donor subsequently reactive");
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(UnitStatus.Recalled, result.Unit!.Status);
    }

    [Fact]
    public async Task ExpireDueUnits_MovesOnlyPastDueUnits()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long pastDueId;
        long futureId;

        await using (var context = _factory.Create())
        {
            var pastDue = new BloodUnit
            {
                UnitNumber = "U-PASTDUE",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = UnitStatus.Available
            };
            var future = new BloodUnit
            {
                UnitNumber = "U-FUTURE",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(5),
                Status = UnitStatus.Available
            };
            context.BloodUnits.AddRange(pastDue, future);
            await context.SaveChangesAsync();
            pastDueId = pastDue.Id;
            futureId = future.Id;
        }

        await using (var context = _factory.Create())
        {
            var expired = await CreateService(context).ExpireDueUnitsAsync();
            Assert.True(expired.Succeeded, expired.Error);
            Assert.True(expired.AffectedCount >= 1);
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(UnitStatus.Expired, (await verify.BloodUnits.FindAsync(pastDueId))!.Status);
            Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(futureId))!.Status);
        }
    }

    [Fact]
    public async Task ExpireDueUnits_WithoutInventoryDiscard_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long pastDueId;
        await using (var setup = _factory.Create())
        {
            var pastDue = new BloodUnit
            {
                UnitNumber = "U-EXP-PERM",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = UnitStatus.Available
            };
            setup.BloodUnits.Add(pastDue);
            await setup.SaveChangesAsync();
            pastDueId = pastDue.Id;
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .ExpireDueUnitsAsync();
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.ExpireCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(pastDueId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryDiscard))
            .ExpireDueUnitsAsync();
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.AffectedCount >= 1);
        Assert.Equal(UnitStatus.Expired, (await context.BloodUnits.FindAsync(pastDueId))!.Status);
    }

    [Fact]
    public async Task Search_FiltersByStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            await service.ReceiveUnitAsync(NewUnitRequest("U-SEARCH-Q", productTypeId)); // stays Quarantine
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var quarantined = await service.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Quarantine));
            Assert.Contains(quarantined, u => u.UnitNumber == "U-SEARCH-Q");
            Assert.All(quarantined, u => Assert.Equal(UnitStatus.Quarantine, u.Status));
        }
    }

    [Fact]
    public async Task ReceiveUnit_RequiresRetype_CreatesReceivedUnit()
    {
        await EnsureProductCodesAsync();
        long productTypeId;
        await using (var context = _factory.Create())
        {
            var type = new ProductType
            {
                ProductCode = "RBC-RETYPE",
                Name = "Retype RBC",
                ComponentClass = ComponentClass.RedBloodCells,
                RequiresRetype = true
            };
            context.ProductTypes.Add(type);
            await context.SaveChangesAsync();
            productTypeId = type.Id;
        }

        await using var receive = _factory.Create();
        var service = CreateService(receive);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-RETYPE-1", productTypeId));

        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.Received, result.Unit!.Status);
        var history = await receive.InventoryStatusHistory.Where(h => h.BloodProductId == result.Unit.Id).ToListAsync();
        var initial = Assert.Single(history);
        Assert.Equal(UnitStatus.Received, initial.ToStatus);
        Assert.Contains("retype", initial.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
