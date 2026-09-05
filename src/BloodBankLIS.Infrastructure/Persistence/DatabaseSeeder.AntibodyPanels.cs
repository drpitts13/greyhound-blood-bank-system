using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    private static async Task EnsureAntibodyIdentificationPoliciesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await EnsureSettingAsync(
            context,
            FacilityPolicyKeys.AntibodyIdDosageAware,
            "true",
            "AntibodyId",
            "Heterozygous cells do not rule out dosage-sensitive antibodies during assistance.",
            ct);
        await EnsureSettingAsync(
            context,
            FacilityPolicyKeys.AntibodyIdMinHomozygousExclusions,
            "1",
            "AntibodyId",
            "Minimum homozygous negative cells required to exclude a dosage-sensitive antibody.",
            ct);
        await EnsureSettingAsync(
            context,
            FacilityPolicyKeys.AntibodyIdMinHeterozygousExclusions,
            "2",
            "AntibodyId",
            "Minimum antigen-positive negative cells required to exclude when dosage-aware evaluation is off.",
            ct);
        await EnsureSettingAsync(
            context,
            FacilityPolicyKeys.AntibodyIdRequireSupervisorReview,
            "true",
            "AntibodyId",
            "Supervisor review is required before an antibody-identification workup can complete.",
            ct);
        await EnsureSettingAsync(
            context,
            FacilityPolicyKeys.AntibodyIdBlockSelfReview,
            "true",
            "AntibodyId",
            "The interpreting technologist cannot also perform supervisor review.",
            ct);
    }

    private static async Task SeedAntibodyIdentificationPanelsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.AntibodyPanelManufacturers.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var manufacturer = new AntibodyPanelManufacturer
        {
            Code = "GHP",
            Name = "Greyhound Teaching Panels",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        };
        context.AntibodyPanelManufacturers.Add(manufacturer);
        await context.SaveChangesAsync(ct);

        var attrs = await context.BloodAttributeDefinitions.AsNoTracking()
            .Where(a => a.IsActive)
            .ToDictionaryAsync(a => a.Code, StringComparer.Ordinal, ct);
        if (attrs.Count == 0)
        {
            return;
        }

        var panel = new AntibodyPanelLot
        {
            ManufacturerId = manufacturer.Id,
            LotNumber = "GHP-ABID-2026A",
            ExpiresOn = new DateOnly(2027, 12, 31),
            PanelName = "Teaching 8-cell antibody ID panel",
            IsSelectedCellLot = false,
            IsActive = true
        };
        var selected = new AntibodyPanelLot
        {
            ManufacturerId = manufacturer.Id,
            LotNumber = "GHP-SEL-2026A",
            ExpiresOn = new DateOnly(2027, 12, 31),
            PanelName = "Teaching selected cells",
            IsSelectedCellLot = true,
            IsActive = true
        };
        context.AntibodyPanelLots.AddRange(panel, selected);
        await context.SaveChangesAsync(ct);

        // Teaching phenotypes only — not a commercial antigram.
        // Columns: K, E, C, c, FYA, JKA, JKB, M, N
        AntigenExpression H = AntigenExpression.Homozygous;
        AntigenExpression T = AntigenExpression.Heterozygous;
        AntigenExpression P = AntigenExpression.Present;
        AntigenExpression A = AntigenExpression.Absent;

        await AddCellAsync(context, panel.Id, "1", PanelCellRole.Panel, 1, attrs, ct,
            ("K", A), ("E", A), ("C", H), ("c", A), ("FYA", H), ("JKA", H), ("JKB", A), ("M", H), ("N", A));
        await AddCellAsync(context, panel.Id, "2", PanelCellRole.Panel, 2, attrs, ct,
            ("K", P), ("E", H), ("C", A), ("c", H), ("FYA", A), ("JKA", A), ("JKB", H), ("M", A), ("N", H));
        await AddCellAsync(context, panel.Id, "3", PanelCellRole.Panel, 3, attrs, ct,
            ("K", A), ("E", A), ("C", A), ("c", H), ("FYA", T), ("JKA", T), ("JKB", T), ("M", T), ("N", T));
        await AddCellAsync(context, panel.Id, "4", PanelCellRole.Panel, 4, attrs, ct,
            ("K", A), ("E", A), ("C", T), ("c", T), ("FYA", A), ("JKA", H), ("JKB", A), ("M", H), ("N", A));
        await AddCellAsync(context, panel.Id, "5", PanelCellRole.Panel, 5, attrs, ct,
            ("K", P), ("E", A), ("C", T), ("c", T), ("FYA", H), ("JKA", A), ("JKB", H), ("M", A), ("N", H));
        await AddCellAsync(context, panel.Id, "6", PanelCellRole.Panel, 6, attrs, ct,
            ("K", A), ("E", A), ("C", A), ("c", H), ("FYA", A), ("JKA", T), ("JKB", T), ("M", T), ("N", T));
        await AddCellAsync(context, panel.Id, "7", PanelCellRole.Panel, 7, attrs, ct,
            ("K", A), ("E", T), ("C", A), ("c", H), ("FYA", H), ("JKA", H), ("JKB", A), ("M", A), ("N", A));
        await AddCellAsync(context, panel.Id, "8", PanelCellRole.Panel, 8, attrs, ct,
            ("K", A), ("E", H), ("C", H), ("c", A), ("FYA", A), ("JKA", A), ("JKB", A), ("M", T), ("N", T));
        await AddCellAsync(context, panel.Id, "AC", PanelCellRole.Autocontrol, 9, attrs, ct);

        await AddCellAsync(context, selected.Id, "S1", PanelCellRole.Selected, 1, attrs, ct,
            ("K", A), ("E", H), ("C", A), ("c", H), ("FYA", A), ("JKA", H), ("JKB", A), ("M", H), ("N", A));
        await AddCellAsync(context, selected.Id, "S2", PanelCellRole.Selected, 2, attrs, ct,
            ("K", P), ("E", A), ("C", H), ("c", A), ("FYA", H), ("JKA", A), ("JKB", H), ("M", A), ("N", H));
    }

    private static async Task AddCellAsync(
        BloodBankDbContext context,
        long lotId,
        string number,
        PanelCellRole role,
        int sort,
        IReadOnlyDictionary<string, BloodAttributeDefinition> attrs,
        CancellationToken ct,
        params (string Code, AntigenExpression Expression)[] typings)
    {
        var cell = new AntibodyPanelCell
        {
            LotId = lotId,
            CellNumber = number,
            Role = role,
            SortOrder = sort
        };
        context.AntibodyPanelCells.Add(cell);
        await context.SaveChangesAsync(ct);

        foreach (var (code, expression) in typings)
        {
            if (!attrs.TryGetValue(code, out var def))
            {
                continue;
            }

            context.AntibodyPanelCellAntigens.Add(new AntibodyPanelCellAntigen
            {
                CellId = cell.Id,
                BloodAttributeDefinitionId = def.Id,
                Expression = expression
            });
        }

        await context.SaveChangesAsync(ct);
    }
}
