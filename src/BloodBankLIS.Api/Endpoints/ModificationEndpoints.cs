using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Modifications;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>
/// Clinical product-modification endpoints: divide, pool, and apply a 1:1
/// modification (irradiate/thaw/volume-reduce/leukoreduce), plus the eligible-rules
/// lookup and per-unit modification history. All writes are gated by
/// <c>inventory.modify</c> (see docs/safety-rules.md).
/// </summary>
public static class ModificationEndpoints
{
    public static void MapModificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory: Modifications").RequireAuthenticatedUser();

        group.MapGet("/units/{id:long}/eligible-modifications", async (long id, BloodProductModificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetEligibleModificationsAsync(id, ct)));

        group.MapGet("/units/{id:long}/modifications", async (long id, BloodProductModificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetHistoryAsync(id, ct)));

        group.MapPost("/units/{id:long}/modifications/divide", async (long id, PerformDivideRequest request, BloodProductModificationService svc, CancellationToken ct) =>
            ToHttpResult(await svc.DivideAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.InventoryModify);

        group.MapPost("/modifications/pool", async (PerformPoolRequest request, BloodProductModificationService svc, CancellationToken ct) =>
            ToHttpResult(await svc.PoolAsync(request, ct)))
            .RequirePermission(PermissionCodes.InventoryModify);

        group.MapPost("/units/{id:long}/modifications/apply", async (long id, PerformSingleModificationRequest request, BloodProductModificationService svc, CancellationToken ct) =>
            ToHttpResult(await svc.ApplySingleAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.InventoryModify);
    }

    private static IResult ToHttpResult(ModificationActionResult result)
    {
        if (result.Succeeded)
        {
            return Results.Ok(new
            {
                modificationId = result.Modification!.Id,
                resultUnits = result.ResultUnits!.Select(BloodUnitDto.From)
            });
        }

        if (result.Evaluation is not null)
        {
            return Results.UnprocessableEntity(new
            {
                blocked = true,
                hardStops = result.Evaluation.HardStops.Select(r => new { r.Code, r.Message }),
                warnings = result.Evaluation.Warnings.Select(r => new { r.Code, r.Message })
            });
        }

        return string.Equals(result.Error, "Source unit not found.", StringComparison.Ordinal)
            ? Results.NotFound(new { error = result.Error })
            : Results.BadRequest(new { error = result.Error });
    }
}
