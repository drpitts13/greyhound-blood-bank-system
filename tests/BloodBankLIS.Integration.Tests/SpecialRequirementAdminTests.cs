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
