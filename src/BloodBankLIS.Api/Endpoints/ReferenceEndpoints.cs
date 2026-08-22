using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>
/// Read-only reference/config data used to populate UI pickers (product catalog,
/// inventory locations). Requires an authenticated user but no specific permission.
/// </summary>
public static class ReferenceEndpoints
{
    public static void MapReferenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reference").WithTags("Reference").RequireAuthenticatedUser();

        group.MapGet("/product-types", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var types = await context.ProductTypes.AsNoTracking()
                .OrderBy(t => t.ProductCode)
                .ToListAsync(ct);
            return Results.Ok(types.Select(ProductTypeDto.From));
        });

        group.MapGet("/locations", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var locations = await context.InventoryLocations.AsNoTracking()
                .OrderBy(l => l.Code)
                .ToListAsync(ct);
            return Results.Ok(locations.Select(InventoryLocationDto.From));
        });

        group.MapGet("/ordering-locations", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var locations = await context.OrderingLocations.AsNoTracking()
                .Where(l => l.IsActive)
                .OrderBy(l => l.Code)
                .ToListAsync(ct);
            return Results.Ok(locations.Select(OrderingLocationRefDto.From));
        });

        group.MapGet("/ordering-providers", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var providers = await context.OrderingProviders.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
            return Results.Ok(providers.Select(OrderingProviderRefDto.From));
        });

        group.MapGet("/blood-attributes", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var attrs = await context.BloodAttributeDefinitions.AsNoTracking()
                .Where(d => d.IsActive && !d.IsDraft)
                .OrderBy(d => d.SortOrder)
                .ThenBy(d => d.Code)
                .ToListAsync(ct);
            return Results.Ok(attrs.Select(d => new BloodAttributeListItemDto(
                d.Id, d.Code, d.Name, d.AntibodyName, d.IsClinicallySignificant, d.SortOrder)));
        });

        group.MapGet("/specimen-types", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var types = await context.SpecimenTypeDefinitions.AsNoTracking()
                .Where(t => t.IsActive && !t.IsDraft)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Code)
                .ToListAsync(ct);
            return Results.Ok(types.Select(t => new SpecimenTypeListItemDto(t.Code, t.Description, t.SortOrder)));
        });

        group.MapGet("/subtests", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var subtests = await context.SubtestDefinitions.AsNoTracking()
                .Where(s => s.IsActive && !s.IsDraft)
                .OrderBy(s => s.Code)
                .ToListAsync(ct);
            return Results.Ok(subtests.Select(s => new SubtestListItemDto(s.Code, s.Name, s.ResultType)));
        });

        group.MapGet("/phases", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var phases = await context.PhaseDefinitions.AsNoTracking()
                .Where(p => p.IsActive && !p.IsDraft)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Code)
                .ToListAsync(ct);
            return Results.Ok(phases.Select(p => new PhaseListItemDto(
                p.Code, p.Name, p.SortOrder, p.IncludeInInterpretation, p.IsCheckCell, p.ValidatesPhaseCode)));
        });

        group.MapGet("/test-groupers", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var groupers = await context.TestGroupers.AsNoTracking()
                .Where(g => g.IsActive && !g.IsDraft)
                .OrderBy(g => g.Code)
                .ToListAsync(ct);
            return Results.Ok(groupers.Select(g => new TestGrouperListItemDto(
                g.Code,
                g.Name,
                TestGrouperMembers.Parse(g.MemberTestsJson).Select(m => m.TestCode).ToList())));
        });

        group.MapGet("/test-definitions", async (BloodBankDbContext context, CancellationToken ct) =>
        {
            var tests = await context.TestDefinitions.AsNoTracking()
                .Where(t => t.IsActive && !t.IsDraft)
                .OrderBy(t => t.Code)
                .ToListAsync(ct);
            return Results.Ok(tests.Select(TestDefinitionListItemDto.From));
        });

        group.MapGet("/test-definitions/{code}", async (string code, BloodBankDbContext context, CancellationToken ct) =>
        {
            var normalized = code.Trim().ToUpperInvariant();
            var def = await context.TestDefinitions.AsNoTracking()
                .Where(t => t.IsActive && !t.IsDraft && t.Code == normalized)
                .OrderByDescending(t => t.Version)
                .FirstOrDefaultAsync(ct);
            if (def is null)
            {
                return Results.NotFound();
            }

            var catalog = await LoadSubtestCatalogAsync(context, ct);
            var phases = await LoadPhaseCatalogAsync(context, ct);
            var resolved = PanelSubtestAssignments.ResolveForEntry(
                def.PanelSubtestsJson,
                catalog,
                useAboRhDefaultsWhenEmpty: def.ResultValueType == ResultValueType.AboRh,
                phases);

            var interpretationOptions = InterpretationLogicDefinitions.Parse(def.InterpretationLogicJson)
                .Select(r => new InterpretationOptionDto(r.InterpretationKey, r.Label))
                .ToList();

            var bloodAttrScope = BloodAttributeScope.Parse(def.BloodAttributeScopeJson);
            var bloodAttrCatalog = await context.BloodAttributeDefinitions.AsNoTracking()
                .Where(d => d.IsActive && !d.IsDraft)
                .ToDictionaryAsync(d => d.Code, ct);
            var scopedBloodAttrs = bloodAttrScope
                .Where(s => bloodAttrCatalog.ContainsKey(s.Code))
                .Select(s =>
                {
                    var d = bloodAttrCatalog[s.Code];
                    return new BloodAttributeListItemDto(d.Id, d.Code, d.Name, d.AntibodyName, d.IsClinicallySignificant, d.SortOrder);
                })
                .ToList();

            return Results.Ok(new TestDefinitionForEntryDto(
                def.Code,
                def.Name,
                def.ResultValueType,
                def.AllowedResultValues,
                resolved.Select(s => new ResolvedPanelSubtestDto(
                    s.SubtestCode,
                    s.Label,
                    s.ResultType,
                    s.Choices.Select(c => new SubtestChoiceDto(c.Code, c.Label, c.Polarity)).ToList(),
                    s.Required,
                    s.SortOrder,
                    (s.Phases ?? Array.Empty<ResolvedPanelPhase>())
                        .Select(p => new ResolvedPanelPhaseDto(
                            p.PhaseCode, p.Label, p.Required, p.IncludeInInterpretation,
                            p.IsCheckCell, p.ValidatesPhaseCode, p.SortOrder))
                        .ToList())).ToList(),
                interpretationOptions,
                scopedBloodAttrs,
                def.BloodAttributeScopeKind,
                def.ContributesToUnitBloodAttributes));
        });
    }

    private static async Task<Dictionary<string, SubtestDefinition>> LoadSubtestCatalogAsync(
        BloodBankDbContext context, CancellationToken ct)
    {
        var items = await context.SubtestDefinitions.AsNoTracking()
            .Where(s => s.IsActive && !s.IsDraft)
            .ToListAsync(ct);
        return items
            .GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Version).First(), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, PhaseDefinition>> LoadPhaseCatalogAsync(
        BloodBankDbContext context, CancellationToken ct)
    {
        var items = await context.PhaseDefinitions.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDraft)
            .ToListAsync(ct);
        return items
            .GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Version).First(), StringComparer.OrdinalIgnoreCase);
    }
}
