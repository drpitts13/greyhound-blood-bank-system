using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class SpecialRequirementAdminTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SpecialRequirementAdminTests(SqliteContextFactory factory) => _factory = factory;

    private SpecialRequirementAdminService Admin(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new SpecialRequirementAdminService(
            new EfRepository<SpecialRequirementDefinition>(c),
            new EfRepository<ProductAttribute>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveSpecialRequirementDefinitionRequest Request(string code) =>
        new(code, "Blood warmer", SpecialRequirementLevel.Issuing,
            SpecialRequirementEnforcementKind.RequireIssueAcknowledgment, null, "Ack at issue.", 10, "Catalog.");

    private SpecialRequirementService Chart(BloodBankDbContext c, IFacilityTimeZone? timeZone = null) =>
        new(
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<Patient>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            definitions: new EfRepository<SpecialRequirementDefinition>(c),
            timeZone: timeZone);

    // TEST-BB-412: each table takes only its own kinds, and new typings and modifications name a unit category.
    [Fact]
    public async Task Create_InEachTable_AsksForWhatThatTableNeeds()
    {
        await using var c = _factory.Create();
        if (!await c.ProductAttributes.AnyAsync(a => a.Code == "IRRAD"))
        {
            c.ProductAttributes.Add(new ProductAttribute { Code = "IRRAD", Name = "Irradiated" });
            await c.SaveChangesAsync();
        }

        var admin = Admin(c);
        string Code() => $"T{Guid.NewGuid():N}"[..8];

        var noClass = await admin.CreateAsync(new(Code(), "Irradiated", SpecialRequirementLevel.Unit,
            SpecialRequirementEnforcementKind.RequireProductAttribute, "IRRAD", null, 10, "New entry.",
            SpecialRequirementCategory.UnitModification));
        Assert.Contains(noClass.Evaluation!.HardStops, r => r.Code == "SRDEF.CLASS.REQUIRED");

        var modification = await admin.CreateAsync(new(Code(), "Irradiated", SpecialRequirementLevel.Unit,
            SpecialRequirementEnforcementKind.RequireProductAttribute, "IRRAD", null, 10, "New entry.",
            SpecialRequirementCategory.UnitModification, ComponentClass.Platelets));
        Assert.True(modification.Succeeded, modification.Error);
        Assert.Equal("Irradiated – Platelets", modification.Value!.ChoiceLabel);

        var noGroups = await admin.CreateAsync(new(Code(), "Group O only", SpecialRequirementLevel.Unit,
            SpecialRequirementEnforcementKind.RequireUnitAbo, null, null, 10, "New entry.",
            SpecialRequirementCategory.UnitTyping, ComponentClass.RedBloodCells, AllowedAboGroups: "  "));
        Assert.Contains(noGroups.Evaluation!.HardStops, r => r.Code == "SRDEF.ABO.REQUIRED");

        var typing = await admin.CreateAsync(new(Code(), "Group O only", SpecialRequirementLevel.Unit,
            SpecialRequirementEnforcementKind.RequireUnitAbo, null, null, 10, "New entry.",
            SpecialRequirementCategory.UnitTyping, ComponentClass.RedBloodCells, AllowedAboGroups: "a, o"));
        Assert.True(typing.Succeeded, typing.Error);
        Assert.Equal("O,A", typing.Value!.AllowedAboGroups);

        var wrongKind = await admin.CreateAsync(new(Code(), "Warmer", SpecialRequirementLevel.Issuing,
            SpecialRequirementEnforcementKind.RequireIssueAcknowledgment, null, null, 10, "New entry.",
            SpecialRequirementCategory.UnitTyping, ComponentClass.RedBloodCells));
        Assert.Contains(wrongKind.Evaluation!.HardStops, r => r.Code == "SRDEF.KIND.CATEGORY");

        var noBanner = await admin.CreateAsync(new(Code(), "Call physician", SpecialRequirementLevel.Patient,
            SpecialRequirementEnforcementKind.RequireIssueAcknowledgment, null, null, 10, "New entry.",
            SpecialRequirementCategory.Informational));
        Assert.Contains(noBanner.Evaluation!.HardStops, r => r.Code == "SRDEF.BANNER.REQUIRED");

        var info = await admin.CreateAsync(new(Code(), "Call physician", SpecialRequirementLevel.Patient,
            SpecialRequirementEnforcementKind.RequireIssueAcknowledgment, null, "Call before issue.", 10, "New entry.",
            SpecialRequirementCategory.Informational));
        Assert.True(info.Succeeded, info.Error);
        Assert.Equal(SpecialRequirementEnforcementKind.Informational, info.Value!.EnforcementKind);
    }

    // TEST-BB-412: definitions made before categories keep what they enforce, for all unit categories.
    [Fact]
    public async Task Backfill_PlacesEarlierDefinitions_AndKeepsAllCategories()
    {
        await using var c = _factory.Create();
        var irr = new SpecialRequirementDefinition
        {
            Code = $"BI{Guid.NewGuid():N}"[..8], Name = "Irradiated", Category = (SpecialRequirementCategory)(-1),
            EnforcementKind = SpecialRequirementEnforcementKind.RequireProductAttribute, ProductAttributeCode = "IRRAD", IsActive = true
        };
        var warmer = new SpecialRequirementDefinition
        {
            Code = $"BW{Guid.NewGuid():N}"[..8], Name = "Warmer", Category = (SpecialRequirementCategory)(-1),
            EnforcementKind = SpecialRequirementEnforcementKind.RequireIssueAcknowledgment, IsActive = true
        };
        c.SpecialRequirementDefinitions.AddRange(irr, warmer);
        await c.SaveChangesAsync();

        await c.Database.ExecuteSqlRawAsync(SpecialRequirementCategoryBackfill.Sql("Category = -1"));
        await c.Entry(irr).ReloadAsync();
        await c.Entry(warmer).ReloadAsync();

        Assert.Equal(SpecialRequirementCategory.UnitModification, irr.Category);
        Assert.Null(irr.ComponentClass);
        Assert.Equal(SpecialRequirementEnforcementKind.RequireProductAttribute, irr.EnforcementKind);
        Assert.Equal(SpecialRequirementCategory.IssueProcedure, warmer.Category);
    }

    // TEST-BB-412: chart dates are facility days; the end date must come after the start.
    [Fact]
    public async Task ChartDates_AreFacilityMidnight_AndTheEndMustFollowTheStart()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"DT{Guid.NewGuid():N}"[..8],
            LastName = "Dates",
            FirstName = "Chart",
            DateOfBirth = new DateOnly(1980, 1, 1)
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var zone = FacilityTimeZone.FromId("America/New_York");
        var chart = Chart(c, zone);

        var backwards = await chart.AddAsync(patient.Id, new AddSpecialRequirementRequest(
            SpecialTransfusionRequirementType.Irradiated, "Ordered",
            StartDate: new DateOnly(2026, 6, 10), EndDate: new DateOnly(2026, 6, 10)));
        Assert.False(backwards.Succeeded);
        Assert.Contains("end date must be after the start date", backwards.Error);

        var added = await chart.AddAsync(patient.Id, new AddSpecialRequirementRequest(
            SpecialTransfusionRequirementType.Irradiated, "Ordered",
            StartDate: new DateOnly(2026, 6, 1), EndDate: new DateOnly(2026, 6, 10)));
        Assert.True(added.Succeeded, added.Error);
        // New York is UTC-4 in June: facility midnight is 04:00 UTC.
        Assert.Equal(new DateTime(2026, 6, 1, 4, 0, 0, DateTimeKind.Utc), added.Value!.EffectiveUtc);
        Assert.Equal(new DateTime(2026, 6, 10, 4, 0, 0, DateTimeKind.Utc), added.Value.ExpiresUtc);
    }

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"WR{Guid.NewGuid():N}"[..6];

        var denied = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == SpecialRequirementAuthorizationRule.CreateCode);

        var allowed = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code.ToUpperInvariant(), allowed.Value!.Code);
    }

    [Fact]
    public async Task Activate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Admin(c).CreateAsync(Request($"WR{Guid.NewGuid():N}"[..6]));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .ActivateAsync(created.Value!.Id, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == SpecialRequirementAuthorizationRule.ActivateCode);

        var allowed = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .ActivateAsync(created.Value.Id, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsActive);
    }

    [Fact]
    public async Task ListDtos_MarksEndDatedInactive()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"SR{Guid.NewGuid():N}"[..8],
            LastName = "Req",
            FirstName = "End",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Sex = Sex.Unknown
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();

        c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
        {
            PatientId = patient.Id,
            RequirementType = SpecialTransfusionRequirementType.Irradiated,
            Reason = "Ended",
            EffectiveUtc = _factory.Clock.UtcNow.AddDays(-5),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(-1),
            IsActive = true,
            EnteredBy = "tech-test"
        });
        await c.SaveChangesAsync();

        var service = new SpecialRequirementService(
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<Patient>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            definitions: new EfRepository<SpecialRequirementDefinition>(c));

        var dtos = await service.ListDtosAsync(patient.Id);
        var row = Assert.Single(dtos);
        Assert.True(row.IsActive);
        Assert.False(row.IsClinicallyActive);
    }

    [Fact]
    public async Task ExtendedXmRequirement_ForcesComplexCrossmatch()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"XM{Guid.NewGuid():N}"[..8],
            LastName = "Ext",
            FirstName = "Xm",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Sex = Sex.Unknown
        };
        c.Patients.Add(patient);
        var def = new SpecialRequirementDefinition
        {
            Code = SpecialRequirementCatalog.ExtendedCrossmatch,
            Name = "Extended XM",
            Level = SpecialRequirementLevel.Issuing,
            EnforcementKind = SpecialRequirementEnforcementKind.RequireComplexCrossmatch,
            IsActive = true,
            IsDraft = false,
            Version = 1
        };
        c.SpecialRequirementDefinitions.Add(def);
        await c.SaveChangesAsync();
        c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
        {
            PatientId = patient.Id,
            RequirementDefinitionId = def.Id,
            RequirementType = SpecialTransfusionRequirementType.Other,
            Reason = "Extended XM",
            EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
            IsActive = true,
            EnteredBy = "tech-test"
        });
        await c.SaveChangesAsync();

        var loader = new AntibodyScreenCompatLoader(
            new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<SpecialRequirementDefinition>(c),
            _factory.Clock);

        Assert.True(await loader.HasActiveExtendedCrossmatchRequirementAsync(patient.Id));
        Assert.True(await loader.RequiresComplexCrossmatchAsync(patient.Id));
    }
}
