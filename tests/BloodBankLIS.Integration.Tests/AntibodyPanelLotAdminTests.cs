using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AntibodyPanelLotAdminTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AntibodyPanelLotAdminTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Deactivate_WithoutAdminConfigActivate_IsRejected()
    {
        var lotId = await SeedLotAsync("GHP-ADMIN-DENY");

        await using var c = _factory.Create();
        var denied = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .DeactivateAsync(lotId, "QC failure.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r =>
            r.Code == AntibodyPanelLotAdminRule.DeactivatePermCode);
        Assert.True((await c.AntibodyPanelLots.SingleAsync(l => l.Id == lotId)).IsActive);
    }

    [Fact]
    public async Task Deactivate_WithoutReason_IsHardStop()
    {
        var lotId = await SeedLotAsync("GHP-ADMIN-NOREASON");

        await using var c = _factory.Create();
        var denied = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .DeactivateAsync(lotId, "  ");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r =>
            r.Code == AntibodyPanelLotAdminRule.DeactivateReasonCode);
        Assert.True((await c.AntibodyPanelLots.SingleAsync(l => l.Id == lotId)).IsActive);
    }

    [Fact]
    public async Task Deactivate_WritesAudit_AndActivateRestores()
    {
        var lotId = await SeedLotAsync("GHP-ADMIN-OK");

        await using var c = _factory.Create();
        var svc = Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate));
        var deactivated = await svc.DeactivateAsync(lotId, "Manufacturer recall.");
        Assert.True(deactivated.Succeeded, deactivated.Error);
        Assert.False(deactivated.Value!.IsActive);
        Assert.False((await c.AntibodyPanelLots.SingleAsync(l => l.Id == lotId)).IsActive);
        Assert.True(await c.AuditEvents.AnyAsync(e =>
            e.EntityType == nameof(AntibodyPanelLot)
            && e.EventType == AuditEventType.Deactivate
            && e.EntityId == lotId));

        var activated = await svc.ActivateAsync(lotId, "Recall lifted.");
        Assert.True(activated.Succeeded, activated.Error);
        Assert.True(activated.Value!.IsActive);
        Assert.True((await c.AntibodyPanelLots.SingleAsync(l => l.Id == lotId)).IsActive);
    }

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        var (manufacturerId, attrId) = await SeedManufacturerAndKellAsync("GHP-CREATE-DENY");

        await using var c = _factory.Create();
        var denied = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .CreateAsync(ValidCreate(manufacturerId, attrId, "GHP-CREATE-DENY-LOT"));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r =>
            r.Code == AntibodyPanelLotAdminRule.CreatePermCode);
        Assert.False(await c.AntibodyPanelLots.AnyAsync(l => l.LotNumber == "GHP-CREATE-DENY-LOT"));
    }

    [Fact]
    public async Task Create_WithoutCells_IsHardStop()
    {
        var (manufacturerId, attrId) = await SeedManufacturerAndKellAsync("GHP-CREATE-NOCELL");

        await using var c = _factory.Create();
        var denied = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(new CreateAntibodyPanelLotRequest(
                manufacturerId, "GHP-CREATE-NOCELL-LOT", "Empty panel",
                DateOnly.FromDateTime(_factory.Clock.UtcNow.Date.AddYears(1)),
                false, []));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r =>
            r.Code == AntibodyPanelLotAdminRule.CellsCode);
        _ = attrId;
    }

    [Fact]
    public async Task Create_WithCellAndAntigen_WritesAudit_AndDoesNotIdentify()
    {
        var (manufacturerId, attrId) = await SeedManufacturerAndKellAsync("GHP-CREATE-OK");

        await using var c = _factory.Create();
        var historyBefore = await c.AntibodyHistory.CountAsync();
        var created = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(ValidCreate(manufacturerId, attrId, "GHP-CREATE-OK-LOT"));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("GHP-CREATE-OK-LOT", created.Value!.LotNumber);
        Assert.True(created.Value.IsActive);
        Assert.False(created.Value.IsExpired);
        Assert.True(await c.AntibodyPanelCells.AnyAsync(cell => cell.LotId == created.Value.Id));
        Assert.True(await c.AntibodyPanelCellAntigens.AnyAsync(a =>
            a.BloodAttributeDefinitionId == attrId));
        Assert.True(await c.AuditEvents.AnyAsync(e =>
            e.EntityType == nameof(AntibodyPanelLot)
            && e.EventType == AuditEventType.TestChange
            && e.EntityId == created.Value.Id));
        Assert.Equal(historyBefore, await c.AntibodyHistory.CountAsync());
    }

    [Fact]
    public async Task Get_AfterCreate_ReturnsCellsAndAntigens()
    {
        var (manufacturerId, attrId) = await SeedManufacturerAndKellAsync("GHP-GET-OK");

        await using var c = _factory.Create();
        var svc = Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit));
        var created = await svc.CreateAsync(ValidCreate(manufacturerId, attrId, "GHP-GET-OK-LOT"));
        Assert.True(created.Succeeded, created.Error);

        var detail = await svc.GetAsync(created.Value!.Id);
        Assert.NotNull(detail);
        Assert.Equal("GHP-GET-OK-LOT", detail.Lot.LotNumber);
        var cell = Assert.Single(detail.Cells);
        Assert.Equal("1", cell.CellNumber);
        Assert.Equal(PanelCellRole.Panel, cell.Role);
        var antigen = Assert.Single(cell.Antigens);
        Assert.Equal(attrId, antigen.BloodAttributeDefinitionId);
        Assert.Equal("K", antigen.AntigenCode);
        Assert.Equal(AntigenExpression.Present, antigen.Expression);
        Assert.Null(await svc.GetAsync(long.MaxValue));
    }

    [Fact]
    public async Task GetAndList_ShowOpenWorkupsUsingLot()
    {
        var (manufacturerId, attrId) = await SeedManufacturerAndKellAsync("GHP-OPEN-WL");
        await using var setup = _factory.Create();
        var created = await Admin(setup, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(ValidCreate(manufacturerId, attrId, "GHP-OPEN-WL-LOT"));
        Assert.True(created.Succeeded, created.Error);
        var lotId = created.Value!.Id;
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-ABID-LOT-OPEN",
            LastName = "Lot",
            FirstName = "Open",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Sex = Sex.Unknown
        };
        setup.Patients.Add(patient);
        await setup.SaveChangesAsync();
        var workup = new AntibodyIdentificationWorkup
        {
            PatientId = patient.Id,
            PrimaryLotId = lotId,
            Status = AntibodyWorkupStatus.InProgress
        };
        setup.AntibodyIdentificationWorkups.Add(workup);
        await setup.SaveChangesAsync();
        setup.AntibodyIdentificationWorkupLots.Add(new AntibodyIdentificationWorkupLot
        {
            WorkupId = workup.Id,
            LotId = lotId,
            IsPrimary = true
        });
        await setup.SaveChangesAsync();

        await using var check = _factory.Create();
        var svc = Admin(check);
        var listed = await svc.ListAsync(includeInactive: true);
        var row = Assert.Single(listed, l => l.Id == lotId);
        Assert.Equal(1, row.OpenWorkupCount);

        var detail = await svc.GetAsync(lotId);
        Assert.NotNull(detail);
        var open = Assert.Single(detail.OpenWorkups);
        Assert.Equal(workup.Id, open.WorkupId);
        Assert.Equal("MRN-ABID-LOT-OPEN", open.PatientMrn);
        Assert.Equal("Lot, Open", open.PatientName);
        Assert.Equal(AntibodyIdWorklistNextAction.RecordReactions, open.NextAction);

        var manufacturer = Assert.Single(
            await svc.ListManufacturersAsync(includeInactive: true),
            m => m.Id == manufacturerId);
        Assert.Equal(1, manufacturer.OpenWorkupCount);

        var mfgDetail = await svc.GetManufacturerAsync(manufacturerId);
        Assert.NotNull(mfgDetail);
        var mfgOpen = Assert.Single(mfgDetail.OpenWorkups);
        Assert.Equal(workup.Id, mfgOpen.WorkupId);
        Assert.Equal("MRN-ABID-LOT-OPEN", mfgOpen.PatientMrn);
        Assert.Equal("GHP-OPEN-WL-LOT", mfgOpen.LotNumber);
        Assert.Empty(detail.CompletedWorkups);
        Assert.Empty(mfgDetail.CompletedWorkups);
        Assert.Equal(0, row.CompletedWorkupCount);
        Assert.Equal(0, row.PostedHistoryWorkupCount);
        Assert.Equal(0, manufacturer.CompletedWorkupCount);
        Assert.Equal(0, manufacturer.PostedHistoryWorkupCount);

        var completedPatient = new Patient
        {
            MedicalRecordNumber = "MRN-ABID-LOT-DONE",
            LastName = "Lot",
            FirstName = "Done",
            DateOfBirth = new DateOnly(1975, 2, 2),
            Sex = Sex.Unknown
        };
        setup.Patients.Add(completedPatient);
        await setup.SaveChangesAsync();
        var completed = new AntibodyIdentificationWorkup
        {
            PatientId = completedPatient.Id,
            PrimaryLotId = lotId,
            Status = AntibodyWorkupStatus.Completed
        };
        setup.AntibodyIdentificationWorkups.Add(completed);
        await setup.SaveChangesAsync();
        setup.AntibodyIdentificationWorkupLots.Add(new AntibodyIdentificationWorkupLot
        {
            WorkupId = completed.Id,
            LotId = lotId,
            IsPrimary = true
        });
        var voided = new AntibodyIdentificationWorkup
        {
            PatientId = patient.Id,
            PrimaryLotId = lotId,
            Status = AntibodyWorkupStatus.Voided
        };
        setup.AntibodyIdentificationWorkups.Add(voided);
        await setup.SaveChangesAsync();
        setup.AntibodyIdentificationWorkupLots.Add(new AntibodyIdentificationWorkupLot
        {
            WorkupId = voided.Id,
            LotId = lotId,
            IsPrimary = true
        });
        await setup.SaveChangesAsync();
        var selected = await Admin(setup, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(ValidCreate(manufacturerId, attrId, "GHP-OPEN-SEL-LOT", selected: true));
        Assert.True(selected.Succeeded, selected.Error);
        setup.AntibodyIdentificationWorkupLots.Add(new AntibodyIdentificationWorkupLot
        {
            WorkupId = workup.Id,
            LotId = selected.Value!.Id,
            IsPrimary = false
        });
        setup.AntibodyIdentificationFindings.Add(new AntibodyIdentificationFinding
        {
            WorkupId = completed.Id,
            Specificity = "anti-K",
            Classification = AntibodyIdClassification.Identified,
            Source = AntibodyIdSource.Technologist,
            PostedToHistory = true
        });
        await setup.SaveChangesAsync();

        await using var recall = _factory.Create();
        var recallSvc = Admin(recall);
        var recallRow = Assert.Single(await recallSvc.ListAsync(includeInactive: true), l => l.Id == lotId);
        Assert.Equal(1, recallRow.OpenWorkupCount);
        Assert.Equal(1, recallRow.CompletedWorkupCount);
        Assert.Equal(1, recallRow.PostedHistoryWorkupCount);

        var recallDetail = await recallSvc.GetAsync(lotId);
        Assert.NotNull(recallDetail);
        Assert.Equal(workup.Id, Assert.Single(recallDetail.OpenWorkups).WorkupId);
        var completedOpen = Assert.Single(recallDetail.CompletedWorkups);
        Assert.Equal(completed.Id, completedOpen.WorkupId);
        Assert.Equal("MRN-ABID-LOT-DONE", completedOpen.PatientMrn);
        Assert.Equal(1, completedOpen.PostedHistoryCount);
        Assert.Equal(0, Assert.Single(recallDetail.OpenWorkups).PostedHistoryCount);

        var recallMfg = Assert.Single(
            await recallSvc.ListManufacturersAsync(includeInactive: true),
            m => m.Id == manufacturerId);
        Assert.Equal(1, recallMfg.OpenWorkupCount);
        Assert.Equal(1, recallMfg.CompletedWorkupCount);
        Assert.Equal(1, recallMfg.PostedHistoryWorkupCount);
        var recallMfgDetail = await recallSvc.GetManufacturerAsync(manufacturerId);
        Assert.NotNull(recallMfgDetail);
        var recallOpen = Assert.Single(recallMfgDetail.OpenWorkups);
        Assert.Contains("GHP-OPEN-WL-LOT", recallOpen.LotNumber);
        Assert.Contains("GHP-OPEN-SEL-LOT", recallOpen.LotNumber);
        Assert.Equal(completed.Id, Assert.Single(recallMfgDetail.CompletedWorkups).WorkupId);
        Assert.Equal("GHP-OPEN-WL-LOT", Assert.Single(recallMfgDetail.CompletedWorkups).LotNumber);
        Assert.Null(await svc.GetManufacturerAsync(long.MaxValue));
    }

    [Fact]
    public async Task CreateManufacturer_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var denied = await Admin(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .CreateManufacturerAsync(new CreateAntibodyPanelManufacturerRequest("NEW-MFG", "New Panels"));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r =>
            r.Code == AntibodyPanelManufacturerAdminRule.CreatePermCode);
        Assert.False(await c.AntibodyPanelManufacturers.AnyAsync(m => m.Code == "NEW-MFG"));
    }

    [Fact]
    public async Task CreateManufacturer_ThenLot_DoesNotIdentify()
    {
        var attrId = await SeedKellAsync();

        await using var c = _factory.Create();
        var historyBefore = await c.AntibodyHistory.CountAsync();
        var svc = Admin(c, new FixedPermissionEvaluator(
            1, PermissionCodes.AdminConfigEdit, PermissionCodes.AdminConfigActivate));
        var code = $"M{Guid.NewGuid():N}"[..12];
        var manufacturer = await svc.CreateManufacturerAsync(
            new CreateAntibodyPanelManufacturerRequest(code, "Bio-Rad Teaching"));
        Assert.True(manufacturer.Succeeded, manufacturer.Error);
        Assert.True(manufacturer.Value!.IsActive);

        var lot = await svc.CreateAsync(ValidCreate(manufacturer.Value.Id, attrId, "BIO-2026A"));
        Assert.True(lot.Succeeded, lot.Error);
        Assert.Equal(manufacturer.Value.Id, lot.Value!.ManufacturerId);

        var withdrawn = await svc.DeactivateManufacturerAsync(manufacturer.Value.Id, "Vendor contract ended.");
        Assert.True(withdrawn.Succeeded, withdrawn.Error);
        Assert.False(withdrawn.Value!.IsActive);
        Assert.DoesNotContain(
            (await svc.ListManufacturersAsync(includeInactive: false)).Select(m => m.Id),
            id => id == manufacturer.Value.Id);

        var blockedLot = await svc.CreateAsync(ValidCreate(manufacturer.Value.Id, attrId, "BIO-2026B"));
        Assert.False(blockedLot.Succeeded);
        Assert.Contains(blockedLot.Evaluation!.HardStops, r =>
            r.Code == AntibodyPanelLotAdminRule.ManufacturerCode);
        Assert.Equal(historyBefore, await c.AntibodyHistory.CountAsync());
    }

    private AntibodyPanelLotAdminService Admin(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new AntibodyPanelLotAdminService(
            new EfRepository<AntibodyPanelLot>(c),
            new EfRepository<AntibodyPanelManufacturer>(c),
            new EfRepository<AntibodyPanelCell>(c),
            new EfRepository<AntibodyPanelCellAntigen>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            new EfRepository<AntibodyIdentificationWorkupLot>(c),
            new EfRepository<Patient>(c),
            new EfRepository<AntibodyIdentificationFinding>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private async Task<long> SeedLotAsync(string lotNumber)
    {
        await using var context = _factory.Create();
        var manufacturer = new AntibodyPanelManufacturer
        {
            Code = $"M-{lotNumber}",
            Name = "Greyhound Panels",
            IsActive = true,
            IsDraft = false,
            Version = 1
        };
        context.AntibodyPanelManufacturers.Add(manufacturer);
        await context.SaveChangesAsync();

        var lot = new AntibodyPanelLot
        {
            ManufacturerId = manufacturer.Id,
            LotNumber = lotNumber,
            PanelName = "Admin Panel",
            ExpiresOn = DateOnly.FromDateTime(_factory.Clock.UtcNow.Date.AddYears(1)),
            IsActive = true
        };
        context.AntibodyPanelLots.Add(lot);
        await context.SaveChangesAsync();
        return lot.Id;
    }

    private async Task<long> SeedKellAsync()
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var kell = await context.BloodAttributeDefinitions.FirstOrDefaultAsync(a => a.Code == "K");
        if (kell is null)
        {
            kell = new BloodAttributeDefinition
            {
                Code = "K",
                Name = "Kell",
                AntibodyName = "anti-K",
                IsClinicallySignificant = true,
                SortOrder = 1,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now,
                Version = 1
            };
            context.BloodAttributeDefinitions.Add(kell);
            await context.SaveChangesAsync();
        }

        return kell.Id;
    }

    private async Task<(long ManufacturerId, long AttrId)> SeedManufacturerAndKellAsync(string token)
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var manufacturer = new AntibodyPanelManufacturer
        {
            Code = $"M-{token}"[..Math.Min(12, token.Length + 2)],
            Name = "Greyhound Panels",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        };
        context.AntibodyPanelManufacturers.Add(manufacturer);

        var kell = await context.BloodAttributeDefinitions.FirstOrDefaultAsync(a => a.Code == "K");
        if (kell is null)
        {
            kell = new BloodAttributeDefinition
            {
                Code = "K",
                Name = "Kell",
                AntibodyName = "anti-K",
                IsClinicallySignificant = true,
                SortOrder = 1,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now,
                Version = 1
            };
            context.BloodAttributeDefinitions.Add(kell);
        }

        await context.SaveChangesAsync();
        return (manufacturer.Id, kell.Id);
    }

    private CreateAntibodyPanelLotRequest ValidCreate(
        long manufacturerId,
        long attrId,
        string lotNumber,
        bool selected = false) =>
        new(
            manufacturerId,
            lotNumber,
            selected ? "Selected cells" : "Created panel",
            DateOnly.FromDateTime(_factory.Clock.UtcNow.Date.AddYears(1)),
            selected,
            [
                new CreateAntibodyPanelLotCellRequest(
                    selected ? "S1" : "1",
                    selected ? PanelCellRole.Selected : PanelCellRole.Panel,
                    1,
                    [new CreateAntibodyPanelLotAntigenRequest(attrId, AntigenExpression.Present)])
            ]);
}
