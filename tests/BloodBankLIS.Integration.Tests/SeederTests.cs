using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class SeederTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SeederTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Seed_PopulatesReferenceAndDemoData_AndIsIdempotent()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context); // second run should not duplicate
        }

        await using (var verify = _factory.Create())
        {
            // Four base products plus the three modification targets.
            Assert.Equal(7, await verify.ProductTypes.CountAsync());
            Assert.True(await verify.ProductTypes.AnyAsync(p => p.ProductCode == "WB" && p.RequiresCrossmatch));
            Assert.True(await verify.ProductTypes.AnyAsync(p => p.ProductCode == "RBC-LR" && p.RequiresRetype));
            Assert.True(await verify.ProductTypes.AnyAsync(p => p.ProductCode == "FFP" && !p.RequiresRetype));
            Assert.True(await verify.TestDefinitions.AnyAsync(t => t.Code == AboRhRetypeRule.TestCode && t.Category == TestCategory.AboRhRetype));
            Assert.Equal(5, await verify.InventoryLocations.CountAsync());
            Assert.True(await verify.AntibodyPanelLots.AnyAsync(l => l.LotNumber == "GHP-ABID-2026A" && l.IsActive));
            Assert.True(await verify.AntibodyPanelCells.CountAsync() >= 10);

            // The original demo patient plus the five extended scenarios.
            Assert.Equal(6, await verify.Patients.CountAsync());
            Assert.Equal(7, await verify.Encounters.CountAsync());
            Assert.Equal(11, await verify.Orders.CountAsync());

            // Three original units, 28 stocked across every ABO/Rh, two modification
            // results, one received by ISBT 128 scan, two waiting for ABO/Rh retype,
            // and one on operational hold.
            Assert.Equal(37, await verify.BloodUnits.CountAsync());
            Assert.True(await verify.BloodUnits.AnyAsync(u => u.Status == UnitStatus.OnHold && u.HoldReason != null));
            Assert.Equal(2, await verify.BloodUnits.CountAsync(u => u.Status == UnitStatus.Received));

            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e => e.RuleCode == AboCompatibilityRule.AboCode && !e.IsOverridable));
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e =>
                e.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode
                && e.IsOverridable
                && e.MinSecurityLevel == 2));
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e => e.RuleCode == CrossmatchValidityRule.Code && !e.IsOverridable));
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e => e.RuleCode == AntibodyHistoryCrossmatchRule.RuleCode && e.IsOverridable));

            // Seeding clinical/reference rows also produced audit events.
            Assert.True(await verify.AuditEvents.AnyAsync());

            Assert.True(await verify.IsbtProductCodes.CountAsync() >= 40);
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p =>
                p.ProductDescriptionCode == "E0336"
                && p.Description.Contains("AS1")
                && p.StandardVersion == UsSupplierProductCodeSeed.StandardVersion));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0701"));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E5165"));

            await AssertExtendedScenariosAsync(verify);
        }
    }

    /// <summary>
    /// Each extended demo scenario must land, otherwise a scenario silently returned early
    /// because a unit or reference row it depends on was missing.
    /// </summary>
    private static async Task AssertExtendedScenariosAsync(BloodBankDbContext verify)
    {
            Assert.True(await verify.PhaseDefinitions.AnyAsync(p => p.Code == "IS" && p.IsActive));
            Assert.True(await verify.PhaseDefinitions.AnyAsync(p => p.Code == "CC" && p.IsCheckCell && !p.IncludeInInterpretation));
            var absc = await verify.TestDefinitions.SingleAsync(t => t.Code == "ABSC" && t.IsActive);
            Assert.Equal(ResultValueType.Subtest, absc.ResultValueType);
            Assert.Contains("Cell1", absc.PanelSubtestsJson);
            Assert.Contains("AHG", absc.PanelSubtestsJson);

            // The neonatal rule adds TSNEO, so the catalog has to define it.
            Assert.True(await verify.TestDefinitions.AnyAsync(t => t.Code == "TSNEO" && t.IsActive));

        var neonate = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0002");
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), neonate.DateOfBirth);

        // Rh negative type drives the Weak D rule, which matches on the canonical
        // interpretation derived from the stored value.
        var aboRh = await verify.TestResults.SingleAsync(r =>
            r.TestCode == "ABORH" && r.Value == AboRhResultValue.Format(AboGroup.A, RhType.Negative));
        Assert.Equal("A Negative", ResultInterpretation.Resolve(aboRh.Interpretation, aboRh.Value));

        Assert.True(await verify.AntibodyHistory.AnyAsync(a => a.AntibodySpecificity == "anti-K" && a.IsActive));
        Assert.True(await verify.AntigenProfiles.AnyAsync(a => a.Result == AntigenResult.Negative));
        Assert.True(await verify.UnitBloodAttributes.AnyAsync(a => a.Result == AntigenResult.Negative));

        var emergency = await verify.Issues.SingleAsync(i => i.IssueType == IssueType.EmergencyRelease);
        Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency, emergency.CrossmatchStatus);
        Assert.NotNull(emergency.OverrideId);
        Assert.True(await verify.Overrides.AnyAsync(o =>
            o.Action == OverrideAction.EmergencyRelease && o.ContextId == emergency.Id));

        var reaction = await verify.TransfusionEvents.SingleAsync(t => t.ReactionSuspected);
        Assert.Equal(TransfusionDisposition.Stopped, reaction.FinalDisposition);
        Assert.True(await verify.Orders.AnyAsync(o => o.OrderType == OrderType.TransfusionReactionWorkup));
        Assert.True(await verify.ReactionInvestigations.AnyAsync(i =>
            i.TransfusionEventId == reaction.Id
            && i.ClericalCheckCompleted
            && i.DatResult == DatWorkupResult.Negative));

        // Modifications consume a source unit and produce a derived one.
        Assert.Equal(2, await verify.UnitModifications.CountAsync());
        Assert.Equal(4, await verify.UnitModificationUnits.CountAsync());
        Assert.True(await verify.UnitModifications.AnyAsync(m => m.ModificationType == ModificationType.Wash));
        Assert.Equal(2, await verify.BloodUnits.CountAsync(u => u.DerivedFromModificationId != null));
        Assert.Equal(2, await verify.BloodUnits.CountAsync(u => u.Status == UnitStatus.Modified));

        var scanned = await verify.BloodUnits.SingleAsync(u => u.Source == ComponentEntrySource.Scanner);
        Assert.Equal(4, await verify.BloodComponentRawScans.CountAsync(s => s.BloodProductId == scanned.Id));
        Assert.True(await verify.BloodComponentScanSessions.AnyAsync(s => s.IsCompleted));
    }

    [Fact]
    public async Task Seed_UpsertsMissingProductCodes_WhenPlaceholderAlreadyPresent()
    {
        await using (var context = _factory.Create())
        {
            // Stale placeholder with a different StandardVersion than the US subset seed.
            if (!await context.IsbtProductCodes.AnyAsync(p =>
                    p.ProductDescriptionCode == "E0206"
                    && p.StandardVersion == "PLACEHOLDER-REQUIRES-ICCBBA"))
            {
                context.IsbtProductCodes.Add(new IsbtProductCode
                {
                    ProductDescriptionCode = "E0206",
                    Description = "PLACEHOLDER — Red Blood Cells",
                    ComponentClass = "RedBloodCells",
                    AttributesJson = "[]",
                    StandardVersion = "PLACEHOLDER-REQUIRES-ICCBBA",
                    IsPlaceholder = true
                });
                await context.SaveChangesAsync();
            }
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var verify = _factory.Create())
        {
            Assert.True(await verify.IsbtProductCodes.CountAsync() >= 40);
            Assert.Equal(1, await verify.IsbtProductCodes.CountAsync(p => p.ProductDescriptionCode == "E0206"));
            var e0206 = await verify.IsbtProductCodes.SingleAsync(p => p.ProductDescriptionCode == "E0206");
            Assert.Equal("RED BLOOD CELLS|CPDA-1/450mL/refg|Irradiated", e0206.Description);
            Assert.Equal(UsSupplierProductCodeSeed.StandardVersion, e0206.StandardVersion);
        }
    }

    [Fact]
    public async Task Seed_AddsRequiredReferenceCodes_WhenTableHasPartialMigrationData()
    {
        await using (var context = _factory.Create())
        {
            context.OrderingLocations.Add(new OrderingLocation
            {
                Code = "CUSTOM",
                Name = "Custom location from SQLite migration",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var verify = _factory.Create())
        {
            Assert.True(await verify.OrderingLocations.AnyAsync(l => l.Code == "CUSTOM"));
            Assert.True(await verify.OrderingLocations.AnyAsync(l => l.Code == "OR"));
            Assert.True(await verify.OrderingLocations.AnyAsync(l => l.Code == "ED"));
            Assert.Equal(6, await verify.Patients.CountAsync());
        }
    }
}
