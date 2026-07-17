using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Seeds reference catalog rows required by integration tests (SQLite uses EnsureCreated, not DatabaseSeeder).
/// </summary>
internal static class TestCatalogSeeder
{
    public static async Task EnsureSpecimenTypesAsync(BloodBankDbContext context, DateTime effectiveUtc, CancellationToken ct = default)
    {
        if (await context.SpecimenTypeDefinitions.AnyAsync(ct))
        {
            return;
        }

        SpecimenTypeDefinition Type(string code, string description, int sort, params string[] excludedTests) => new()
        {
            Code = code,
            Description = description,
            ExcludedTestCodesJson = SpecimenTypeExcludedTests.Serialize(excludedTests),
            SortOrder = sort,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = effectiveUtc,
            Version = 1
        };

        context.SpecimenTypeDefinitions.AddRange(
            Type("EDTA", "EDTA Whole Blood", 1),
            Type("SERUM", "Serum", 2, "XM"));

        await context.SaveChangesAsync(ct);
    }
}
