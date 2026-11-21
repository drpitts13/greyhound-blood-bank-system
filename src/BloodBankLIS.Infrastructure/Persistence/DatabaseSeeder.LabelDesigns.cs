using System.Text.Json;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Once, on new and existing databases (Cycle 657): the starting specimen label designs. The tube design takes the
    /// layout already saved on the single-layout designer when there is one, so labels print as they did; an aliquot
    /// design for 1.5 × 0.5 in cryovial labels is added beside it. Both are the defaults for their purpose.
    /// </summary>
    private static async Task EnsureSpecimenLabelDesignsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.SpecimenLabelDesigns.AnyAsync(ct))
        {
            return;
        }

        var saved = await context.LabelLayouts.AsNoTracking().FirstOrDefaultAsync(l => l.Kind == LabelLayoutKind.Specimen, ct);
        LabelLayoutDocument? tube = null;
        if (saved is not null && !string.IsNullOrWhiteSpace(saved.DocumentJson))
        {
            try
            {
                tube = JsonSerializer.Deserialize<LabelLayoutDocument>(saved.DocumentJson, LabelFields.JsonOptions);
            }
            catch (JsonException)
            {
                tube = null;
            }
        }

        tube ??= LabelLayoutDefaults.Specimen();
        var aliquotWidth = SpecimenLabelDesignRules.ToDots(1.5m);
        var aliquotHeight = SpecimenLabelDesignRules.ToDots(0.5m);
        var aliquot = SpecimenLabelDesignRules.Starter(SpecimenLabelPurpose.Aliquot, aliquotWidth, aliquotHeight);

        context.SpecimenLabelDesigns.AddRange(
            new SpecimenLabelDesign
            {
                Code = "TUBE-STD",
                Name = "Standard tube label 2 × 1 in",
                Purpose = SpecimenLabelPurpose.Tube,
                WidthDots = tube.WidthDots > 0 ? tube.WidthDots : 406,
                HeightDots = tube.HeightDots > 0 ? tube.HeightDots : 203,
                DocumentJson = JsonSerializer.Serialize(tube, LabelFields.JsonOptions),
                IsDefault = true,
                IsActive = true
            },
            new SpecimenLabelDesign
            {
                Code = "ALIQ-STD",
                Name = "Aliquot label 1.5 × 0.5 in",
                Purpose = SpecimenLabelPurpose.Aliquot,
                WidthDots = aliquotWidth,
                HeightDots = aliquotHeight,
                DocumentJson = JsonSerializer.Serialize(aliquot, LabelFields.JsonOptions),
                IsDefault = true,
                IsActive = true
            });
        await context.SaveChangesAsync(ct);
    }
}
