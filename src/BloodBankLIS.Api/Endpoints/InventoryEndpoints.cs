using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Api.Endpoints;

public sealed record TransferUnitRequest(long ToLocationId, string? Reason);

public sealed record DiscardUnitRequest(string Reason);

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory").RequireAuthenticatedUser();

        // Search with optional filters.
        group.MapGet("/units", async (
            string? unitNumber,
            UnitStatus? status,
            AboGroup? abo,
            RhType? rh,
            long? productTypeId,
            long? locationId,
            DateTime? expiringBeforeUtc,
            InventoryService service,
            CancellationToken ct) =>
        {
            var criteria = new InventorySearchCriteria(unitNumber, status, abo, rh, productTypeId, locationId, expiringBeforeUtc);
            var units = await service.SearchAsync(criteria, ct);
            return Results.Ok(units.Select(BloodUnitDto.From));
        });

        group.MapGet("/units/{id:long}", async (long id, InventoryService service, CancellationToken ct) =>
        {
            var unit = await service.GetAsync(id, ct);
            return unit is null ? Results.NotFound() : Results.Ok(BloodUnitDto.From(unit));
        });

        group.MapGet("/units/{id:long}/history", async (long id, InventoryService service, CancellationToken ct) =>
        {
            var history = await service.GetHistoryAsync(id, ct);
            return Results.Ok(history.Select(InventoryStatusHistoryDto.From));
        });

        group.MapGet("/units/{id:long}/blood-attributes", async (long id, InventoryService service, BloodBankDbContext context, CancellationToken ct) =>
        {
            var attrs = await service.GetBloodAttributesAsync(id, ct);
            var defs = await context.BloodAttributeDefinitions.AsNoTracking().ToDictionaryAsync(d => d.Id, ct);
            return Results.Ok(attrs
                .Where(a => defs.ContainsKey(a.BloodAttributeDefinitionId))
                .Select(a =>
                {
                    var d = defs[a.BloodAttributeDefinitionId];
                    return UnitBloodAttributeDto.From(a, d.Code, d.Name, d.AntibodyName);
                }));
        });

        group.MapPost("/units/{id:long}/blood-attributes", async (long id, SaveUnitBloodAttributeRequest request, InventoryService service, BloodBankDbContext context, CancellationToken ct) =>
        {
            var result = await service.SaveBloodAttributeAsync(id, request, ct);
            if (!result.Succeeded || result.Value is null)
            {
                return EndpointResults.From(result, a => a);
            }

            var def = await context.BloodAttributeDefinitions.AsNoTracking()
                .FirstAsync(d => d.Id == result.Value.BloodAttributeDefinitionId, ct);
            return EndpointResults.Created(result, a => ($"/api/inventory/units/{id}/blood-attributes/{a.Id}", (object)UnitBloodAttributeDto.From(a, def.Code, def.Name, def.AntibodyName)));
        }).RequirePermission(PermissionCodes.InventoryReceive);

        // Intake: new units land in Quarantine.
        group.MapPost("/units", async (ReceiveUnitRequest request, InventoryService service, CancellationToken ct) =>
        {
            var result = await service.ReceiveUnitAsync(request, ct);
            return result.Succeeded
                ? Results.Created($"/api/inventory/units/{result.Unit!.Id}", BloodUnitDto.From(result.Unit))
                : ToFailure(result);
        }).RequirePermission(PermissionCodes.InventoryReceive);

        group.MapPost("/units/{id:long}/release", async (long id, InventoryService service, CancellationToken ct) =>
            ToHttpResult(await service.ReleaseFromQuarantineAsync(id, ct)))
            .RequirePermission(PermissionCodes.InventoryRelease);

        group.MapPost("/units/{id:long}/transfer", async (long id, TransferUnitRequest request, InventoryService service, CancellationToken ct) =>
            ToHttpResult(await service.TransferAsync(id, request.ToLocationId, request.Reason, ct)))
            .RequirePermission(PermissionCodes.InventoryTransfer);

        group.MapPost("/units/{id:long}/discard", async (long id, DiscardUnitRequest request, InventoryService service, CancellationToken ct) =>
            ToHttpResult(await service.DiscardAsync(id, request.Reason, ct)))
            .RequirePermission(PermissionCodes.InventoryDiscard);

        // Operational sweep that expires units past their expiration date/time.
        group.MapPost("/expire-due", async (InventoryService service, CancellationToken ct) =>
        {
            var count = await service.ExpireDueUnitsAsync(ct);
            return Results.Ok(new { expired = count });
        }).RequirePermission(PermissionCodes.InventoryDiscard);
    }

    private static IResult ToHttpResult(InventoryActionResult result) =>
        result.Succeeded ? Results.Ok(BloodUnitDto.From(result.Unit!)) : ToFailure(result);

    private static IResult ToFailure(InventoryActionResult result)
    {
        if (result.Evaluation is not null)
        {
            return Results.UnprocessableEntity(new
            {
                blocked = true,
                hardStops = result.Evaluation.HardStops.Select(r => new { r.Code, r.Message }),
                warnings = result.Evaluation.Warnings.Select(r => new { r.Code, r.Message })
            });
        }

        return string.Equals(result.Error, "Unit not found.", StringComparison.Ordinal)
            ? Results.NotFound(new { error = result.Error })
            : Results.BadRequest(new { error = result.Error });
    }
}
